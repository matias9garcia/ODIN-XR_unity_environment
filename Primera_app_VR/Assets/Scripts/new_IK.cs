using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections;

public class SolveIK_Network : MonoBehaviour {

    [Header("--- Configuración de Red ---")]
    public string urlServidor = "http://127.0.0.1:5000/api/angulos_braccio";

    [Header("--- Configuración IK ---")]
    public bool useIK = true;
    public bool autoEnd = true;
    public Transform targetObj; 

    [Header("--- Control de Envío (API) ---")]
    public float tiempoParaEstabilizar = 0.5f; 
    public float umbralMovimiento = 0.001f;

    [Header("--- UI Debug ---")]
    public TMP_Text textoAngulos;

    [Header("--- Gripper (Pinza) ---")]
    public bool isPinching = false; 
    public float longitudPinza = 0.10f; 
    [Range(10, 73)] public float anguloAbierto = 73f; 
    [Range(10, 73)] public float anguloCerrado = 10f; 

    [Header("--- Ajustes Brazo ---")]
    // Nota: El cálculo antiguo usa offsets fijos implícitos
    [Range(1f, 20f)] public float velocidadSuavizado = 10f; 

    [Header("--- Visuales Pinza ---")]
    public Transform visualPinzaIzq; 
    public Transform visualPinzaDer; 
    public Vector3 ejeRotacionPinza = new Vector3(1, 0, 0); 
    public float visualMultiplier = 1.0f; 

    // Variables Privadas Visuales
    private Quaternion _initialRotIzq;
    private Quaternion _initialRotDer;
    
    // Control de estado
    private bool _ultimoEstadoPinching; 

    // --- SALIDAS (Ángulos) ---
    [Header("Salidas Calculadas")]
    [Range(0, 180)] public float thetaBase = 90f;          
    [Range(0, 180)] public float thetaShoulder = 90f;      
    [Range(0, 180)] public float thetaElbow = 90f;         
    [Range(0, 180)] public float thetaWristVertical = 90f; 
    [Range(0, 180)] public float thetaWristRotation = 90f; 
    [Range(10, 73)] public float thetaGripper = 10f;       

    public GameObject[] arms = new GameObject[5]; 
    
    // Variables Matemáticas
    private float BASE_HGT, HUMERUS, ULNA, hum_sq, uln_sq;
    private int _estadoPinzaLogico = 1;

    // Estado Lógico Movimiento
    private Vector3 _ultimaPosicionTarget;
    private float _timerEstabilidad = 0f;
    private bool _datosEnviados = false;
    private string _estadoTextoDebug = ""; 

    // Estructura JSON
    [Serializable]
    private struct BraccioData {
        public long timestamp;
        public int m1, m2, m3, m4, m5, m6, apertura_pinza;
    }

    void Start () {
        // Inicialización de Targets y Visuales
        if (targetObj != null) _ultimaPosicionTarget = targetObj.position;
        if (visualPinzaIzq != null) _initialRotIzq = visualPinzaIzq.localRotation;
        if (visualPinzaDer != null) _initialRotDer = visualPinzaDer.localRotation;
        
        _ultimoEstadoPinching = isPinching;

        // --- CALIBRACIÓN ---
        if(arms[0] != null && arms[3] != null) {
            BASE_HGT = Vector3.Distance(arms[0].transform.position, arms[1].transform.position);
            HUMERUS = Vector3.Distance(arms[1].transform.position, arms[2].transform.position);
            ULNA = Vector3.Distance(arms[2].transform.position, arms[3].transform.position);
            
            // Protección mínima
            if (HUMERUS < 0.001f) HUMERUS = 0.125f;
            if (ULNA < 0.001f) ULNA = 0.125f;
            
            hum_sq = HUMERUS*HUMERUS;
            uln_sq = ULNA*ULNA;
        }
    }

    void Update () {
        if (targetObj == null || arms[0] == null) return;
        
        // 1. Calcular IK (Con matemática ajustada a 360)
        ProcesarIK(); 
        
        // 2. Mover Visuales Unity
        ActualizarVisualesBrazo();

        // 3. Lógica de Red y UI
        GestionarRedYUI();
    }

    // ---------------------------------------------------------
    //   LÓGICA HÍBRIDA: PREPARACIÓN DE DATOS
    // ---------------------------------------------------------
    void ProcesarIK() {
        if (!useIK) return;

        // Calculamos la posición local relativa a la base
        Vector3 localTarget = (arms[0].transform.parent != null) ? 
            arms[0].transform.parent.InverseTransformPoint(targetObj.position) : 
            targetObj.position - arms[0].transform.position;

        // Compensación de Pinza
        Vector3 directionToTarget = new Vector3(localTarget.x, 0, localTarget.z).normalized;
        // Protección contra vector cero si el target está justo encima
        if (directionToTarget == Vector3.zero) directionToTarget = Vector3.forward;

        Vector3 wristTarget = localTarget - (directionToTarget * longitudPinza);

        // Llamamos al calculador con la lógica mejorada
        SetArm_360Logic(wristTarget.x, wristTarget.y, wristTarget.z, autoEnd);

        // Gestión del Ángulo de la Pinza (M6)
        if (isPinching) { thetaGripper = anguloCerrado; _estadoPinzaLogico = 0; } 
        else { thetaGripper = anguloAbierto; _estadoPinzaLogico = 1; }
    }

    // ---------------------------------------------------------
    //   CÁLCULO IK CON SOPORTE 360° (Modificado)
    // ---------------------------------------------------------
    void SetArm_360Logic(float x, float y, float z, bool endHorizontal) {
        // 1. Ángulo Base (M1)
        float bas_angle_r = Mathf.Atan2(x, z);
        float bas_angle_d = bas_angle_r * Mathf.Rad2Deg + 90f;
        
        // Normalización a 0-360
        if (bas_angle_d < 0) bas_angle_d += 360f;

        // Detección "IsBackwards" (Si el target está detrás)
        bool isBackwards = false;
        if (bas_angle_d > 180f) {
            isBackwards = true;
            bas_angle_d -= 180f; // Giramos la base para mirar "hacia atrás"
        }

        // --- Cálculos Geométricos (Teorema del Coseno) ---
        float wrt_y = y - BASE_HGT; // Altura muñeca relativa
        float s_w = x * x + z * z + wrt_y * wrt_y; // Distancia al cuadrado
        float s_w_sqrt = Mathf.Sqrt(s_w);

        // 2. Ángulo Codo (M3)
        float cosAngleElbow = (hum_sq + uln_sq - s_w) / (2f * HUMERUS * ULNA);
        float elb_angle_r = Mathf.Acos(Mathf.Clamp(cosAngleElbow, -1f, 1f));
        float elb_angle_d = 270f - elb_angle_r * Mathf.Rad2Deg;

        // 3. Ángulo Hombro (M2) - Cálculo base
        float a1 = Mathf.Atan2(wrt_y, Mathf.Sqrt(x * x + z * z));
        float cosAngleShoulder = (hum_sq + s_w - uln_sq) / (2f * HUMERUS * s_w_sqrt);
        float a2 = Mathf.Acos(Mathf.Clamp(cosAngleShoulder, -1f, 1f));
        float shl_angle_r = a1 + a2;
        float shl_angle_d = 180f - shl_angle_r * Mathf.Rad2Deg; 

        // --- ASIGNACIÓN DE SALIDAS (Aplicando Inversión si es Backwards) ---
        
        // BASE
        thetaBase = Mathf.Clamp(bas_angle_d, 0f, 180f);

        // HOMBRO: Si estamos hacia atrás, invertimos el ángulo
        float finalShoulder = shl_angle_d;
        if (isBackwards) finalShoulder = 180f - shl_angle_d;
        
        if (!float.IsNaN(finalShoulder))
            thetaShoulder = Mathf.Clamp(finalShoulder, 0f, 180f);

        // CODO: Se mantiene (la geometría relativa del brazo no cambia)
        if (!float.IsNaN(elb_angle_d))
            thetaElbow = Mathf.Clamp(elb_angle_d, 0f, 180f);

        // 4. Ángulo Muñeca Vertical (M4)
        if (endHorizontal) {
            // Ángulo ideal mirando al frente
            float wr_ang = 90f + (shl_angle_d - 90f) + (elb_angle_d - 90f);
            
            // Si estamos hacia atrás, invertimos la muñeca para mantener el horizonte
            if (isBackwards) wr_ang = 180f - wr_ang;
            
            thetaWristVertical = Mathf.Clamp(wr_ang, 0f, 180f);
        }
        else {
            thetaWristVertical = 90f; 
        }
    }

    // ---------------------------------------------------------
    //   VISUALES (Visualización Suavizada en Unity)
    // ---------------------------------------------------------
    void ActualizarVisualesBrazo() {
        arms[0].transform.localRotation = Quaternion.Slerp(arms[0].transform.localRotation, Quaternion.Euler(0, thetaBase, 0), Time.deltaTime * velocidadSuavizado);
        arms[1].transform.localRotation = Quaternion.Slerp(arms[1].transform.localRotation, Quaternion.Euler(0, 0, thetaShoulder - 90), Time.deltaTime * velocidadSuavizado);
        arms[2].transform.localRotation = Quaternion.Slerp(arms[2].transform.localRotation, Quaternion.Euler(0, 0, thetaElbow - 90), Time.deltaTime * velocidadSuavizado);
        arms[3].transform.localRotation = Quaternion.Slerp(arms[3].transform.localRotation, Quaternion.Euler(0, 0, thetaWristVertical - 90), Time.deltaTime * velocidadSuavizado);
        arms[4].transform.localRotation = Quaternion.Slerp(arms[4].transform.localRotation, Quaternion.Euler(0, thetaWristRotation, 0), Time.deltaTime * velocidadSuavizado);

        // Visuales de la Pinza
        if (visualPinzaIzq != null && visualPinzaDer != null) {
            float anguloVisual = (thetaGripper - anguloAbierto) * visualMultiplier;
            Quaternion rotacionApertura = Quaternion.Euler(ejeRotacionPinza * anguloVisual);
            visualPinzaIzq.localRotation = Quaternion.Slerp(visualPinzaIzq.localRotation, _initialRotIzq * rotacionApertura, Time.deltaTime * velocidadSuavizado);
            visualPinzaDer.localRotation = Quaternion.Slerp(visualPinzaDer.localRotation, _initialRotDer * rotacionApertura, Time.deltaTime * velocidadSuavizado);
        }
    }

    // ---------------------------------------------------------
    //   RED Y UI (Lógica de Envío y Debug)
    // ---------------------------------------------------------
    void GestionarRedYUI() {
        // Detección de cambio de PINZA
        if (isPinching != _ultimoEstadoPinching) {
            StartCoroutine(EnviarDatosPOST());
            _ultimoEstadoPinching = isPinching;
            _datosEnviados = true; 
            _estadoTextoDebug = "<color=#00FF00><b>ACCION PINZA ENVIADA</b></color>";
        }

        // Detección de MOVIMIENTO
        float distancia = Vector3.Distance(targetObj.position, _ultimaPosicionTarget);

        if (distancia > umbralMovimiento) {
            _timerEstabilidad = 0f;
            _datosEnviados = false; 
            _ultimaPosicionTarget = targetObj.position;
            _estadoTextoDebug = "<color=yellow>MOVIENDO...</color>";
        } 
        else {
            _timerEstabilidad += Time.deltaTime;
            if (_timerEstabilidad < tiempoParaEstabilizar) {
                if (!_datosEnviados)
                    _estadoTextoDebug = $"<color=orange>ESTABILIZANDO... {(_timerEstabilidad/tiempoParaEstabilizar)*100:F0}%</color>";
            }
            else {
                if (!_datosEnviados) {
                    StartCoroutine(EnviarDatosPOST());
                    _datosEnviados = true; 
                    _estadoTextoDebug = "<color=#00FF00><b>ENVIADO (QUIETO)</b></color>";
                }
            }
        }
        
        // ACTUALIZACIÓN UI TMP
        if (textoAngulos != null) {
            string textoEstadoPinza;
            if (isPinching) textoEstadoPinza = $"<color=black><b>Abierto ({(int)anguloCerrado}°)</b></color>"; 
            else textoEstadoPinza = $"<color=black>Cerrado ({(int)anguloAbierto}°)</color>";

            textoAngulos.text = 
                $"{_estadoTextoDebug}\n" + 
                $"ESTADO: {textoEstadoPinza}\n" + 
                $"----------------\n" +
                $"M1 (Base):   <b>{(int)thetaBase}°</b>\n" +
                $"M2 (Hombro): <b>{(int)thetaShoulder}°</b>\n" +
                $"M3 (Codo):   <b>{(int)thetaElbow}°</b>\n" +
                $"M4 (Muñ V):  <b>{(int)thetaWristVertical}°</b>\n" +
                $"M5 (Muñ R):  <b>{(int)thetaWristRotation}°</b>\n" +
                $"M6 (Pinza):  <b>{(int)thetaGripper}°</b>"; 
        }
    }

    IEnumerator EnviarDatosPOST() {
        BraccioData data = new BraccioData();
        data.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        data.m1 = (int)thetaBase; data.m2 = (int)thetaShoulder; data.m3 = (int)thetaElbow;
        data.m4 = (int)thetaWristVertical; data.m5 = (int)thetaWristRotation; 
        data.m6 = (int)thetaGripper; data.apertura_pinza = _estadoPinzaLogico;

        string json = JsonUtility.ToJson(data);
        using (UnityWebRequest req = new UnityWebRequest(urlServidor, "POST")) {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
        }
    }
}
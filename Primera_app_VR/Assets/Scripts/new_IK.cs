using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections;

public class SolveIK_Network : MonoBehaviour {

    [Header("--- Configuración de Red ---")]
    public string urlServidor = "http://127.0.0.1:5000/api/braccio";

    [Header("--- Configuración IK ---")]
    public bool useIK = true;
    public bool autoEnd = true; 
    public Transform targetObj; 

    [Header("--- Control de Envío (API) ---")]
    public float tiempoParaEstabilizar = 0.5f; 
    public float umbralMovimiento = 0.001f;

    [Header("--- UI Debug ---")]
    public TMP_Text textoAngulos;

    [Header("--- Gripper ---")]
    public bool isPinching = false; 
    public float longitudPinza = 0.10f; 
    // Ajustado a tu solicitud: Cerrado = 10, Abierto = 73
    [Range(10, 73)] public float anguloAbierto = 73f; 
    [Range(10, 73)] public float anguloCerrado = 10f; 

    [Header("--- Ajustes Brazo ---")]
    public float offsetHombro = 180f; 
    public float offsetCodo = 270f;
    [Range(1f, 20f)] public float velocidadSuavizado = 10f; 

    [Header("--- Visuales Pinza ---")]
    public Transform visualPinzaIzq; 
    public Transform visualPinzaDer; 
    public Vector3 ejeRotacionPinza = new Vector3(1, 0, 0); 
    public float visualMultiplier = 1.0f; 

    // Variables Privadas
    private Quaternion _initialRotIzq;
    private Quaternion _initialRotDer;
    
    // Control de estado
    private bool _ultimoEstadoPinching; 

    // Salidas
    [Header("Salidas")]
    [Range(0, 180)] public float thetaBase = 90f;          
    [Range(0, 180)] public float thetaShoulder = 90f;      
    [Range(0, 180)] public float thetaElbow = 90f;         
    [Range(0, 180)] public float thetaWristVertical = 90f; 
    [Range(0, 180)] public float thetaWristRotation = 90f; 
    [Range(10, 73)] public float thetaGripper = 10f;       

    public GameObject[] arms = new GameObject[5]; 
    private float BASE_HGT, HUMERUS, ULNA, hum_sq, uln_sq;
    private int _estadoPinzaLogico = 1;

    // Estado Lógico Movimiento
    private Vector3 _ultimaPosicionTarget;
    private float _timerEstabilidad = 0f;
    private bool _datosEnviados = false;
    private string _estadoTextoDebug = ""; 

    [Serializable]
    private struct BraccioData {
        public long timestamp;
        public int m1, m2, m3, m4, m5, m6, apertura_pinza;
    }

    void Start () {
        if (targetObj != null) _ultimaPosicionTarget = targetObj.position;

        if (visualPinzaIzq != null) _initialRotIzq = visualPinzaIzq.localRotation;
        if (visualPinzaDer != null) _initialRotDer = visualPinzaDer.localRotation;
        
        _ultimoEstadoPinching = isPinching;

        if(arms[0] != null && arms[3] != null) {
            BASE_HGT = Vector3.Distance(arms[0].transform.position, arms[1].transform.position);
            HUMERUS = Vector3.Distance(arms[1].transform.position, arms[2].transform.position);
            ULNA = Vector3.Distance(arms[2].transform.position, arms[3].transform.position);
            if (HUMERUS < 0.001f) HUMERUS = 0.125f;
            if (ULNA < 0.001f) ULNA = 0.125f;
            hum_sq = HUMERUS*HUMERUS;
            uln_sq = ULNA*ULNA;
        }
    }

    void Update () {
        if (targetObj == null || arms[0] == null) return;
        
        ProcesarIK(); 
        ActualizarVisualesBrazo();

        // 1. Detección de cambio de PINZA (Prioridad)
        if (isPinching != _ultimoEstadoPinching) {
            StartCoroutine(EnviarDatosPOST());
            _ultimoEstadoPinching = isPinching;
            _datosEnviados = true; 
            _estadoTextoDebug = "<color=#00FF00><b>ACCION PINZA ENVIADA</b></color>";
        }

        // 2. Detección de MOVIMIENTO
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
        
        // --- ACTUALIZACIÓN UI ---
        if (textoAngulos != null) {
            // Lógica de la Segunda Línea (Estado Pinza)
            string textoEstadoPinza;
            if (isPinching)
                textoEstadoPinza = $"<color=black><b>Cerrado ({(int)anguloCerrado}°)</b></color>";
            else
                textoEstadoPinza = $"<color=black>Abierto ({(int)anguloAbierto}°)</color>";

            textoAngulos.text = 
                $"{_estadoTextoDebug}\n" + 
                $"ESTADO: {textoEstadoPinza}\n" + // Segunda línea modificada
                $"----------------\n" +
                $"M1 (Base):   <b>{(int)thetaBase}°</b>\n" +
                $"M2 (Hombro): <b>{(int)thetaShoulder}°</b>\n" +
                $"M3 (Codo):   <b>{(int)thetaElbow}°</b>\n" +
                $"M4 (Muñ V):  <b>{(int)thetaWristVertical}°</b>\n" +
                $"M5 (Muñ R):  <b>{(int)thetaWristRotation}°</b>\n" +
                $"M6 (Pinza):  <b>{(int)thetaGripper}°</b>"; // M6 devuelto al original
        }
    }

    void ProcesarIK() {
        if (!useIK) return;
        Vector3 localTarget = (arms[0].transform.parent != null) ? 
            arms[0].transform.parent.InverseTransformPoint(targetObj.position) : 
            targetObj.position - arms[0].transform.position;
        Vector3 dir = localTarget.normalized;
        if (dir == Vector3.zero) dir = Vector3.up;
        Vector3 wristTarget = localTarget - (dir * longitudPinza);
        float maxReach = (HUMERUS + ULNA) - 0.001f;
        if (wristTarget.magnitude > maxReach) wristTarget = wristTarget.normalized * maxReach;

        SetArm(wristTarget.x, wristTarget.y, wristTarget.z, autoEnd);

        if (isPinching) { thetaGripper = anguloCerrado; _estadoPinzaLogico = 0; } 
        else { thetaGripper = anguloAbierto; _estadoPinzaLogico = 1; }
    }

    void ActualizarVisualesBrazo() {
        arms[0].transform.localRotation = Quaternion.Slerp(arms[0].transform.localRotation, Quaternion.Euler(0, thetaBase, 0), Time.deltaTime * velocidadSuavizado);
        arms[1].transform.localRotation = Quaternion.Slerp(arms[1].transform.localRotation, Quaternion.Euler(0, 0, thetaShoulder - 90), Time.deltaTime * velocidadSuavizado);
        arms[2].transform.localRotation = Quaternion.Slerp(arms[2].transform.localRotation, Quaternion.Euler(0, 0, thetaElbow - 90), Time.deltaTime * velocidadSuavizado);
        arms[3].transform.localRotation = Quaternion.Slerp(arms[3].transform.localRotation, Quaternion.Euler(0, 0, thetaWristVertical - 90), Time.deltaTime * velocidadSuavizado);
        arms[4].transform.localRotation = Quaternion.Slerp(arms[4].transform.localRotation, Quaternion.Euler(0, thetaWristRotation, 0), Time.deltaTime * velocidadSuavizado);
        if (visualPinzaIzq != null && visualPinzaDer != null) {
            float anguloVisual = (thetaGripper - anguloAbierto) * visualMultiplier;
            Quaternion rotacionApertura = Quaternion.Euler(ejeRotacionPinza * anguloVisual);
            visualPinzaIzq.localRotation = Quaternion.Slerp(visualPinzaIzq.localRotation, _initialRotIzq * rotacionApertura, Time.deltaTime * velocidadSuavizado);
            visualPinzaDer.localRotation = Quaternion.Slerp(visualPinzaDer.localRotation, _initialRotDer * rotacionApertura, Time.deltaTime * velocidadSuavizado);
        }
    }

    void SetArm(float x, float y, float z, bool endHorizontal) {
        float maxReach = HUMERUS + ULNA - 0.001f;
        float distSq = x*x + y*y + z*z;
        if (distSq > maxReach * maxReach) {
            float scale = maxReach / Mathf.Sqrt(distSq);
            x *= scale; y *= scale; z *= scale;
        }
        float r_abs = Mathf.Sqrt(x*x + z*z);
        if (r_abs < 0.05f) r_abs = 0.05f;
        float rawAngle = Mathf.Atan2(x, z) * Mathf.Rad2Deg + 90f;
        if (rawAngle < 0) rawAngle += 360f; 
        bool isBackwards = false;
        float baseFinal = rawAngle;
        if (rawAngle > 180f) { isBackwards = true; baseFinal = rawAngle - 180f; }
        thetaBase = Mathf.Clamp(baseFinal, 0f, 180f);
        float dy = y - BASE_HGT;
        float hypSq = r_abs*r_abs + dy*dy; 
        float hyp = Mathf.Sqrt(hypSq);
        float cElb = (hum_sq + uln_sq - hypSq) / (2 * HUMERUS * ULNA);
        cElb = Mathf.Clamp(cElb, -1.0f, 1.0f);
        float thetaElbowIdeal = offsetCodo - Mathf.Acos(cElb) * Mathf.Rad2Deg;
        float angleElev = Mathf.Atan2(dy, r_abs); 
        float cShl = (hum_sq + hypSq - uln_sq) / (2 * HUMERUS * hyp);
        cShl = Mathf.Clamp(cShl, -1.0f, 1.0f);
        float thetaShoulderIdeal = offsetHombro - (angleElev + Mathf.Acos(cShl)) * Mathf.Rad2Deg;
        if (!isBackwards) { thetaShoulder = thetaShoulderIdeal; thetaElbow = thetaElbowIdeal; } 
        else { thetaShoulder = 180f - thetaShoulderIdeal; thetaElbow = thetaElbowIdeal; }
        if (endHorizontal) {
            float wr_ang = 90f + (thetaShoulder - 90f) + (thetaElbow - 90f);
            if (isBackwards) wr_ang = 180f - wr_ang; 
            thetaWristVertical = Mathf.Clamp(wr_ang, 0f, 180f);
        }
        if(float.IsNaN(thetaShoulder)) thetaShoulder = 90f;
        if(float.IsNaN(thetaElbow)) thetaElbow = 90f;
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
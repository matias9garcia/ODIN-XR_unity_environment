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
    
    [Tooltip("El objeto 'Target' que mueves")]
    public Transform targetObj; 

    [Header("--- Control de Envío (API) ---")]
    public float tiempoParaEstabilizar = 0.5f; 
    public float umbralMovimiento = 0.001f;

    [Header("--- UI Debug ---")]
    public TMP_Text textoAngulos;

    [Header("--- Gripper ---")]
    public bool isPinching = false; 
    public float longitudPinza = 0.10f; 
    [Range(10, 73)] public float anguloAbierto = 10f;
    [Range(10, 73)] public float anguloCerrado = 73f;

    [Header("--- Ajustes Brazo ---")]
    public float offsetHombro = 180f; 
    public float offsetCodo = 270f;
    [Range(1f, 20f)] public float velocidadSuavizado = 10f; 

    // Salidas
    [Header("Salidas")]
    [Range(0, 180)] public float thetaBase = 90f;
    [Range(0, 180)] public float thetaShoulder = 90f;
    [Range(0, 180)] public float thetaElbow = 90f;
    [Range(0, 180)] public float thetaWristVertical = 90f;
    [Range(0, 180)] public float thetaWristRotation = 90f;
    [Range(10, 73)] public float thetaGripper = 10f;

    // Referencias
    public GameObject[] arms = new GameObject[5]; 

    // Internas
    private float BASE_HGT, HUMERUS, ULNA;
    private float hum_sq, uln_sq;
    private int _estadoPinzaLogico = 1;

    // Estado Lógico
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

        // Lógica de Red y UI
        float distancia = Vector3.Distance(targetObj.position, _ultimaPosicionTarget);
        bool cambioPinza = (_estadoPinzaLogico == 1 && isPinching) || (_estadoPinzaLogico == 0 && !isPinching);

        if (distancia > umbralMovimiento || cambioPinza) {
            _timerEstabilidad = 0f;
            _datosEnviados = false;
            _ultimaPosicionTarget = targetObj.position;
            _estadoTextoDebug = "<color=yellow>MOVIENDO...</color>";
        } 
        else {
            _timerEstabilidad += Time.deltaTime;
            if (_timerEstabilidad < tiempoParaEstabilizar) {
                _estadoTextoDebug = $"<color=orange>ESTABILIZANDO... {(_timerEstabilidad/tiempoParaEstabilizar)*100:F0}%</color>";
            }
            else {
                if (!_datosEnviados) {
                    StartCoroutine(EnviarDatosPOST());
                    _datosEnviados = true; 
                }
                _estadoTextoDebug = "<color=#00FF00><b>ENVIADO</b></color>";
            }
        }
        
        if (textoAngulos != null) {
            string estadoP = isPinching ? "[CERRADA]" : "[ABIERTA]";
            textoAngulos.text = $"{_estadoTextoDebug} {estadoP}\n" +
                                $"B:{(int)thetaBase} H:{(int)thetaShoulder} C:{(int)thetaElbow} W:{(int)thetaWristVertical}";
        }
    }

    void ProcesarIK() {
        if (!useIK) return;

        Vector3 localTarget = (arms[0].transform.parent != null) ? 
            arms[0].transform.parent.InverseTransformPoint(targetObj.position) : 
            targetObj.position - arms[0].transform.position;

        // Ajuste Pinza
        Vector3 dir = localTarget.normalized;
        if (dir == Vector3.zero) dir = Vector3.up;
        Vector3 wristTarget = localTarget - (dir * longitudPinza);

        // Protección de Alcance
        float maxReach = (HUMERUS + ULNA) - 0.001f;
        if (wristTarget.magnitude > maxReach) {
            wristTarget = wristTarget.normalized * maxReach;
        }

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
    }

    void SetArm(float x, float y, float z, bool endHorizontal) {
        // Distancia horizontal radial
        float rad = Mathf.Sqrt(x*x + z*z);
        if (rad < 0.05f) rad = 0.05f; 
        
        // 1. CÁLCULO INTELIGENTE DE LA BASE
        float base_ang_raw = Mathf.Atan2(x, z) * Mathf.Rad2Deg + 90f;
        
        // Variable clave: Distancia Horizontal Efectiva
        float hDist = rad;

        // ¿Está el objetivo fuera del rango 0-180 de la base? (Zona Trasera)
        if (base_ang_raw < 0 || base_ang_raw > 180) 
        {
            // ESTRATEGIA: Girar la base 180 grados para mirar al "lado opuesto"
            // y hacer que el brazo alcance "hacia atrás" usando distancia negativa.
            
            if (base_ang_raw < 0) base_ang_raw += 180;
            else if (base_ang_raw > 180) base_ang_raw -= 180;

            // EL TRUCO DE PRECISIÓN: Invertimos la distancia horizontal.
            // Esto le dice a las matemáticas del hombro: "El objetivo está detrás de ti".
            hDist = -rad; 
        }

        float base_ang = Mathf.Clamp(base_ang_raw, 0f, 180f);


        // 2. CÁLCULO TRIGONOMÉTRICO (Usando hDist que puede ser negativo)
        float wy = y - BASE_HGT;
        float sw = hDist*hDist + wy*wy; // Pitágoras funciona igual con negativo
        float sw_sqrt = Mathf.Sqrt(sw);

        // -- Codo --
        float cElb = (hum_sq + uln_sq - sw) / (2 * HUMERUS * ULNA);
        float elb = offsetCodo - Mathf.Acos(Mathf.Clamp(cElb, -1, 1)) * Mathf.Rad2Deg;

        // -- Hombro --
        float a1 = Mathf.Atan2(wy, hDist); // Aquí 'hDist' negativo hace la magia
        float cShl = (hum_sq + sw - uln_sq) / (2 * HUMERUS * sw_sqrt);
        
        float shl = offsetHombro - (a1 + Mathf.Acos(Mathf.Clamp(cShl, -1, 1))) * Mathf.Rad2Deg;

        // 3. Muñeca
        if (endHorizontal) {
           float wr_ang = 90f + (thetaShoulder - 90f) + (thetaElbow - 90f); 
           thetaWristVertical = Mathf.Clamp(wr_ang, 0, 180);
        }

        if(!float.IsNaN(base_ang)) thetaBase = base_ang;
        if(!float.IsNaN(shl)) thetaShoulder = shl; 
        if(!float.IsNaN(elb)) thetaElbow = elb;     
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
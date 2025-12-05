using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections;

public class SolveIK_Network : MonoBehaviour {

    [Header("--- Configuración de Red ---")]
    public string urlServidor = "http://127.0.0.1:5000/api/braccio";
    [Range(0.05f, 1f)] public float intervaloEnvio = 0.1f;

    [Header("--- Configuración IK ---")]
    public bool useIK = true;
    public bool autoEnd = true;
    
    [Tooltip("El objeto 'Target' que mueves (Debe estar FUERA de la jerarquía del brazo)")]
    public Transform targetObj; 

    [Header("--- UI Debug ---")]
    public TMP_Text textoAngulos;

    [Header("--- Control Manual / Gestos ---")]
    public bool isPinching = false;

    [Header("--- Calibración y Ajustes ---")]
    [Tooltip("Ajusta si el hombro apunta mal (Default ~180)")]
    public float offsetHombro = 180f; 
    [Tooltip("Ajusta si el codo no estira bien (Default ~270)")]
    public float offsetCodo = 270f;
    [Range(1f, 20f)] public float velocidadSuavizado = 10f; 
    public float alturaMesa = 0.02f; // Evita que atraviese el suelo

    [Header("Ángulos Calculados (Salida)")]
    [Range(0.0f, 180.0f)] public float thetaBase = 90f;
    [Range(0.0f, 180.0f)] public float thetaShoulder = 45f;
    [Range(0.0f, 180.0f)] public float thetaElbow = 180f;
    [Range(0.0f, 180.0f)] public float thetaWristVertical = 90f;
    [Range(0.0f, 180.0f)] public float thetaWristRotation = 0f;
    [Range(10.0f, 73.0f)] public float thetaGripper = 10f;

    [Header("Referencias del Brazo")]
    public GameObject[] arms = new GameObject[5]; 

    // Dimensiones internas
    private float BASE_HGT, HUMERUS, ULNA;
    public float GRIPPER = 0.098f; 
    private float hum_sq, uln_sq;
    private float timer = 0f;
    private int _estadoPinza = 1;

    // Estructura JSON
    [Serializable]
    private struct BraccioData {
        public long timestamp;
        public int m1, m2, m3, m4, m5, m6, apertura_pinza;
    }

    void Start () {
        if (targetObj == null) Debug.LogWarning("¡Asigna el Target Obj!");

        // Auto-Calcular longitudes
        if(arms[0] != null && arms[1] != null && arms[2] != null && arms[3] != null) {
            BASE_HGT = Vector3.Distance(arms[0].transform.position, arms[1].transform.position);
            HUMERUS = Vector3.Distance(arms[1].transform.position, arms[2].transform.position);
            ULNA = Vector3.Distance(arms[2].transform.position, arms[3].transform.position);
            hum_sq = HUMERUS*HUMERUS;
            uln_sq = ULNA*ULNA;
        } else {
            Debug.LogError("¡Faltan referencias en el array 'arms'!");
        }
    }

    void Update () {
        if (targetObj == null || arms[0] == null) return;
        
        // --- 2. BLOQUEO DE PISO (MESA) ---
        // Mantiene la esfera por encima de la mesa
        if (targetObj.position.y < alturaMesa) {
            targetObj.position = new Vector3(targetObj.position.x, alturaMesa, targetObj.position.z);
        }

        // --- 3. CÁLCULO IK ---
        if (useIK) {
            Vector3 targetPos = targetObj.position;
            Vector3 localTarget;

            if (arms[0].transform.parent != null) 
                localTarget = arms[0].transform.parent.InverseTransformPoint(targetPos);
            else 
                localTarget = targetPos - arms[0].transform.position;

            Vector3 directionToTarget = localTarget.normalized;
            if (directionToTarget == Vector3.zero) directionToTarget = Vector3.up;

            Vector3 wristTarget = localTarget - (directionToTarget * GRIPPER);

            // Liberar muñeca si apunta muy arriba
            bool forzarHorizontal = autoEnd;
            if (directionToTarget.y > 0.6f) forzarHorizontal = false;

            SetArm(wristTarget.x, wristTarget.y, wristTarget.z, forzarHorizontal);
            
            if (!forzarHorizontal) thetaWristVertical = 90f; 
        }

        // --- 4. CONTROL PINZA ---
        if (isPinching) { thetaGripper = 10f; _estadoPinza = 0; }
        else { thetaGripper = 73f; _estadoPinza = 1; }

        // --- 5. VISUALIZACIÓN SUAVIZADA ---
        Quaternion rotBase = Quaternion.Euler(0f, thetaBase, 0f);
        arms[0].transform.localRotation = Quaternion.Slerp(arms[0].transform.localRotation, rotBase, Time.deltaTime * velocidadSuavizado);

        Quaternion rotShoulder = Quaternion.Euler(0f, 0f, thetaShoulder - 90f); 
        arms[1].transform.localRotation = Quaternion.Slerp(arms[1].transform.localRotation, rotShoulder, Time.deltaTime * velocidadSuavizado);

        Quaternion rotElbow = Quaternion.Euler(0f, 0f, thetaElbow - 90f); 
        arms[2].transform.localRotation = Quaternion.Slerp(arms[2].transform.localRotation, rotElbow, Time.deltaTime * velocidadSuavizado);

        Quaternion rotWrist = Quaternion.Euler(0f, 0f, thetaWristVertical - 90f); 
        arms[3].transform.localRotation = Quaternion.Slerp(arms[3].transform.localRotation, rotWrist, Time.deltaTime * velocidadSuavizado);

        Quaternion rotWristRot = Quaternion.Euler(0f, thetaWristRotation, 0f);
        arms[4].transform.localRotation = Quaternion.Slerp(arms[4].transform.localRotation, rotWristRot, Time.deltaTime * velocidadSuavizado);

        // --- 6. TEXTO UI ---
        if (textoAngulos != null) {
            textoAngulos.text = $"M1:{(int)thetaBase} M2:{(int)thetaShoulder} M3:{(int)thetaElbow} M4:{(int)thetaWristVertical} M5:{(int)thetaWristRotation}";
        }

        // --- 7. RED ---
        timer += Time.deltaTime;
        if (timer >= intervaloEnvio) {
            StartCoroutine(EnviarDatosPOST());
            timer = 0f;
        }
    }

    // --- LÓGICA MATEMÁTICA IK (360 GRADOS) ---
    void SetArm(float x, float y, float z, bool endHorizontal) {
        
        // --- ZONA MUERTA ---
        float radioHorizontal = Mathf.Sqrt(x * x + z * z);
        if (radioHorizontal < 0.05f) return; 

        // Base
        float bas_angle_r = Mathf.Atan2( x, z );
        float bas_angle_d = bas_angle_r * Mathf.Rad2Deg + 90f;
        float horizontalDist = radioHorizontal;

        // FLIP 360
        if (bas_angle_d > 180f) {
            bas_angle_d -= 180f;
            horizontalDist *= -1f;
        } else if (bas_angle_d < 0f) {
            bas_angle_d += 180f;
            horizontalDist *= -1f;
        }

        // Triángulo
        float wrt_y = y - BASE_HGT; 
        float s_w = horizontalDist * horizontalDist + wrt_y * wrt_y; 
        float s_w_sqrt = Mathf.Sqrt (s_w);

        // Codo
        float cosAngleElbow = (hum_sq + uln_sq - s_w) / (2f * HUMERUS * ULNA);
        if (s_w_sqrt > (HUMERUS + ULNA)) cosAngleElbow = -1f; 
        float elb_angle_r = Mathf.Acos (Mathf.Clamp(cosAngleElbow, -1f, 1f));
        float elb_angle_d = offsetCodo - elb_angle_r * Mathf.Rad2Deg; 

        // Hombro
        float a1 = Mathf.Atan2 (wrt_y, horizontalDist); 
        float cosAngleShoulder = (hum_sq + s_w - uln_sq) / (2f * HUMERUS * s_w_sqrt);
        float a2 = Mathf.Acos (Mathf.Clamp(cosAngleShoulder, -1f, 1f));
        float shl_angle_r = a1 + a2;
        float shl_angle_d = offsetHombro - shl_angle_r * Mathf.Rad2Deg; 

        // Muñeca
        if (endHorizontal) {
            float end_x = arms [4].transform.position.x;
            float end_y = arms [4].transform.position.y;
            float end_z = arms [4].transform.position.z;
            float end_last_angle = thetaWristVertical; 
            float dx = end_x - x;
            float dz = end_z - z;
            float wrt_angle_r = Mathf.Atan2 (end_y - y, Mathf.Sqrt (dx * dx + dz * dz));
            float wrt_angle_d = end_last_angle + wrt_angle_r * Mathf.Rad2Deg; 
            if (wrt_angle_d >= 0f && wrt_angle_d <= 180f) thetaWristVertical = wrt_angle_d;
        }

        // Aplicar
        if (!float.IsNaN(bas_angle_d)) thetaBase = Mathf.Clamp(bas_angle_d, 0, 180);
        if (!float.IsNaN(shl_angle_d)) thetaShoulder = Mathf.Clamp(shl_angle_d, 0, 180); 
        if (!float.IsNaN(elb_angle_d)) thetaElbow = Mathf.Clamp(elb_angle_d, 0, 180);
    }

    IEnumerator EnviarDatosPOST() {
        BraccioData data = new BraccioData();
        data.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        data.m1 = (int)thetaBase; data.m2 = (int)thetaShoulder; data.m3 = (int)thetaElbow;
        data.m4 = (int)thetaWristVertical; data.m5 = (int)thetaWristRotation; data.m6 = (int)thetaGripper;
        data.apertura_pinza = _estadoPinza;
        string jsonData = JsonUtility.ToJson(data);
        using (UnityWebRequest request = new UnityWebRequest(urlServidor, "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();  
        }
    }
}
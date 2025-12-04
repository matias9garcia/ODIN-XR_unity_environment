using UnityEngine;
using UnityEngine.Networking; // Necesario para HTTP
using TMPro;                  // Necesario para TextMeshPro
using System;
using System.Collections;     // Necesario para Corutinas

public class SolveIK_Network : MonoBehaviour {

    [Header("--- Configuración de Red ---")]
    public string urlServidor = "http://127.0.0.1:5000/api/braccio";
    [Range(0.05f, 1f)] public float intervaloEnvio = 0.1f; // Frecuencia de envío (segundos)

    [Header("--- Configuración IK ---")]
    public bool useIK = true;
    public bool autoEnd = true;
    
    // El objeto fantasma o target que movemos en Unity
    [Tooltip("Arrastra aquí la Esfera que quieres que el robot siga")]
    public Transform targetObj; 

    [Header("--- UI Debug ---")]
    [Tooltip("Arrastra aquí tu objeto de TextMeshPro (UI o World)")]
    public TMP_Text textoAngulos; // Funciona tanto para TMP UI como para TMP 3D

    [Header("--- Control Manual / Gestos ---")]
    public bool isPinching = false; // ¿Está haciendo el gesto de pellizco?

    [Header("Ángulos Calculados (Salida)")]
    [Range(0.0f, 180.0f)] public float thetaBase = 90f;
    [Range(15.0f, 165.0f)] public float thetaShoulder = 45f;
    [Range(0.0f, 180.0f)] public float thetaElbow = 180f;
    [Range(0.0f, 180.0f)] public float thetaWristVertical = 90f;
    [Range(0.0f, 180.0f)] public float thetaWristRotation = 0f;  // M5
    [Range(10.0f, 73.0f)] public float thetaGripper = 10f;       // M6

    [Header("Referencias del Brazo")]
    public GameObject[] arms = new GameObject[5]; // Base, Hombro, Codo, Muñeca, Rotador

    [Header("Dimensiones (Auto-Calculadas)")]
    public float BASE_HGT; 
    public float HUMERUS;
    public float ULNA;
    public float GRIPPER = 0.098f; 

    // Variables internas
    private float hum_sq;
    private float uln_sq;
    private float timer = 0f;
    private int _estadoPinza = 1; // 1 = Abierto, 0 = Cerrado (Para lógica de servidor)

    // --- ESTRUCTURA JSON PARA EL SERVIDOR ---
    [Serializable]
    private struct BraccioData
    {
        public long timestamp;
        public int m1; // Base
        public int m2; // Hombro
        public int m3; // Codo
        public int m4; // Muñeca Ver
        public int m5; // Muñeca Rot
        public int m6; // Gripper
        public int apertura_pinza; // 0 o 1
    }

    void Start () {
        // --- 0. SEGURIDAD: Intentar encontrar target si está vacío ---
        if (targetObj == null)
        {
            var posibleTarget = GameObject.Find("Target");
            if (posibleTarget == null) posibleTarget = GameObject.Find("Sphere");
            
            if (posibleTarget != null) {
                targetObj = posibleTarget.transform;
                Debug.Log("Auto-asignado Target: " + targetObj.name);
            } else {
                Debug.LogWarning("NO HAY TARGET ASIGNADO. El brazo no se moverá hacia la esfera. Arrastra la esfera a la variable 'Target Obj'.");
            }
        }

        // --- CALIBRACIÓN AUTOMÁTICA (Del Primer Código) ---
        if(arms[0] != null && arms[1] != null && arms[2] != null && arms[3] != null)
        {
            BASE_HGT = Vector3.Distance(arms[0].transform.position, arms[1].transform.position);
            HUMERUS = Vector3.Distance(arms[1].transform.position, arms[2].transform.position);
            ULNA = Vector3.Distance(arms[2].transform.position, arms[3].transform.position);
            
            hum_sq = HUMERUS*HUMERUS;
            uln_sq = ULNA*ULNA;
            
            Debug.Log($"Medidas: Base={BASE_HGT}, Humero={HUMERUS}, Ulna={ULNA}");
        }
        else
        {
            Debug.LogError("¡Faltan referencias en el array 'arms'!");
        }
    }

    void Update () {
        // 1. OBTENER TARGET
        Vector3 targetPosition = (targetObj != null) ? targetObj.position : arms[4].transform.position;

        // 2. CÁLCULO DE CINEMÁTICA INVERSA
        if (useIK && arms[0] != null) {
            Vector3 localTarget;

            if (arms[0].transform.parent != null) {
                localTarget = arms[0].transform.parent.InverseTransformPoint(targetPosition);
            } 
            else {
                localTarget = targetPosition - arms[0].transform.position;
            }

            Vector3 directionToTarget = new Vector3(localTarget.x, 0, localTarget.z).normalized;
            if (directionToTarget == Vector3.zero) directionToTarget = Vector3.forward; 

            Vector3 wristTarget = localTarget - (directionToTarget * GRIPPER);

            SetArm (wristTarget.x, wristTarget.y, wristTarget.z, autoEnd);
        }

        // 3. LÓGICA DE PELLIZCO (PINCHING)
        if (isPinching)
        {
            // Si pellizca -> CERRADO
            thetaGripper = 10f;    // Grados físicos (cerrado)
            _estadoPinza = 0;      // Lógica (0 = cerrado)
        }
        else
        {
            // Si no pellizca -> ABIERTO
            thetaGripper = 73f;    // Grados físicos (abierto)
            _estadoPinza = 1;      // Lógica (1 = abierto)
        }

        // 4. ACTUALIZAR VISUALIZACIÓN EN UNITY
        if (arms[0] != null) {
            arms [0].transform.localRotation = Quaternion.Euler(new Vector3 (0f, thetaBase, 0f));
            arms [1].transform.localRotation = Quaternion.Euler(new Vector3 (0f, 0f, thetaShoulder - 90f));
            arms [2].transform.localRotation = Quaternion.Euler(new Vector3 (0f, 0f, thetaElbow - 90f));
            arms [3].transform.localRotation = Quaternion.Euler(new Vector3 (0f, 0f, thetaWristVertical - 90f));
            arms [4].transform.localRotation = Quaternion.Euler(new Vector3 (0f, thetaWristRotation, 0f));
        }

        // 5. ACTUALIZAR UI TEXTMESHPRO (NUEVO)
        if (textoAngulos != null)
        {
            string estadoPinzaTexto = (_estadoPinza == 1) ? "ABIERTA" : "CERRADA";
            textoAngulos.text = $"<b>ESTADO DEL BRAZO</b>\n" +
                                $"----------------\n" +
                                $"M1 (Base):   {(int)thetaBase}°\n" +
                                $"M2 (Hombro): {(int)thetaShoulder}°\n" +
                                $"M3 (Codo):   {(int)thetaElbow}°\n" +
                                $"M4 (Muñeca): {(int)thetaWristVertical}°\n" +
                                $"M5 (Rot):    {(int)thetaWristRotation}°\n" +
                                $"M6 (Pinza):  {(int)thetaGripper}° ({estadoPinzaTexto})";
        }

        // 6. ENVIAR DATOS A LA RED
        timer += Time.deltaTime;
        if (timer >= intervaloEnvio)
        {
            StartCoroutine(EnviarDatosPOST());
            timer = 0f;
        }
    }

    // --- AYUDA VISUAL EN EL EDITOR ---
    void OnDrawGizmos() {
        if (arms[0] != null && targetObj != null) {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(arms[0].transform.position, targetObj.position);
            Gizmos.DrawWireSphere(targetObj.position, 0.05f);
        }
    }

    // --- FUNCIÓN MATEMÁTICA IK ---
    void SetArm(float x, float y, float z, bool endHorizontal) {
        float bas_angle_r = Mathf.Atan2( x, z );
        float bas_angle_d = bas_angle_r * Mathf.Rad2Deg + 90f;

        float wrt_y = y - BASE_HGT; 
        float s_w = x * x + z * z + wrt_y * wrt_y; 
        float s_w_sqrt = Mathf.Sqrt (s_w);

        float cosAngleElbow = (hum_sq + uln_sq - s_w) / (2f * HUMERUS * ULNA);
        float elb_angle_r = Mathf.Acos (Mathf.Clamp(cosAngleElbow, -1f, 1f));
        float elb_angle_d = 270f - elb_angle_r * Mathf.Rad2Deg;

        float a1 = Mathf.Atan2 (wrt_y, Mathf.Sqrt (x * x + z * z));
        float cosAngleShoulder = (hum_sq + s_w - uln_sq) / (2f * HUMERUS * s_w_sqrt);
        float a2 = Mathf.Acos (Mathf.Clamp(cosAngleShoulder, -1f, 1f));
        float shl_angle_r = a1 + a2;
        float shl_angle_d = 180f - shl_angle_r * Mathf.Rad2Deg;

        if (endHorizontal) {
            float end_x = arms [4].transform.position.x;
            float end_y = arms [4].transform.position.y;
            float end_z = arms [4].transform.position.z;

            float end_last_angle = thetaWristVertical; 
            float dx = end_x - x;
            float dz = end_z - z;
            float wrt_angle_r = Mathf.Atan2 (end_y - y, Mathf.Sqrt (dx * dx + dz * dz));
            float wrt_angle_d = end_last_angle + wrt_angle_r * Mathf.Rad2Deg; 
            
            if (wrt_angle_d >= 0f && wrt_angle_d <= 180f)
                thetaWristVertical = wrt_angle_d;
        }

        if (!float.IsNaN(bas_angle_d)) thetaBase = Mathf.Clamp(bas_angle_d, 0, 180);
        if (!float.IsNaN(shl_angle_d)) thetaShoulder = Mathf.Clamp(shl_angle_d, 15, 165);
        if (!float.IsNaN(elb_angle_d)) thetaElbow = Mathf.Clamp(elb_angle_d, 0, 180);
    }

    // --- COROUTINE DE RED ---
    IEnumerator EnviarDatosPOST()
    {
        BraccioData data = new BraccioData();
        data.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        data.m1 = (int)thetaBase;
        data.m2 = (int)thetaShoulder;
        data.m3 = (int)thetaElbow;
        data.m4 = (int)thetaWristVertical;
        data.m5 = (int)thetaWristRotation; 
        data.m6 = (int)thetaGripper;
        data.apertura_pinza = _estadoPinza;

        string jsonData = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(urlServidor, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
        }
    }
}
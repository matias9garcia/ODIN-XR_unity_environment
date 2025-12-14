using UnityEngine;
using System.Collections;

public class SolveIK : MonoBehaviour {

    public bool useIK = true;
    public bool autoEnd = true;

    public Vector3 targetPosition;
    public Vector3 currentPosition;
    
    [Header("Joint Angles")]
    [Range(0.0f, 180.0f)] public float thetaBase = 90f;
    [Range(15.0f, 165.0f)] public float thetaShoulder = 45f;
    [Range(0.0f, 180.0f)] public float thetaElbow = 180f;
    [Range(0.0f, 180.0f)] public float thetaWristVertical = 90f;
    [Range(0.0f, 180.0f)] public float thetaWristRotation = 0f;
    [Range(10.0f, 73.0f)] public float thetaGripper = 10f;

    [Header("References")]
    public GameObject[] arms = new GameObject[5];

    [Header("Arm Dimensions (Auto-calculated)")]
    // Hacemos publicas estas variables para verlas en el inspector
    public float BASE_HGT; 
    public float HUMERUS;
    public float ULNA;
    // El gripper es difícil de calcular auto, mejor dejarlo manual o ajustarlo visualmente
    public float GRIPPER = 0.098f; 

    /* pre-calculations */
    float hum_sq;
    float uln_sq;

    void Start () {
        // --- CALIBRACIÓN AUTOMÁTICA ---
        // Calcula las distancias basándose en dónde pusiste los Pivotes en Unity
        
        // Distancia Y del pivot del hombro (Arms[1]) respecto al pivot base (Arms[0])
        BASE_HGT = Vector3.Distance(arms[0].transform.position, arms[1].transform.position);
        
        // Longitud del brazo superior: distancia entre hombro (Arms[1]) y codo (Arms[2])
        HUMERUS = Vector3.Distance(arms[1].transform.position, arms[2].transform.position);
        
        // Longitud del antebrazo: distancia entre codo (Arms[2]) y muñeca (Arms[3])
        ULNA = Vector3.Distance(arms[2].transform.position, arms[3].transform.position);

        Debug.Log("Medidas detectadas: Base=" + BASE_HGT + " Humero=" + HUMERUS + " Ulna=" + ULNA);

        // --- AQUÍ FORZAMOS TU CAMBIO ---
        GRIPPER = 0.098f;

        /* pre-calculations */
        hum_sq = HUMERUS*HUMERUS;
        uln_sq = ULNA*ULNA;
    }

    void Update () {
        targetPosition = transform.position;

        if (useIK) {
            Vector3 localTarget;

            // 1. Calcular posición local (como hicimos antes para evitar vibración)
            if (arms[0].transform.parent != null) {
                localTarget = arms[0].transform.parent.InverseTransformPoint(targetPosition);
            } 
            else {
                localTarget = targetPosition - arms[0].transform.position;
            }

            // --- CORRECCIÓN NUEVA: COMPENSACIÓN DEL GRIPPER ---
            // Queremos que la PUNTA llegue al target, no la muñeca.
            // Calculamos la dirección horizontal hacia el objetivo
            Vector3 directionToTarget = new Vector3(localTarget.x, 0, localTarget.z).normalized;

            // "Retrocedemos" el objetivo la distancia que mide la mano (GRIPPER)
            // Si autoEnd está activado (mano horizontal), esto funciona perfecto.
            // Si la mano no está horizontal, el cálculo es más complejo, pero esto suele bastar.
            Vector3 wristTarget = localTarget - (directionToTarget * GRIPPER);

            // Pasamos al solucionador la posición ajustada de la muñeca
            SetArm (wristTarget.x, wristTarget.y, wristTarget.z, autoEnd);
        }

        // Resto de rotaciones (Igual que antes)
        arms [0].transform.localRotation = Quaternion.Euler(new Vector3 (0f, thetaBase, 0f));
        arms [1].transform.localRotation = Quaternion.Euler(new Vector3 (0f, 0f, thetaShoulder - 90f));
        arms [2].transform.localRotation = Quaternion.Euler(new Vector3 (0f, 0f, thetaElbow - 90f));
        arms [3].transform.localRotation = Quaternion.Euler(new Vector3 (0f, 0f, thetaWristVertical - 90f));
        arms [4].transform.localRotation = Quaternion.Euler(new Vector3 (0f, thetaWristRotation, 0f));
    }

    void SetArm(float x, float y, float z, bool endHorizontal) {
        // Base angle
        float bas_angle_r = Mathf.Atan2( x, z );
        float bas_angle_d = bas_angle_r * Mathf.Rad2Deg + 90f;

        float wrt_y = y - BASE_HGT; // Wrist relative height to shoulder
        float s_w = x * x + z * z + wrt_y * wrt_y; // Shoulder to wrist distance square
        float s_w_sqrt = Mathf.Sqrt (s_w);

        // VERIFICACIÓN DE ALCANCE: Si el objetivo está muy lejos, limitarlo para evitar errores NaN
        if (s_w_sqrt > (HUMERUS + ULNA)) {
             // Opcional: Podrías poner un debug aquí si quieres saber cuando no llega
             // Debug.LogWarning("Objetivo fuera de alcance");
        }

        // Elbow angle: knowing 3 edges of the triangle, get the angle
        // Mathf.Clamp asegura que el valor esté entre -1 y 1 para que Acos no falle
        float cosAngleElbow = (hum_sq + uln_sq - s_w) / (2f * HUMERUS * ULNA);
        float elb_angle_r = Mathf.Acos (Mathf.Clamp(cosAngleElbow, -1f, 1f));
        float elb_angle_d = 270f - elb_angle_r * Mathf.Rad2Deg;

        // Shoulder angle = a1 + a2
        float a1 = Mathf.Atan2 (wrt_y, Mathf.Sqrt (x * x + z * z));
        
        float cosAngleShoulder = (hum_sq + s_w - uln_sq) / (2f * HUMERUS * s_w_sqrt);
        float a2 = Mathf.Acos (Mathf.Clamp(cosAngleShoulder, -1f, 1f));
        
        float shl_angle_r = a1 + a2;
        float shl_angle_d = 180f - shl_angle_r * Mathf.Rad2Deg;

        // Keep end point horizontal
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

        // Update angles
        if (bas_angle_d >= 0f && bas_angle_d <=180f) thetaBase = bas_angle_d;
        if (shl_angle_d >= 15f && shl_angle_d <=165f) thetaShoulder = shl_angle_d;
        if (elb_angle_d >= 0f && elb_angle_d <=180f) thetaElbow = elb_angle_d;
    }
}
using UnityEngine;

public class BraccioIK : MonoBehaviour {

    [Header("--- Configuración General ---")]
    public bool autoEnd = true; // Define si la muñeca intenta mantenerse horizontal
    
    [Header("--- Configuración Física del Robot ---")]
    // Referencias necesarias SOLO para calcular las longitudes de los segmentos al inicio
    public Transform baseTransform;   // Asigna arms[0] o la base padre
    public Transform shoulderTransform; // arms[1]
    public Transform elbowTransform;    // arms[2]
    public Transform wristTransform;    // arms[3]

    [Header("--- Configuración Pinza (Gripper) ---")]
    public float longitudPinza = 0.10f;
    [Range(10, 73)] public float anguloAbierto = 73f;
    [Range(10, 73)] public float anguloCerrado = 10f;
    [Tooltip("0 = auto (usa la escala del padre). >0 = escala manual del brazo (uniforme).")]
    public float armScale = 0f;

    // --- SALIDAS (Propiedades de solo lectura para otros scripts) ---
    // Otros scripts leerán esto: miScriptIK.ThetaBase
    public float ThetaBase { get; private set; } = 90f;
    public float ThetaShoulder { get; private set; } = 90f;
    public float ThetaElbow { get; private set; } = 90f;
    public float ThetaWristVertical { get; private set; } = 90f;
    public float ThetaWristRotation { get; private set; } = 90f;
    public float ThetaGripper { get; private set; } = 10f;
    
    // Estado lógico para la API (1 = abierto, 0 = cerrado, según tu lógica original)
    public int EstadoPinzaLogico { get; private set; } = 1;

    // Variables Matemáticas Internas
    private float BASE_HGT, HUMERUS, ULNA, hum_sq, uln_sq;

    void Start() {
        CalibrarLongitudes();
        // Inicializar pinza abierta visualmente
        ThetaGripper = anguloAbierto;
    }

    private void CalibrarLongitudes() {
        if (baseTransform != null && wristTransform != null) {
            // Trabajar en el mismo espacio que usa el solver (espacio del padre de la base)
            Transform space = baseTransform.parent;

            Vector3 baseP = (space != null) ? space.InverseTransformPoint(baseTransform.position) : baseTransform.position;
            Vector3 shoulderP = (space != null) ? space.InverseTransformPoint(shoulderTransform.position) : shoulderTransform.position;
            Vector3 elbowP = (space != null) ? space.InverseTransformPoint(elbowTransform.position) : elbowTransform.position;
            Vector3 wristP = (space != null) ? space.InverseTransformPoint(wristTransform.position) : wristTransform.position;

            // Altura base -> hombro tomada en el eje Y del espacio de referencia
            BASE_HGT = shoulderP.y - baseP.y;
            HUMERUS = Vector3.Distance(shoulderP, elbowP);
            ULNA = Vector3.Distance(elbowP, wristP);

            // Protección mínima para evitar división por cero
            if (HUMERUS < 0.001f) HUMERUS = 0.125f;
            if (ULNA < 0.001f) ULNA = 0.125f;

            hum_sq = HUMERUS * HUMERUS;
            uln_sq = ULNA * ULNA;
            
            Debug.Log($"[BraccioIK] Calibrado: Humero={HUMERUS}, Ulna={ULNA}");
        } else {
            Debug.LogError("[BraccioIK] Faltan referencias a los Transforms para calibrar.");
        }
    }

    /// <summary>
    /// Función principal. Llama a esto desde tu Controller pasándole el Target y si está pellizcando.
    /// </summary>
    public void CalcularIK(Vector3 targetGlobalPosition, bool isPinching, Quaternion targetRotation) {
        if (baseTransform == null) return;

        // 1. Convertir la posición global del Target a local respecto a la base del robot
        // Esto es crucial para que el robot funcione aunque lo rotes o muevas en la escena.
        Vector3 localTarget = (baseTransform.parent != null) ? 
            baseTransform.parent.InverseTransformPoint(targetGlobalPosition) : 
            targetGlobalPosition - baseTransform.position;

        // 2. Compensación de la longitud de la pinza
        Vector3 directionToTarget = new Vector3(localTarget.x, 0, localTarget.z).normalized;
        if (directionToTarget == Vector3.zero) directionToTarget = Vector3.forward;

        // Convertir longitud de pinza (en unidades de mundo) al espacio local usado por el solver
        float scaleFactor = ObtenerEscalaReferenciaXZ();
        if (armScale > 0f) scaleFactor = armScale; // Permite override manual
        float longitudPinzaLocal = longitudPinza / Mathf.Max(scaleFactor, 1e-4f);

        Vector3 wristTarget = localTarget - (directionToTarget * longitudPinzaLocal);

        // Extraer rotación Y del target (yaw) para la muñeca
        float targetYaw = targetRotation.eulerAngles.y;

        // 3. Ejecutar la matemática pura
        ResolverMatematica360(wristTarget.x, wristTarget.y, wristTarget.z, autoEnd, targetYaw);

        // 4. Gestionar ángulo de la pinza
        if (isPinching) {
            ThetaGripper = anguloCerrado;
            EstadoPinzaLogico = 0; // Cerrado
        } else {
            ThetaGripper = anguloAbierto;
            EstadoPinzaLogico = 1; // Abierto
        }
    }

    // Lógica matemática interna (Tu SetArm_360Logic original, adaptada)
    private void ResolverMatematica360(float x, float y, float z, bool endHorizontal, float targetYaw) {
        // A. Base
        float bas360 = Mathf.Atan2(x, z) * Mathf.Rad2Deg + 90f;
        if (bas360 < 0f) bas360 += 360f;

        bool isBackwards = bas360 > 180f;
        float bas180 = isBackwards ? bas360 - 180f : bas360;
        ThetaBase = Mathf.Clamp(bas180, 0f, 180f);

        // B. Preparar geometría plana
        float effX = isBackwards ? -x : x;
        float effZ = isBackwards ? -z : z;

        float wrt_y = y - BASE_HGT;
        float planar = Mathf.Sqrt(effX * effX + effZ * effZ);
        float s_w = effX * effX + effZ * effZ + wrt_y * wrt_y;
        float s_w_sqrt = Mathf.Max(Mathf.Sqrt(s_w), 1e-4f);

        // C. Codo (M3)
        float cosAngleElbow = (hum_sq + uln_sq - s_w) / (2f * HUMERUS * ULNA);
        cosAngleElbow = Mathf.Clamp(cosAngleElbow, -1f, 1f);
        float elb_angle_d = 270f - Mathf.Acos(cosAngleElbow) * Mathf.Rad2Deg;

        // D. Hombro (M2)
        float a1 = Mathf.Atan2(wrt_y, planar);
        float cosAngleShoulder = (hum_sq + s_w - uln_sq) / (2f * HUMERUS * s_w_sqrt);
        cosAngleShoulder = Mathf.Clamp(cosAngleShoulder, -1f, 1f);
        float a2 = Mathf.Acos(cosAngleShoulder);
        float shl_angle_d = 180f - (a1 + a2) * Mathf.Rad2Deg;

        float finalShoulder = isBackwards ? 180f - shl_angle_d : shl_angle_d;
        if (!float.IsNaN(finalShoulder))
            ThetaShoulder = Mathf.Clamp(finalShoulder, 0f, 180f);

        // Asignar Codo
        if (!float.IsNaN(elb_angle_d)) {
            float elbowForServo = isBackwards ? 180f - elb_angle_d : elb_angle_d;
            ThetaElbow = Mathf.Clamp(elbowForServo, 0f, 180f);
        }

        // E. Muñeca Vertical (M4)
        if (endHorizontal) {
            float wr_ang = 90f + (shl_angle_d - 90f) + (elb_angle_d - 90f);
            if (isBackwards) wr_ang = 180f - wr_ang;
            ThetaWristVertical = Mathf.Clamp(wr_ang, 0f, 180f);
        } else {
            ThetaWristVertical = 90f;
        }
        
        // M5 (Rotación muñeca) - Basada en la rotación Y del target
        ThetaWristRotation = Mathf.Clamp(targetYaw, 0f, 180f); 
    }

    // Escala efectiva usada para convertir magnitudes horizontales (X/Z) de mundo a espacio local de referencia
    private float ObtenerEscalaReferenciaXZ() {
        Transform space = (baseTransform != null) ? baseTransform.parent : null;
        if (space == null) return 1f;
        Vector3 s = space.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(s.x), 1e-4f);
        float sz = Mathf.Max(Mathf.Abs(s.z), 1e-4f);
        return 0.5f * (sx + sz);
    }
}
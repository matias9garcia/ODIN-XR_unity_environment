using UnityEngine;

public class ForceGrab : MonoBehaviour
{
    [Header("1. Conexión con el Robot")]
    [Tooltip("Arrastra aquí el script BraccioController que orquesta el brazo")]
    public BraccioController braccioController; 

    [Header("2. Referencia de la Esfera")]
    [Tooltip("Arrastra aquí tu 'IKControl' o la Esfera Roja que el robot sigue")]
    public Transform esferaIK; 

    [Header("3. Límite de Movimiento (Radial)")]
    [Tooltip("Arrastra aquí la BASE del robot (Base_Pivot) para usarla como centro")]
    public Transform pivoteCentral; 
    [Tooltip("Distancia máxima que la esfera puede alejarse de la base")]
    public float radioMaximo = 0.8f; 
    [Tooltip("Si es TRUE, limita como un cilindro (altura libre). Si es FALSE, limita como una burbuja.")]
    public bool limitarSoloHorizontal = true; 

    [Header("Configuración de Mano VR")]
    public OVRHand hand;
    
    [Header("Configuración de Fuerza")]
    public float velocidadAtraccion = 5.0f;
    public float distanciaMinima = 0.1f; 

    [Header("Configuración de Gestos")]
    [Range(0.1f, 1.0f)]
    public float umbralPuño = 0.4f;    
    
    [Header("Debug")]
    public bool mostrarValores = true;
    
    private bool isGrabbingLastFrame = false;   
    private bool wasPinchingLastFrame = false; 

    void Start()
    {
        // Inicializar estado
    }

    void Update()
    {
        if (hand != null && hand.IsTracked)
        {
            bool isFist = CheckIfFist();

            if (isFist)
            {
                AtraerEsfera(); 
            }

            if (!isFist) 
            {
                CheckPinchToggle();
            }
        }
    }

    bool CheckIfFist()
    {
        float index = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        float middle = hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        float ring = hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
        bool isFist = (index > umbralPuño) && (middle > umbralPuño) && (ring > umbralPuño);
        
        if (mostrarValores && isFist) Debug.Log($"Fuerza detectada! Trayendo robot...");
        return isFist;
    }

    void AtraerEsfera()
    {
        if (esferaIK == null) return;
        
        Vector3 targetPos = Vector3.MoveTowards(esferaIK.position, hand.transform.position, velocidadAtraccion * Time.deltaTime);

        if (pivoteCentral != null)
        {
            Vector3 offset = targetPos - pivoteCentral.position;

            if (limitarSoloHorizontal)
            {
                float currentY = offset.y;
                offset.y = 0; 
                offset = Vector3.ClampMagnitude(offset, radioMaximo); 
                offset.y = currentY; 
            }
            else
            {
                offset = Vector3.ClampMagnitude(offset, radioMaximo);
            }
            targetPos = pivoteCentral.position + offset;
        }
        esferaIK.position = targetPos;
    }

    void CheckPinchToggle()
    {
        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (isPinching && !wasPinchingLastFrame)
        {
            Debug.Log("Click! Pinza: " + (isPinching ? "CERRANDO" : "ABRIENDO"));

            if (braccioController != null)
            {
                braccioController.isPinching = !braccioController.isPinching;
            }
        }
        wasPinchingLastFrame = isPinching;
    }

    // ---------------------------------------------------------
    // --- NUEVO: VISUALIZACIÓN DE GIZMOS (EL DIBUJO) ---
    // ---------------------------------------------------------
    void OnDrawGizmos()
    {
        // Solo dibujamos si hay un centro asignado
        if (pivoteCentral == null) return;

        Gizmos.color = Color.yellow; // Color del límite

        if (limitarSoloHorizontal)
        {
            // Dibuja un círculo plano en el suelo a la altura de la base
            DrawGizmoCircle(pivoteCentral.position, radioMaximo);
            
            // Opcional: Dibuja líneas verticales para indicar que es un cilindro
            Gizmos.color = new Color(1, 1, 0, 0.3f); // Amarillo transparente
            Vector3 arriba = Vector3.up * 1.0f; // Altura visual de referencia
            Gizmos.DrawLine(pivoteCentral.position + Vector3.right * radioMaximo, pivoteCentral.position + Vector3.right * radioMaximo + arriba);
            Gizmos.DrawLine(pivoteCentral.position + Vector3.left * radioMaximo, pivoteCentral.position + Vector3.left * radioMaximo + arriba);
            Gizmos.DrawLine(pivoteCentral.position + Vector3.forward * radioMaximo, pivoteCentral.position + Vector3.forward * radioMaximo + arriba);
            Gizmos.DrawLine(pivoteCentral.position + Vector3.back * radioMaximo, pivoteCentral.position + Vector3.back * radioMaximo + arriba);
        }
        else
        {
            // Dibuja una esfera completa si el límite es tipo burbuja
            Gizmos.DrawWireSphere(pivoteCentral.position, radioMaximo);
        }
    }

    // Función auxiliar para dibujar el círculo bonito
    void DrawGizmoCircle(Vector3 center, float r)
    {
        float step = 10f; 
        Vector3 prevPos = center + new Vector3(r, 0, 0);
        
        for (float angle = 0; angle <= 360; angle += step)
        {
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * r;
            float z = Mathf.Sin(rad) * r;
            
            Vector3 nextPos = center + new Vector3(x, 0, z);
            Gizmos.DrawLine(prevPos, nextPos);
            prevPos = nextPos;
        }
    }
}
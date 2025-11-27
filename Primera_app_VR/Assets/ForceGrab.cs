using UnityEngine;

public class ForceGrab : MonoBehaviour
{
    [Header("Conexión con Robot")]
    // 1. Aquí arrastrarás el objeto que tiene el script BraccioNetwork
    public BraccioNetwork braccioComunicador; // <--- NUEVO

    [Header("Configuración de Mano")]
    public OVRHand hand;
    
    [Header("Configuración del Objeto")]
    public Transform targetObject;
    public float speed = 5.0f;
    public float stopDistance = 0.1f;

    [Header("Configuración de Gestos")]
    [Tooltip("Fuerza necesaria para detectar el PUÑO (0.1 a 1.0)")]
    [Range(0.1f, 1.0f)]
    public float umbralPuño = 0.4f; 

    [Header("Colores")]
    public Color colorNormal = Color.blue;   // Estado base (Pinza Abierta)
    public Color colorAlterno = Color.green; // Estado Activado (Pinza Cerrada)
    public Color colorFuerza = Color.red;    // Estado Atrayendo
    
    [Header("Debug")]
    public bool mostrarValores = true;
    
    private Renderer targetRenderer; 
    
    // Variables de Estado
    private bool isGrabbingLastFrame = false;   
    private bool wasPinchingLastFrame = false;  
    private bool isGreenState = false;          // TRUE = Verde (Cerrado) | FALSE = Azul (Abierto)

    void Start()
    {
        if (targetObject != null)
        {
            targetRenderer = targetObject.GetComponent<Renderer>();
            ActualizarColor(false); 
        }
    }

    void Update()
    {
        if (hand != null && hand.IsTracked)
        {
            // 1. DETECTAR PUÑO
            bool isFist = CheckIfFist();

            if (isFist)
            {
                PullObject();
            }
            
            if (isFist != isGrabbingLastFrame)
            {
                ActualizarColor(isFist);
                isGrabbingLastFrame = isFist;
            }

            // 2. DETECTAR PELLIZCO
            if (!isFist) 
            {
                CheckPinchToggle();
            }
        }
    }

    // --- Lógica del Puño ---
    bool CheckIfFist()
    {
        float index = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        float middle = hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        float ring = hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);

        bool isFist = (index > umbralPuño) && (middle > umbralPuño) && (ring > umbralPuño);
        
        if (mostrarValores) Debug.Log($"Puño: {isFist} | I:{index:F2} M:{middle:F2} A:{ring:F2}");
        
        return isFist;
    }

    void PullObject()
    {
        if (targetObject == null) return;
        
        float distance = Vector3.Distance(targetObject.position, hand.transform.position);
        if (distance > stopDistance)
        {
            targetObject.position = Vector3.MoveTowards(targetObject.position, hand.transform.position, speed * Time.deltaTime);
            Rigidbody rb = targetObject.GetComponent<Rigidbody>();
            if(rb != null) rb.useGravity = false;
        }
    }

    // --- Lógica del Pellizco (EL INTERRUPTOR) ---
    void CheckPinchToggle()
    {
        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (isPinching && !wasPinchingLastFrame)
        {
            // Cambiamos el estado (Toggle)
            isGreenState = !isGreenState;
            
            // Actualizamos color visual
            ActualizarColor(false);
            
            Debug.Log("Cambio de Estado: " + (isGreenState ? "ACTIVADO (Verde)" : "DESACTIVADO (Azul)"));

            // ---------------------------------------------------------
            // 2. AQUÍ ESTÁ EL PUENTE (CONEXIÓN CON BRACCIO)
            // ---------------------------------------------------------
            if (braccioComunicador != null)
            {
                // Si está Verde (isGreenState = true) -> isPinching = true (Cerrar pinza)
                // Si está Azul (isGreenState = false) -> isPinching = false (Abrir pinza)
                braccioComunicador.isPinching = isGreenState; // <--- NUEVO
            }
        }

        wasPinchingLastFrame = isPinching;
    }

    void ActualizarColor(bool haciendoFuerza)
    {
        if (targetRenderer == null) return;

        if (haciendoFuerza)
        {
            targetRenderer.material.color = colorFuerza;
        }
        else
        {
            targetRenderer.material.color = isGreenState ? colorAlterno : colorNormal;
        }
    }
}
using UnityEngine;

public class ForceGrab : MonoBehaviour
{
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
    public Color colorNormal = Color.blue;  // Estado base
    public Color colorAlterno = Color.green; // Estado "Activado" por pellizco
    public Color colorFuerza = Color.red;    // Estado mientras atraes (Puño)
    
    [Header("Debug")]
    public bool mostrarValores = true;
    
    private Renderer targetRenderer; 
    
    // Variables de Estado
    private bool isGrabbingLastFrame = false;   // Para controlar el puño
    private bool wasPinchingLastFrame = false;  // Para controlar el "clic" del pellizco
    private bool isGreenState = false;          // ¿Está el objeto en modo verde?

    void Start()
    {
        if (targetObject != null)
        {
            targetRenderer = targetObject.GetComponent<Renderer>();
            ActualizarColor(false); // Inicia con el color base
        }
    }

    void Update()
    {
        if (hand != null && hand.IsTracked)
        {
            // 1. DETECTAR PUÑO (Para atraer)
            bool isFist = CheckIfFist();

            // Lógica de Atracción
            if (isFist)
            {
                PullObject();
            }
            
            // Si el estado de agarre cambia (empiezas o terminas de hacer puño), actualizamos color
            if (isFist != isGrabbingLastFrame)
            {
                ActualizarColor(isFist);
                isGrabbingLastFrame = isFist;
            }

            // 2. DETECTAR PELLIZCO (Para cambiar color Verde/Azul)
            // Solo revisamos el pellizco si NO estamos haciendo puño (para evitar conflictos)
            if (!isFist) 
            {
                CheckPinchToggle();
            }
        }
    }

    // --- Lógica del Puño (Atracción) ---
    bool CheckIfFist()
    {
        // Usamos la lógica de 3 dedos que es más estable
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

    // --- Lógica del Pellizco (Interruptor de Color) ---
    void CheckPinchToggle()
    {
        // OVRHand tiene un booleano directo para saber si estás pellizcando
        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        // LOGICA DE INTERRUPTOR (Solo actúa cuando el pellizco EMPIEZA)
        if (isPinching && !wasPinchingLastFrame)
        {
            // Invertimos el estado (Si es false pasa a true, si es true pasa a false)
            isGreenState = !isGreenState;
            
            // Actualizamos el color visualmente
            ActualizarColor(false); // false porque NO estamos haciendo fuerza en este momento
            
            Debug.Log("Cambio de Color activado: " + (isGreenState ? "VERDE" : "AZUL"));
        }

        // Guardamos el estado para el siguiente frame
        wasPinchingLastFrame = isPinching;
    }

    // --- Sistema de Colores Centralizado ---
    void ActualizarColor(bool haciendoFuerza)
    {
        if (targetRenderer == null) return;

        if (haciendoFuerza)
        {
            // Prioridad 1: Si haces fuerza, siempre es ROJO (indicador de acción)
            targetRenderer.material.color = colorFuerza;
        }
        else
        {
            // Prioridad 2: Si está reposo, depende del interruptor (Verde o Azul)
            targetRenderer.material.color = isGreenState ? colorAlterno : colorNormal;
        }
    }
}
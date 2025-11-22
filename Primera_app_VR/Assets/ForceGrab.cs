using UnityEngine;

public class ForceGrab : MonoBehaviour
{
    [Header("Configuración de Mano")]
    public OVRHand hand;
    
    [Header("Configuración del Objeto")]
    public Transform targetObject;
    public float speed = 5.0f;
    public float stopDistance = 0.1f;

    [Header("Configuración del Gesto (PUÑO)")]
    [Tooltip("Valor más bajo (0.4) para que detecte el puño aunque el tracking falle un poco")]
    [Range(0.1f, 1.0f)]
    public float umbralAgarrar = 0.4f; 

    [Header("Feedback Visual")]
    public Color colorNormal = Color.blue;
    public Color colorActivo = Color.red;
    
    [Header("Herramientas")]
    public bool mostrarValoresEnConsola = true; // Mantenlo activo para probar
    
    private Renderer targetRenderer; 
    private bool isGrabbingLastFrame = false; 

    void Start()
    {
        if (targetObject != null)
        {
            targetRenderer = targetObject.GetComponent<Renderer>();
            if (targetRenderer != null) targetRenderer.material.color = colorNormal;
        }
        else
        {
            Debug.LogError("ERROR: No has asignado el 'Target Object' en el Inspector.");
        }

        if (hand == null)
        {
            Debug.LogError("ERROR: No has asignado la variable 'Hand' (OVRHand) en el Inspector.");
        }
    }

    void Update()
    {
        // Verificamos que la mano esté detectada por las cámaras
        if (hand != null && hand.IsTracked)
        {
            bool isGrabbing = CheckIfGrabbing();

            if (isGrabbing != isGrabbingLastFrame)
            {
                UpdateObjectColor(isGrabbing);
                isGrabbingLastFrame = isGrabbing;
            }

            if (isGrabbing)
            {
                PullObject();
            }
        }
    }

    bool CheckIfGrabbing()
    {
        // Obtenemos la fuerza de los 3 dedos principales
        // Ignoramos el Meñique (Pinky) porque suele perderse al cerrar el puño
        float index = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        float middle = hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        float ring = hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);

        // CONDICIÓN DE PUÑO ROBUSTA:
        // Requerimos que Índice, Medio y Anular superen el umbral.
        bool isFist = (index > umbralAgarrar) && (middle > umbralAgarrar) && (ring > umbralAgarrar);

        // DEBUG: Esto te dirá exactamente por qué falla
        if (mostrarValoresEnConsola)
        {
            // Si uno de estos valores se queda en 0.00 cuando cierras la mano, 
            // las gafas no están viendo ese dedo bien.
            Debug.Log($"Puño: {isFist} || I:{index:F2} M:{middle:F2} A:{ring:F2}");
        }

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

    void UpdateObjectColor(bool active)
    {
        if (targetRenderer != null) targetRenderer.material.color = active ? colorActivo : colorNormal;
    }
}
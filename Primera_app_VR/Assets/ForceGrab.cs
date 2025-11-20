using UnityEngine;

public class ForceGrab : MonoBehaviour
{
    [Header("Configuración de Mano")]
    public OVRHand hand;
    
    [Header("Configuración del Objeto")]
    public Transform targetObject; // Tu esfera
    public float speed = 5.0f;
    public float stopDistance = 0.1f;

    [Header("Feedback Visual")]
    public Color colorNormal = Color.blue; // Color en reposo
    public Color colorActivo = Color.red;  // Color al pinchar
    
    private Renderer targetRenderer; 
    private bool isPinchingLastFrame = false; 

    void Start()
    {
        // Buscamos el Renderer para poder cambiar el color
        if (targetObject != null)
        {
            targetRenderer = targetObject.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                targetRenderer.material.color = colorNormal;
            }
        }
    }

    void Update()
    {
        if (hand.IsTracked)
        {
            // Detectamos si estás haciendo el gesto
            bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

            // Lógica de cambio de color (solo si el estado cambia)
            if (isPinching != isPinchingLastFrame)
            {
                UpdateObjectColor(isPinching);
                isPinchingLastFrame = isPinching;
            }

            // Si hay pinch, atraemos el objeto
            if (isPinching)
            {
                PullObject();
            }
        }
        else 
        {
            // Si se pierde el rastreo de la mano, aseguramos volver al color normal
            if (isPinchingLastFrame)
            {
                UpdateObjectColor(false);
                isPinchingLastFrame = false;
            }
        }
    }

    void PullObject()
    {
        if (targetObject == null) return;

        // Calculamos distancia
        float distance = Vector3.Distance(targetObject.position, hand.transform.position);

        // Si está lejos, lo acercamos
        if (distance > stopDistance)
        {
            targetObject.position = Vector3.MoveTowards(
                targetObject.position, 
                hand.transform.position, 
                speed * Time.deltaTime
            );
            
            // Desactivamos gravedad para que flote hacia ti
            Rigidbody rb = targetObject.GetComponent<Rigidbody>();
            if(rb != null) rb.useGravity = false;
        }
    }

    void UpdateObjectColor(bool active)
    {
        if (targetRenderer != null)
        {
            targetRenderer.material.color = active ? colorActivo : colorNormal;
        }
    }
}
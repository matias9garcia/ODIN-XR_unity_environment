using UnityEngine;

public class ResetArmPosition : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Arrastra aquí la esfera que controla el brazo")]
    public Transform sphereTarget;

    [Tooltip("Tecla para resetear la posición")]
    public KeyCode resetKey = KeyCode.R;

    // Variables para guardar la posición y rotación iniciales
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Rigidbody rb;

    void Start()
    {
        if (sphereTarget == null)
        {
            Debug.LogError("⚠️ ¡Falta asignar la Esfera en el inspector del script ResetArmPosition!");
            return;
        }

        // 1. Guardamos la posición original al inicio del juego
        initialPosition = sphereTarget.position;
        initialRotation = sphereTarget.rotation;

        // Intentamos obtener el Rigidbody por si la esfera usa físicas
        rb = sphereTarget.GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 2. Detectamos si se presiona la tecla configurada
        if (Input.GetKeyDown(resetKey))
        {
            ResetPosition();
        }
    }

    public void ResetPosition()
    {
        if (sphereTarget == null) return;

        // 3. Restauramos la posición y rotación
        sphereTarget.position = initialPosition;
        sphereTarget.rotation = initialRotation;

        // 4. Si la esfera tiene físicas, detenemos su movimiento para que no salga disparada
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("Brazo reseteado a posición inicial.");
    }
}
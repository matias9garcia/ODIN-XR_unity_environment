using UnityEngine;
using UnityEngine.EventSystems;

public class LaserPointer : MonoBehaviour
{
    public float laserLength = 5.0f; // Largo máximo del rayo
    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        // El rayo sale desde la posición de la mano hacia adelante
        Vector3 endPosition = transform.position + (transform.forward * laserLength);
        
        // Creamos un Raycast físico por si acaso (opcional)
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, laserLength))
        {
            endPosition = hit.point;
        }

        // Actualizamos el dibujante de línea
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, endPosition);
    }
}
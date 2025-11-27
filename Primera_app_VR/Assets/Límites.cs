using UnityEngine;

public class RadialLimiter : MonoBehaviour
{
    [Header("Referencia")]
    [Tooltip("El objeto vacío que actúa como centro del círculo/esfera")]
    public Transform centerPoint;

    [Header("Configuración")]
    [Tooltip("La distancia máxima permitida desde el centro (Radio)")]
    public float radius = 5f;

    [Tooltip("Si es TRUE, el límite es un círculo en el suelo (Cilíndrico). Si es FALSE, es una esfera completa (3D).")]
    public bool flattenY = true;

    void LateUpdate()
    {
        if (centerPoint == null) return;

        // 1. Calculamos el vector desde el centro hasta el jugador
        Vector3 offset = transform.position - centerPoint.position;

        if (flattenY)
        {
            // --- LÓGICA CILÍNDRICA (Círculo en el suelo) ---
            
            // Guardamos la altura actual para no afectarla
            float currentY = offset.y;
            
            // Anulamos la altura temporalmente para medir solo distancia horizontal
            offset.y = 0;

            // Restringimos la distancia horizontal
            offset = Vector3.ClampMagnitude(offset, radius);

            // Restauramos la altura original (el jugador puede saltar libremente)
            offset.y = currentY;
        }
        else
        {
            // --- LÓGICA ESFÉRICA (Burbuja 3D) ---
            
            // Restringimos el vector en todas las direcciones
            offset = Vector3.ClampMagnitude(offset, radius);
        }

        // 2. Aplicamos la posición final
        transform.position = centerPoint.position + offset;
    }

    void OnDrawGizmos()
    {
        if (centerPoint == null) return;

        Gizmos.color = Color.cyan;

        if (flattenY)
        {
            // Dibujar un círculo plano en el suelo para visualizar el límite
            DrawGizmoCircle(centerPoint.position, radius);
            
            // Opcional: Dibujar líneas verticales para simular las "paredes" del cilindro
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawLine(centerPoint.position + Vector3.left * radius, centerPoint.position + Vector3.left * radius + Vector3.up * 5);
            Gizmos.DrawLine(centerPoint.position + Vector3.right * radius, centerPoint.position + Vector3.right * radius + Vector3.up * 5);
        }
        else
        {
            // Dibujar una esfera completa de alambre
            Gizmos.DrawWireSphere(centerPoint.position, radius);
        }
    }

    // Pequeña función auxiliar para dibujar un círculo bonito en Gizmos
    void DrawGizmoCircle(Vector3 center, float r)
    {
        float step = 10f; // Resolución del círculo
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
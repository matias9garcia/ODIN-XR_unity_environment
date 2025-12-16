using UnityEngine;
using System.Collections.Generic;

public class SphereConstraint : MonoBehaviour
{
    [Header("Configuración del Brazo")]
    // Arrastra aquí todas las articulaciones EN ORDEN (Base -> Codo -> Muñeca -> Punta)
    public List<Transform> armJoints = new List<Transform>(); 
    
    [Header("Referencias de IK")]
    public Transform cubeEndEffector; // Opcional (para la rotación)

    private float totalArmLength;
    private Transform armBase;

    void Start()
    {
        CalculateLength();
    }

    void CalculateLength()
    {
        // 1. Validamos que haya al menos 2 puntos para medir distancia
        if (armJoints.Count < 2)
        {
            Debug.LogError("¡Necesitas asignar al menos 2 articulaciones en la lista 'Arm Joints'!");
            return;
        }

        // La base será el primer elemento de la lista
        armBase = armJoints[0];
        totalArmLength = 0f;

        // 2. Sumamos las distancias entre cada par de articulaciones
        for (int i = 0; i < armJoints.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(armJoints[i].position, armJoints[i + 1].position);
            totalArmLength += segmentLength;
        }

        // Imprimimos el largo calculado en consola para que verifiques si es correcto
        Debug.Log("Largo total del brazo calculado: " + totalArmLength);
    }

    void LateUpdate()
    {
        if (armBase == null) return;

        // 3. El resto de la lógica es igual, pero usando el totalArmLength calculado
        Vector3 direction = transform.position - armBase.position;
        float currentDistance = direction.magnitude;

        if (currentDistance > totalArmLength)
        {
            transform.position = armBase.position + (direction.normalized * totalArmLength);
        }

        if (cubeEndEffector != null)
        {
            cubeEndEffector.rotation = transform.rotation;
        }
    }
}
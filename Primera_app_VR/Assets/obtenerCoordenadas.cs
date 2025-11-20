using UnityEngine;
using TMPro; // Necesario para usar TextMeshPro

public class CoordenadasEnCM : MonoBehaviour
{
    [Header("Configuración")]
    public Transform objetoA_Rastrear;  // Tu Cubo
    public TextMeshPro textoMundial;    // El componente de texto en el mundo

    [Header("Opciones de Visualización")]
    public bool mostrarDecimales = true; // ¿Quieres ver 150.5 cm o 150 cm?

    void Update()
    {
        if (objetoA_Rastrear == null || textoMundial == null) return;

        // 1. Obtenemos la posición original en Metros
        Vector3 posicionMetros = objetoA_Rastrear.position;

        // 2. Convertimos a Centímetros (Metros * 100)
        float x_cm = posicionMetros.x * 100f;
        float y_cm = posicionMetros.y * 100f;
        float z_cm = posicionMetros.z * 100f;

        // 3. Formateamos el texto para mostrarlo
        // "F1" significa 1 decimal (ej: 10.5), "F0" es sin decimales (ej: 10)
        string formato = mostrarDecimales ? "F1" : "F0";

        textoMundial.text = $"COORDENADAS (CM)\n" +
                            $"X: {x_cm.ToString(formato)}\n" +
                            $"Y: {y_cm.ToString(formato)}\n" +
                            $"Z: {z_cm.ToString(formato)}";
    }
}
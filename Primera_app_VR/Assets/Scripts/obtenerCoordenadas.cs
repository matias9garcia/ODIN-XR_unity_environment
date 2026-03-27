using UnityEngine;
using TMPro;

public class CoordenadasRelativas : MonoBehaviour
{
    [Header("Configuración")]
    public Transform objetoA_Rastrear;   // Tu Cubo (o el objeto que se mueve)
    public Transform objetoOrigen;       // EL CILINDRO (Tu nuevo punto 0,0,0)
    public TextMeshPro textoMundial;     // El texto para mostrar

    [Header("Opciones de Visualización")]
    public bool mostrarDecimales = true; 

    void Update()
    {
        // Verificamos que todos los objetos estén asignados para evitar errores
        if (objetoA_Rastrear == null || textoMundial == null || objetoOrigen == null) return;

        // 1. Mágia matemática de Unity:
        // Convertimos la posición mundial del objeto a rastrear
        // a una posición RELATIVA al "objetoOrigen" (el cilindro).
        Vector3 posicionRelativa = objetoOrigen.InverseTransformPoint(objetoA_Rastrear.position);

        // 2. Convertimos a Centímetros (Metros * 100)
        // Usamos la posicionRelativa calculada arriba, no la mundial.
        float x_cm = posicionRelativa.x * 100f;
        float y_cm = posicionRelativa.y * 100f;
        float z_cm = -posicionRelativa.z * 100f;

        // 3. Formateamos el texto
        string formato = mostrarDecimales ? "F1" : "F0";

        textoMundial.text = $"<color=#A020F0>RELATIVE COORDINATES OF SPHERE (CM)</color>\n" +
                            $"<color=#A020F0>X: {x_cm.ToString(formato),8} cm</color>\n" +
                            $"<color=#A020F0>Y: {y_cm.ToString(formato),8} cm</color>\n" +
                            $"<color=#A020F0>Z: {z_cm.ToString(formato),8} cm</color>";
        textoMundial.color = new Color(0.627f, 0.125f, 0.941f, 1f);
    }
}
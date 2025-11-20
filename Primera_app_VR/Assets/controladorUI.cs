using UnityEngine;
using TMPro; // ¡Importante! Necesario para usar TextMeshPro

public class ControladorUI : MonoBehaviour
{
    // --- Variables que especificaste ---
    public TMP_Dropdown Dropdown;      // El menú desplegable
    public GameObject Sphere;        // La esfera

    // --- Colores para las 3 opciones ---
    // Puedes asignar estos colores desde el Inspector de Unity
    public Color colorOpcion1 = Color.red;
    public Color colorOpcion2 = Color.green;
    public Color colorOpcion3 = Color.blue;

    // --- Variable interna ---
    private Renderer esferaRenderer; // Componente para cambiar el color

    void Start()
    {
        // 1. Al iniciar, obtenemos el componente "Renderer" de la esfera.
        if (Sphere != null)
        {
            esferaRenderer = Sphere.GetComponent<Renderer>();
        }

        // 2. Si no tiene renderer, mostramos un error para ayudarte
        if (esferaRenderer == null)
        {
            Debug.LogError("El objeto 'Sphere' no tiene un componente 'Renderer'.");
        }
    }

    // 3. Esta es la función que llamará el botón
    public void RevisarSeleccionYActuar()
    {
        // Si no encontramos el renderer, no continuamos
        if (esferaRenderer == null) return;

        // 4. (Opcional) Nos aseguramos de que la esfera esté activa
        //    (basado en tu script anterior)
        Sphere.SetActive(true);

        // 5. Obtenemos el ÍNDICE de la opción seleccionada:
        //    Índice 0 = Primera opción
        //    Índice 1 = Segunda opción
        //    Índice 2 = Tercera opción
        int indiceSeleccionado = Dropdown.value;

        // 6. Usamos un 'switch' para asignar el color según el índice
        switch (indiceSeleccionado)
        {
            case 0: // Si es la primera opción
                esferaRenderer.material.color = colorOpcion1;
                break;
            
            case 1: // Si es la segunda opción
                esferaRenderer.material.color = colorOpcion2;
                break;
            
            case 2: // Si es la tercera opción
                esferaRenderer.material.color = colorOpcion3;
                break;
            
            // Puedes añadir más "case" si tu dropdown tiene más opciones
            // case 3:
            //     esferaRenderer.material.color = Color.yellow;
            //     break;
        }
    }
}
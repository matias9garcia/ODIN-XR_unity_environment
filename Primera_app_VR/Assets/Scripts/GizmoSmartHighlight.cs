using UnityEngine;

public class GizmoSmartHighlight : MonoBehaviour
{
    [Header("Configuración de Ejes")]
    public Renderer ejeX;
    public Renderer ejeY;
    public Renderer ejeZ;

    [Header("Materiales")]
    public Material matRojo;
    public Material matVerde;
    public Material matAzul;
    public Material matGris;

    [Header("Ajustes de Sensibilidad")]
    // Aumenté esto un poco para el mouse
    public float umbralMovimiento = 0.005f; 
    
    // NUEVO: Cuánto tiempo esperar antes de resetear colores (evita parpadeo)
    public float tiempoDeEspera = 0.15f; 

    private Vector3 ultimaPosicion;
    private float temporizadorParada = 0f;
    private bool estaResaltando = false; // Para saber si ya tenemos un eje iluminado

    void Start()
    {
        ultimaPosicion = transform.position;
    }

    void Update()
    {
        Vector3 movimiento = transform.position - ultimaPosicion;
        
        float movX = Mathf.Abs(movimiento.x);
        float movY = Mathf.Abs(movimiento.y);
        float movZ = Mathf.Abs(movimiento.z);

        // Si la velocidad total es mayor que el umbral...
        if (movimiento.magnitude > umbralMovimiento)
        {
            // Reseteamos el temporizador porque nos estamos moviendo
            temporizadorParada = 0f;
            estaResaltando = true;

            // Decidimos cuál eje gana
            if (movX > movY && movX > movZ)
            {
                ResaltarX();
            }
            else if (movY > movX && movY > movZ)
            {
                ResaltarY();
            }
            else
            {
                ResaltarZ();
            }
        }
        else
        {
            // Si NO nos movemos (o nos movemos muy lento)...
            // Aumentamos el contador de tiempo
            temporizadorParada += Time.deltaTime;

            // Solo restauramos si ha pasado el tiempo de espera Y si estaban cambiados
            if (temporizadorParada > tiempoDeEspera && estaResaltando)
            {
                RestaurarTodos();
                estaResaltando = false; // Ya terminamos de restaurar
            }
        }

        ultimaPosicion = transform.position;
    }

    void ResaltarX()
    {
        // Solo cambiamos el material si no es el correcto, para ahorrar rendimiento
        if(ejeX.sharedMaterial != matRojo) ejeX.material = matRojo;
        if(ejeY.sharedMaterial != matGris) ejeY.material = matGris;
        if(ejeZ.sharedMaterial != matGris) ejeZ.material = matGris;
    }

    void ResaltarY()
    {
        if(ejeX.sharedMaterial != matGris) ejeX.material = matGris;
        if(ejeY.sharedMaterial != matVerde) ejeY.material = matVerde;
        if(ejeZ.sharedMaterial != matGris) ejeZ.material = matGris;
    }

    void ResaltarZ()
    {
        if(ejeX.sharedMaterial != matGris) ejeX.material = matGris;
        if(ejeY.sharedMaterial != matGris) ejeY.material = matGris;
        if(ejeZ.sharedMaterial != matAzul) ejeZ.material = matAzul;
    }

    public void RestaurarTodos()
    {
        ejeX.material = matRojo;
        ejeY.material = matVerde;
        ejeZ.material = matAzul;
    }
}
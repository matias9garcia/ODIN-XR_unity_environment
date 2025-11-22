using UnityEngine;
using System.Collections;

public class BraccioPinza : MonoBehaviour
{
    [Header("Configuración de Manos")]
    public OVRHand manoUsuario; // Arrastra aquí tu OVRHandPrefab (Left o Right)
    
    [Header("Configuración de Braccio")]
    public Transform esferaBraccio; // Tu objeto esfera
    public float radioAgarre = 0.2f; // Qué tan cerca debe estar el cubo
    public LayerMask capaInteractuable; // Para que solo agarre cubos, no el suelo

    private GameObject objetoAgarrado = null;
    private bool estaPellizcando = false;

    void Update()
    {
        // 1. Detectar el estado del pellizco (Índice + Pulgar)
        estaPellizcando = manoUsuario.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (estaPellizcando)
        {
            // Si está pellizcando y no tenemos nada, intentamos agarrar
            if (objetoAgarrado == null)
            {
                IntentarAgarrar();
            }
        }
        else
        {
            // Si deja de pellizcar y tenemos algo, lo soltamos
            if (objetoAgarrado != null)
            {
                SoltarObjeto();
            }
        }
    }

    void IntentarAgarrar()
    {
        // Crea una esfera invisible de detección alrededor de Braccio
        Collider[] colisiones = Physics.OverlapSphere(esferaBraccio.position, radioAgarre, capaInteractuable);

        if (colisiones.Length > 0)
        {
            // Agarramos el primer objeto que encontremos
            objetoAgarrado = colisiones[0].gameObject;

            // Desactivamos físicas para que no "tiemble" al moverlo
            Rigidbody rb = objetoAgarrado.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // Emparentamos el cubo a la esfera Braccio
            objetoAgarrado.transform.SetParent(esferaBraccio);
        }
    }

    void SoltarObjeto()
    {
        // Reactivamos físicas (gravedad)
        Rigidbody rb = objetoAgarrado.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        // Desemparentamos (lo devolvemos al mundo)
        objetoAgarrado.transform.SetParent(null);
        objetoAgarrado = null;
    }

    // Dibujo visual para ver el radio de agarre en el editor (Gizmo)
    void OnDrawGizmos()
    {
        if (esferaBraccio != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(esferaBraccio.position, radioAgarre);
        }
    }
}
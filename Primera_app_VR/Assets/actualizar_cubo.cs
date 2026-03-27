using System.Net;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class actualizar_cubo : MonoBehaviour
{
    [System.Serializable]
    public class MotionData { public float x; public float y; }

    [Header("Configuración de Movimiento")]
    public float sensibilidad = 0.01f;
    public float alturaLocalY = 0.05f;
    
    [Range(0.01f, 1.0f)]
    public float suavizadoLerp = 0.1f;

    [Header("Invertir Ejes (Check = *-1)")]
    public bool invertirX = false;
    public bool invertirY = false; 
    public bool invertirZ = false;

    [Header("Conexión")]
    public string apiUrl = "https://dustin-unedible-bethany.ngrok-free.dev/api/posicion";

    [Header("Depuración")]
    public bool mostrarLineaDepuracion = true;

    private Vector3 posicionObjetivo; 
    private Vector2 offsetCalibracion = Vector2.zero; 
    private Vector2 ultimaPosicionAPI = Vector2.zero;
    private bool keepUpdating = false;

    void Start()
    {
        // Al iniciar, asumimos que el cubo empieza en su sitio
        posicionObjetivo = new Vector3(0, alturaLocalY, 0);
        keepUpdating = true;
        _ = DescargarDatosDeAPI();
    }

    void Update()
    {
        // Aplicar Suavizado
        transform.localPosition = Vector3.Lerp(transform.localPosition, posicionObjetivo, suavizadoLerp);

        // CALIBRACIÓN CON TECLA C
        if (Input.GetKeyDown(KeyCode.C))
        {
            CalibrarPuntoCero();
        }

        // Líneas de depuración
        if (mostrarLineaDepuracion && transform.parent != null)
        {
            Debug.DrawRay(transform.parent.position, transform.parent.forward * 0.5f, Color.blue); 
            Debug.DrawRay(transform.parent.position, transform.parent.right * 0.5f, Color.red);    
            Debug.DrawLine(transform.parent.position, transform.position, Color.yellow);         
        }
    }

    void CalibrarPuntoCero()
    {
        // 1. Capturamos el valor actual de la API como el nuevo (0,0)
        offsetCalibracion = ultimaPosicionAPI;

        // 2. Forzamos el objetivo a ser exactamente el centro local (0 en X y Z)
        // Esto elimina el salto visual del offset
        posicionObjetivo = new Vector3(0, alturaLocalY * (invertirY ? -1f : 1f), 0);
        
        // 3. Opcional: Teletransportar instantáneamente para evitar el "deslizamiento" del Lerp tras calibrar
        transform.localPosition = posicionObjetivo;

        Debug.Log($"<color=cyan>🎯 CALIBRADO: API Offset set to {offsetCalibracion}. Cubo centrado en Ansuz.</color>");
    }

    async Task DescargarDatosDeAPI()
    {
        while (keepUpdating)
        {
            try
            {
                string json = await GetResponseAsync(apiUrl);
                if (!string.IsNullOrEmpty(json))
                {
                    MotionData data = JsonUtility.FromJson<MotionData>(json);
                    ultimaPosicionAPI = new Vector2(data.x, data.y);

                    float mX = invertirX ? -1f : 1f;
                    float mY = invertirY ? -1f : 1f;
                    float mZ = invertirZ ? -1f : 1f;

                    // APLICACIÓN DEL OFFSET: (Valor Actual - Valor de Calibración)
                    float xFinal = (data.x - offsetCalibracion.x) * mX * sensibilidad;
                    float zFinal = (data.y - offsetCalibracion.y) * mZ * sensibilidad;
                    float yFinal = alturaLocalY * mY;

                    posicionObjetivo = new Vector3(xFinal, yFinal, zFinal);
                }
            }
            catch (System.Exception e) {
                Debug.LogWarning("Error API: " + e.Message);
            }
            await Task.Delay(20);
        }
    }

    async Task<string> GetResponseAsync(string url)
    {
        try {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 500;
            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                return await reader.ReadToEndAsync();
        } catch { return null; }
    }

    private void OnDisable() => keepUpdating = false;
}
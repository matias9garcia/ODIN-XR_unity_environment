using System.Net;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using System.Globalization;

public class CubeAPIUpdater : MonoBehaviour
{
    [System.Serializable]
    public class MotionData
    {
        public string x;
        public string y;
        public string z;
    }

    [Header("Referencia de Origen")]
    public Transform ansuzRuneTransform; 

    [Header("Configuración de Ejes")]
    public bool swapYandZ = true; 
    public Vector3 invertirEjes = new Vector3(1, 1, 1);

    [Header("Calibración de Sensibilidad")]
    [Tooltip("Usa 0.01 si la API manda cm. Usa 1.0 si manda metros.")]
    public float sensibilidadGeneral = 0.01f; 
    public float suavizado = 15f;

    [Header("Conexión")]
    public string apiUrl = "https://dustin-unedible-bethany.ngrok-free.dev/api/posicion";
    
    private bool keepUpdating = false;
    private Vector3 offsetDesdeRuna;
    
    // Variables para el "Punto Cero"
    private bool calibrated = false;
    private Vector3 initialRawPos;

    void Start()
    {
        if (ansuzRuneTransform == null) {
            Debug.LogError("Asigna la Runa Ansuz.");
            return;
        }
        keepUpdating = true;
        _ = RequestLoop(); 
    }

    // Botón para resetear la posición manualmente desde el inspector
    [ContextMenu("Recalibrar Punto Cero")]
    public void ResetCalibration() { calibrated = false; }

    void Update()
    {
        if (ansuzRuneTransform == null || !calibrated) return;

        // Calculamos posición destino: Runa + (Rotación de Runa * Desplazamiento relativo)
        Vector3 finalWorldPos = ansuzRuneTransform.position + (ansuzRuneTransform.rotation * offsetDesdeRuna);

        // Suavizado
        transform.position = Vector3.Lerp(transform.position, finalWorldPos, Time.deltaTime * suavizado);
    }

    async Task RequestLoop()
    {
        while (keepUpdating)
        {
            try
            {
                string json = await GetResponseAsync(apiUrl);
                MotionData data = JsonUtility.FromJson<MotionData>(json);

                if (data != null)
                {
                    float valX = ParseFloat(data.x);
                    float valY = ParseFloat(data.y);
                    float valZ = ParseFloat(data.z);

                    // Mapeo inicial de ejes
                    float xFinal = valX;
                    float yFinal = swapYandZ ? valZ : valY;
                    float zFinal = swapYandZ ? valY : valZ;
                    
                    Vector3 currentRawPos = new Vector3(xFinal, yFinal, zFinal);

                    // CALIBRACIÓN: Si es el primer dato, lo guardamos como origen
                    if (!calibrated)
                    {
                        initialRawPos = currentRawPos;
                        calibrated = true;
                        Debug.Log($"[SISTEMA] Calibrado. Posición inicial: {initialRawPos}");
                    }

                    // Calculamos cuanto se movió DESDE el inicio
                    Vector3 deltaPos = currentRawPos - initialRawPos;

                    // Aplicamos inversión y sensibilidad
                    offsetDesdeRuna = new Vector3(
                        deltaPos.x * invertirEjes.x,
                        deltaPos.y * invertirEjes.y,
                        deltaPos.z * invertirEjes.z
                    ) * sensibilidadGeneral;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error: " + e.Message);
            }
            await Task.Delay(20);
        }
    }

    private float ParseFloat(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        return float.Parse(value.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    private void OnDisable() => keepUpdating = false;

    async Task<string> GetResponseAsync(string url)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Timeout = 1000;
        using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
        {
            return await reader.ReadToEndAsync();
        }
    }
}
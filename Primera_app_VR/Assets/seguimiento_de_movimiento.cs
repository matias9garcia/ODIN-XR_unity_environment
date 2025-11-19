using System.Net;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CubeAPIUpdater : MonoBehaviour
{
    [System.Serializable]
    public class MotionData
    {
        public long timestamp;
        public float x;
        public float y;
        public float z;
        public float pitch;
        public float yaw;
        public float roll;
    }

    public string apiUrl = "http://10.108.231.187:5000/api/posicion";
    private bool keepUpdating = false;

    async void Start()
    {
        keepUpdating = true;

        while (keepUpdating)
        {
            try
            {
                string json = await GetResponseAsync(apiUrl);
                Debug.Log("Respuesta: " + json);

                MotionData data = JsonUtility.FromJson<MotionData>(json);

                // Actualiza posición (usamos x, y, z)
                transform.position = new Vector3(data.x, data.y, data.z);

                // Actualiza rotación con pitch, yaw, roll (en grados)
                //transform.rotation = Quaternion.Euler(data.pitch, data.yaw, data.roll);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error: " + e.Message);
            }

            await Task.Delay(20);
        }
    }

    private void OnDisable()
    {
        keepUpdating = false;
    }

    public void StopUpdating()
    {
        keepUpdating = false;
    }

    async Task<string> GetResponseAsync(string url)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";

        using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
        {
            return await reader.ReadToEndAsync();
        }
    }
}

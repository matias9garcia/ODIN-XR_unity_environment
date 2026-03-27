using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text; // Necesario para Encoding

public class BraccioNetwork : MonoBehaviour {

    [Header("--- Configuración de Red ---")]
    public string urlServidor = "http://127.0.0.1:5000/api/angulos_braccio";
    
    [Tooltip("Desactiva esto si la consola se llena demasiado de mensajes de éxito")]
    public bool mostrarLogsDebug = true; 

    // Estructura de datos interna para el JSON
    // Se mantiene privada porque nadie más necesita conocer esta estructura exacta
    [Serializable]
    private struct BraccioData {
        public long timestamp;
        public int m1, m2, m3, m4, m5, m6, apertura_pinza;
    }

    /// <summary>
    /// Método público para enviar comandos al brazo.
    /// Llama a esto desde tu Controller.
    /// </summary>
    /// <param name="m1">Base (0-180)</param>
    /// <param name="m2">Hombro (0-180)</param>
    /// <param name="m3">Codo (0-180)</param>
    /// <param name="m4">Muñeca V (0-180)</param>
    /// <param name="m5">Muñeca R (0-180)</param>
    /// <param name="m6">Pinza (Real/Servo angle)</param>
    /// <param name="estadoPinza">Lógica (1=Abierto, 0=Cerrado) o según tu API</param>
    public void EnviarDatos(int m1, int m2, int m3, int m4, int m5, int m6, int estadoPinza) {
        StartCoroutine(RutinaEnvioPOST(m1, m2, m3, m4, m5, m6, estadoPinza));
    }

    // La corrutina real que hace el trabajo sucio
    private IEnumerator RutinaEnvioPOST(int m1, int m2, int m3, int m4, int m5, int m6, int estadoPinza) {
        // 1. Empaquetar datos en la estructura
        BraccioData data = new BraccioData();
        data.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        data.m1 = m1;
        data.m2 = m2;
        data.m3 = m3;
        data.m4 = m4;
        data.m5 = m5;
        data.m6 = m6;
        data.apertura_pinza = estadoPinza;

        // 2. Convertir a JSON
        string json = JsonUtility.ToJson(data);

        // 3. Configurar la petición Web (POST)
        using (UnityWebRequest req = new UnityWebRequest(urlServidor, "POST")) {
            byte[] body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            // 4. Enviar y esperar respuesta
            yield return req.SendWebRequest();

            // 5. Verificar resultado
            if (req.result == UnityWebRequest.Result.Success) {
                if (mostrarLogsDebug) {
                    Debug.Log($"[BraccioNetwork] Éxito: {req.downloadHandler.text} | Datos: M1:{m1} M2:{m2} M3:{m3}");
                }
            } else {
                Debug.LogError($"[BraccioNetwork] Error de conexión ({req.responseCode}): {req.error}");
            }
        }
    }
}
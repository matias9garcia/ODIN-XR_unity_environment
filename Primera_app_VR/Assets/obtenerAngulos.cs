using UnityEngine;
using TMPro;
using System;
using System.Collections;           // Necesario para Corutinas
using UnityEngine.Networking;       // Necesario para HTTP POST

public class BraccioNetwork : MonoBehaviour
{
    [Header("Configuración de Red")]
    public string urlServidor = "http://127.0.0.1:5000/api/braccio"; // Cambia esto por tu URL real
    [Range(0.05f, 1f)] public float intervaloEnvio = 0.1f; // Enviar cada 0.1 segundos (10 veces por seg)

    [Header("Referencias de Escena")]
    public Transform objetoA_Rastrear;
    public Transform objetoOrigen;
    public TextMeshPro textoAngulos;

    [Header("Medidas del Brazo (en cm)")]
    public float d1_Base = 10.0f;
    public float L2_Brazo = 35.0f;
    public float L3_Antebrazo = 35.0f;

    [Header("Control Manual / Gestos")]
    [Range(0, 180)] public int M5_Rotacion = 90; // Muñeca Rotación
    public bool isPinching = false;                // ¿Está haciendo el gesto de pellizco?

    // Variables internas para cálculos
    private double d1, L2, L3;
    private float timer = 0f;
    
    // Variables para almacenar los últimos ángulos calculados
    private int _m1, _m2, _m3, _m4, _m5, _m6;
    private int _estadoPinza; // 0 o 1

    // --- ESTRUCTURA PARA JSON ---
    [Serializable]
    private struct BraccioData
    {
        public long timestamp;
        public int m1;
        public int m2;
        public int m3;
        public int m4;
        public int m5;
        public int m6;
        public int apertura_pinza;
    }

    void Update()
    {
        if (objetoA_Rastrear == null || textoAngulos == null || objetoOrigen == null) return;

        // 1. Actualizar constantes
        d1 = (double)d1_Base;
        L2 = (double)L2_Brazo;
        L3 = (double)L3_Antebrazo;

        // 2. Calcular Cinemática Inversa (IK)
        // Esto actualiza las variables _m1, _m2, _m3, _m4
        ProcesarIK();

        // 3. Procesar M5 y M6 (Pellizco)
        _m5 = M5_Rotacion;

        if (isPinching)
        {
            // Si pellizca -> CERRADO
            _m6 = 10;          // Grados físicos (cerrado)
            _estadoPinza = 0;  // Lógica (0 = cerrado)
        }
        else
        {
            // Si no pellizca -> ABIERTO
            _m6 = 73;          // Grados físicos (abierto)
            _estadoPinza = 1;  // Lógica (1 = abierto)
        }

        // 4. Enviar al Servidor (Control de frecuencia)
        timer += Time.deltaTime;
        if (timer >= intervaloEnvio)
        {
            StartCoroutine(EnviarDatosPOST());
            timer = 0f;
        }
    }

    private void ProcesarIK()
    {
        // Obtener posición relativa
        Vector3 posRelativa = objetoOrigen.InverseTransformPoint(objetoA_Rastrear.position);
        double x = posRelativa.x * 100.0;
        double y = posRelativa.z * 100.0; // Z unity es Y braccio
        double z = posRelativa.y * 100.0; // Y unity es Z braccio

        // --- MATEMÁTICAS ---
        double m1Rad = Math.Atan2(y, x);
        double m1Deg = m1Rad * 180.0 / Math.PI;
        if (m1Deg < 0) m1Deg += 360;

        double R = Math.Sqrt(x * x + y * y);
        double Z_rel = z - d1;
        double D = Math.Sqrt(R * R + Z_rel * Z_rel);
        double alcanceMaximo = L2 + L3;

        string mensajeEstado = "OPERATIVO";

        // Validación de rango
        if (D > (alcanceMaximo + 0.1))
        {
            mensajeEstado = "FUERA DE RANGO";
            // Aun así calculamos para intentar acercarnos o mantenemos el último válido
            // pero para el ejemplo, dejamos que siga el cálculo matemático "roto" o lo limitamos.
        }

        double cos_angle3 = (D * D - L2 * L2 - L3 * L3) / (2 * L2 * L3);
        // Clamp
        if (cos_angle3 > 1.0) cos_angle3 = 1.0;
        if (cos_angle3 < -1.0) cos_angle3 = -1.0;

        double angle3Rad = Math.Acos(cos_angle3);
        double angle2Rad = Math.Atan2(Z_rel, R) - Math.Atan2(L3 * Math.Sin(angle3Rad), L2 + L3 * Math.Cos(angle3Rad));

        // Asignación a variables globales para el JSON
        _m1 = (int)m1Deg;
        _m3 = 180 - (int)(angle3Rad * 180.0 / Math.PI);
        _m2 = (int)(angle2Rad * 180.0 / Math.PI) + 90;
        _m4 = 180 - (_m2 + _m3);

        // Actualizar Texto en Pantalla
        textoAngulos.text = $"ESTADO: {mensajeEstado}\n" +
                            $"Distancia: {D:F1} cm\n" +
                            $"------------------\n" +
                            $"M1: {_m1} | M2: {_m2}\n" +
                            $"M3: {_m3} | M4: {_m4}\n" +
                            $"M5: {_m5} | M6: {_m6}\n" +
                            $"Pinza: {(_estadoPinza == 1 ? "ABIERTA" : "CERRADA")}";
    }

    IEnumerator EnviarDatosPOST()
    {
        // 1. Construir el objeto JSON
        BraccioData data = new BraccioData();
        // C# usa DateTimeOffset para obtener milisegundos tipo Unix Epoch
        data.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        data.m1 = _m1;
        data.m2 = _m2;
        data.m3 = _m3;
        data.m4 = _m4;
        data.m5 = _m5;
        data.m6 = _m6;
        data.apertura_pinza = _estadoPinza;

        // 2. Serializar a String JSON
        string jsonData = JsonUtility.ToJson(data);

        // 3. Configurar la petición Web
        // UnityWebRequest es la forma moderna de hacer HTTP en Unity
        using (UnityWebRequest request = new UnityWebRequest(urlServidor, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // 4. Enviar y esperar
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // Solo muestra error si falla, para no ensuciar la consola
                Debug.LogWarning("Error envío POST: " + request.error);
            }
            else
            {
                // Éxito (Opcional: Descomentar para ver respuesta del servidor)
                // Debug.Log("Enviado: " + request.downloadHandler.text);
            }
        }
    }
}
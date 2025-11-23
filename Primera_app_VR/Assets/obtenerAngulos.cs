using UnityEngine;
using TMPro;
using System;

public class BraccioMonitor80cm : MonoBehaviour
{
    [Header("Referencias de Escena")]
    public Transform objetoA_Rastrear;   
    public Transform objetoOrigen;       
    public TextMeshPro textoAngulos;     

    [Header("Medidas del Brazo (en cm)")]
    public float d1_Base = 10.0f; 
    public float L2_Brazo = 35.0f;
    public float L3_Antebrazo = 35.0f;

    // Variables internas
    private double d1, L2, L3;

    void Update()
    {
        if (objetoA_Rastrear == null || textoAngulos == null || objetoOrigen == null) return;

        // Actualizar medidas
        d1 = (double)d1_Base;
        L2 = (double)L2_Brazo;
        L3 = (double)L3_Antebrazo;

        // 1. Obtener coordenadas
        Vector3 posRelativa = objetoOrigen.InverseTransformPoint(objetoA_Rastrear.position);
        double x_cm = posRelativa.x * 100.0; 
        double y_cm = posRelativa.z * 100.0; 
        double z_cm = posRelativa.y * 100.0; 

        // 2. Calcular y mostrar texto plano
        textoAngulos.text = CalcularIK(x_cm, y_cm, z_cm);
    }

    private string CalcularIK(double x, double y, double z)
    {
        // --- CÁLCULOS MATEMÁTICOS ---
        double m1Rad = Math.Atan2(y, x);
        double m1Deg = m1Rad * 180.0 / Math.PI;
        if (m1Deg < 0) m1Deg += 360;

        double R = Math.Sqrt(x * x + y * y); 
        double Z_rel = z - d1;               
        double D = Math.Sqrt(R * R + Z_rel * Z_rel); 
        double alcanceMaximo = L2 + L3;

        // --- ESTADO DE ERROR (TEXTO PLANO) ---
        if (D > (alcanceMaximo + 0.1))
        {
            return $"ALERTA DE PROXIMIDAD\n" +
                   $"OBJETIVO FUERA DE ALCANCE\n\n" +
                   $"Distancia Actual: {D:F1} cm\n" +
                   $"Alcance Maximo:   {alcanceMaximo:F1} cm";
        }

        // --- CÁLCULO DE ÁNGULOS ---
        double cos_angle3 = (D * D - L2 * L2 - L3 * L3) / (2 * L2 * L3);
        if (cos_angle3 > 1.0) cos_angle3 = 1.0;
        if (cos_angle3 < -1.0) cos_angle3 = -1.0;

        double angle3Rad = Math.Acos(cos_angle3);
        double angle2Rad = Math.Atan2(Z_rel, R) - Math.Atan2(L3 * Math.Sin(angle3Rad), L2 + L3 * Math.Cos(angle3Rad));

        int m1 = (int)m1Deg;
        int m3 = 180 - (int)(angle3Rad * 180.0 / Math.PI);
        int m2 = (int)(angle2Rad * 180.0 / Math.PI) + 90;
        int m4 = 180 - (m2 + m3);

        // --- ESTADO OK (TEXTO PLANO) ---
        return $"ESTADO DEL SISTEMA\n" +
               $"Estado: OPERATIVO\n" +
               $"Distancia: {D:F1} cm\n" +
               $"------------------\n" +
               $"Base (M1):   {m1} grados\n" +
               $"Hombro (M2): {m2} grados\n" +
               $"Codo (M3):   {m3} grados\n" +
               $"Muneca (M4): {m4} grados";
    }
}
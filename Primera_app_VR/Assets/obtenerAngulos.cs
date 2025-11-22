using UnityEngine;
using TMPro;
using System; // Necesario para las matemáticas (Math.Sqrt, etc.)

public class BraccioAngulosRelativos : MonoBehaviour
{
    [Header("Configuración de Escena")]
    public Transform objetoA_Rastrear;   // Tu Cubo/Target (La punta de la pinza)
    public Transform objetoOrigen;       // EL CILINDRO (Tu punto 0,0,0)
    public TextMeshPro textoAngulos;     // El texto donde mostrarás los grados

    [Header("Configuración Física Braccio (cm)")]
    // Ajusta estos valores si tu modelo 3D o físico varía levemente
    private const double d1 = 10.5;  // Altura base
    private const double L2 = 12.0;  // Brazo
    private const double L3 = 14.5;  // Antebrazo

    void Update()
    {
        // Validación de seguridad
        if (objetoA_Rastrear == null || textoAngulos == null || objetoOrigen == null) return;

        // 1. OBTENER POSICIÓN RELATIVA
        // Esto calcula la posición del target "desde el punto de vista" del cilindro.
        // Si mueves el cilindro, el cálculo se adapta automáticamente.
        Vector3 posRelativa = objetoOrigen.InverseTransformPoint(objetoA_Rastrear.position);

        // 2. CONVERSIÓN DE EJES Y UNIDADES (Metros Unity -> CM Braccio)
        // En Unity: Y es Arriba, Z es Frente.
        // En Matemáticas 2D/Arduino usualmente: Z es Altura, Y es Frente/Profundidad.
        
        double x_cm = posRelativa.x * 100.0;
        double y_cm = posRelativa.z * 100.0; // Intercambiamos Z de Unity por Y del algoritmo
        double z_cm = posRelativa.y * 100.0; // Intercambiamos Y de Unity por Z (altura)

        // 3. CÁLCULO DE ÁNGULOS (IK)
        string resultadoTexto = CalcularIK(x_cm, y_cm, z_cm);

        // 4. MOSTRAR EN TEXTO
        textoAngulos.text = resultadoTexto;
    }

    // =====================================================
    // LÓGICA MATEMÁTICA (Cinemática Inversa)
    // =====================================================
    private string CalcularIK(double x, double y, double z)
    {
        // --- Cálculo M1 (Base) ---
        double m1Rad = Math.Atan2(y, x);
        double m1Deg = m1Rad * 180.0 / Math.PI;
        if (m1Deg < 0) m1Deg += 360;

        // --- Cálculos para el plano del brazo ---
        double R = Math.Sqrt(x * x + y * y); // Distancia horizontal desde el centro
        double Z_rel = z - d1;               // Altura relativa al hombro
        double D = Math.Sqrt(R * R + Z_rel * Z_rel); // Distancia directa hombro-muñeca

        // Ley del Coseno (Codo)
        double cos_angle3 = (D * D - L2 * L2 - L3 * L3) / (2 * L2 * L3);

        // VALIDACIÓN: ¿El objeto está físicamente fuera del alcance?
        if (cos_angle3 > 1.0 || cos_angle3 < -1.0)
        {
            return $"FUERA DE RANGO\nTarget a {D:F1}cm\n(Max brazo: {L2+L3}cm)";
        }

        // Calcular ángulos en radianes
        double angle3Rad = Math.Acos(cos_angle3);
        double angle2Rad = Math.Atan2(Z_rel, R) - Math.Atan2(L3 * Math.Sin(angle3Rad), L2 + L3 * Math.Cos(angle3Rad));

        // Convertir a Grados para los Servos
        int m1 = (int)m1Deg;
        int m3 = 180 - (int)(angle3Rad * 180.0 / Math.PI);
        int m2 = (int)(angle2Rad * 180.0 / Math.PI) + 90;
        int m4 = 180 - (m2 + m3); // Muñeca vertical

        // VALIDACIÓN: ¿Los ángulos son seguros para el motor?
        bool m2_bad = m2 < 15 || m2 > 165;
        bool m3_bad = m3 < 0 || m3 > 180;
        bool m4_bad = m4 < 0 || m4 > 180;

        if (m2_bad || m3_bad || m4_bad)
        {
             return $"LIMITES MECÁNICOS\nM2:{m2} M3:{m3} M4:{m4}";
        }

        // SI TODO ESTÁ BIEN:
        return $"ANGULOS BRACCIO\n" +
               $"Base (M1):   {m1}°\n" +
               $"Hombro (M2): {m2}°\n" +
               $"Codo (M3):   {m3}°\n" +
               $"Muñeca (M4): {m4}°";
    }
}
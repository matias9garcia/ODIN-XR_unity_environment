using UnityEngine;
using TMPro; // Necesario para la UI

public class BraccioController : MonoBehaviour {

    [Header("--- Dependencias (Arrastra los otros scripts) ---")]
    public BraccioIK ikCalculator;      // El cerebro matemático
    public BraccioNetwork networker;    // El comunicador

    [Header("--- Configuración de Control ---")]
    public Transform targetObj;         // La esfera/cubo que sigues
    public bool isPinching = false;     // Controlable desde Inspector o Script externo
    
    [Header("--- Configuración de Envío (API) ---")]
    public float tiempoParaEstabilizar = 0.5f;
    public float umbralMovimiento = 0.001f;

    [Header("--- Visuales Unity ---")]
    public GameObject[] arms = new GameObject[5]; // Base, Hombro, Codo, MuñecaV, MuñecaR
    [Range(1f, 20f)] public float velocidadSuavizado = 10f;
    
    [Header("--- Visuales Pinza (Opcional) ---")]
    public Transform visualPinzaIzq;
    public Transform visualPinzaDer;
    public Vector3 ejeRotacionPinza = new Vector3(1, 0, 0);
    public float visualMultiplier = 1.0f;

    [Header("--- UI Debug ---")]
    public TMP_Text textoAngulos;

    // --- Variables de Estado Interno ---
    private Vector3 _ultimaPosicionTarget;
    private float _timerEstabilidad = 0f;
    private bool _datosEnviados = false;
    private bool _ultimoEstadoPinching;
    private string _estadoTextoDebug = "";

    // Variables para la animación de la pinza
    private Quaternion _initialRotIzq;
    private Quaternion _initialRotDer;

    void Start() {
        // Inicializar referencias visuales de la pinza
        if (visualPinzaIzq != null) _initialRotIzq = visualPinzaIzq.localRotation;
        if (visualPinzaDer != null) _initialRotDer = visualPinzaDer.localRotation;

        _ultimoEstadoPinching = isPinching;
        
        // Inicializar posición para que no envíe datos apenas arranca
        if (targetObj != null) _ultimaPosicionTarget = targetObj.position;
    }

    void Update() {
        if (targetObj == null || ikCalculator == null || networker == null) return;

        // ---------------------------------------------------------
        // PASO 1: Calcular Matemáticas (Delegado a BraccioIK)
        // ---------------------------------------------------------
        ikCalculator.CalcularIK(targetObj.position, isPinching, targetObj.rotation);

        // ---------------------------------------------------------
        // PASO 2: Actualizar Visuales en Unity
        // ---------------------------------------------------------
        MoverVisualesUnity();

        // ---------------------------------------------------------
        // PASO 3: Gestionar Lógica de Envío (Red)
        // ---------------------------------------------------------
        GestionarEnvioRed();

        // ---------------------------------------------------------
        // PASO 4: Actualizar UI
        // ---------------------------------------------------------
        ActualizarUI();
    }

    void MoverVisualesUnity() {
        // Leemos las propiedades calculadas por BraccioIK
        // NOTA: Mantenemos los offsets (-90) que tenías en tu código original para que coincida visualmente
        
        // M1: Base
        arms[0].transform.localRotation = Quaternion.Slerp(arms[0].transform.localRotation, 
            Quaternion.Euler(0, ikCalculator.ThetaBase, 0), Time.deltaTime * velocidadSuavizado);
        
        // M2: Hombro
        arms[1].transform.localRotation = Quaternion.Slerp(arms[1].transform.localRotation, 
            Quaternion.Euler(0, 0, ikCalculator.ThetaShoulder - 90), Time.deltaTime * velocidadSuavizado);
        
        // M3: Codo
        arms[2].transform.localRotation = Quaternion.Slerp(arms[2].transform.localRotation, 
            Quaternion.Euler(0, 0, ikCalculator.ThetaElbow - 90), Time.deltaTime * velocidadSuavizado);
        
        // M4: Muñeca Vertical
        arms[3].transform.localRotation = Quaternion.Slerp(arms[3].transform.localRotation, 
            Quaternion.Euler(0, 0, ikCalculator.ThetaWristVertical - 90), Time.deltaTime * velocidadSuavizado);
        
        // M5: Muñeca Rotación
        arms[4].transform.localRotation = Quaternion.Slerp(arms[4].transform.localRotation, 
            Quaternion.Euler(0, ikCalculator.ThetaWristRotation, 0), Time.deltaTime * velocidadSuavizado);

        // M6: Pinza (Visuales complejas de las dos partes)
        if (visualPinzaIzq != null && visualPinzaDer != null) {
            float anguloVisual = (ikCalculator.ThetaGripper - ikCalculator.anguloCerrado) * visualMultiplier;
                        
            Quaternion rotacionApertura = Quaternion.Euler(ejeRotacionPinza * anguloVisual);
                    
            visualPinzaIzq.localRotation = Quaternion.Slerp(visualPinzaIzq.localRotation, 
                _initialRotIzq * rotacionApertura, Time.deltaTime * velocidadSuavizado);
                        
            visualPinzaDer.localRotation = Quaternion.Slerp(visualPinzaDer.localRotation, 
                _initialRotDer * rotacionApertura, Time.deltaTime * velocidadSuavizado);
        }
    }

    void GestionarEnvioRed() {
        // CASO A: Cambio inmediato si se aprieta la pinza
        if (isPinching != _ultimoEstadoPinching) {
            EnviarAhora();
            _ultimoEstadoPinching = isPinching;
            _estadoTextoDebug = "<color=#FF00FF><b>GRIPPER ACTION SENT</b></color>";
            return;
        }

        // CASO B: Detección de movimiento y estabilización
        float distancia = Vector3.Distance(targetObj.position, _ultimaPosicionTarget);

        if (distancia > umbralMovimiento) {
            // Se está moviendo
            _timerEstabilidad = 0f;
            _datosEnviados = false;
            _ultimaPosicionTarget = targetObj.position;
            _estadoTextoDebug = "<color=yellow>MOVING...</color>";
        } 
        else {
            // Está quieto
            _timerEstabilidad += Time.deltaTime;
            
            if (_timerEstabilidad < tiempoParaEstabilizar) {
                if (!_datosEnviados)
                    _estadoTextoDebug = $"<color=orange>STABILIZING... {(_timerEstabilidad/tiempoParaEstabilizar)*100:F0}%</color>";
            }
            else {
                // Tiempo cumplido, enviar si no se ha enviado ya
                if (!_datosEnviados) {
                    EnviarAhora();
                    _datosEnviados = true;
                    _estadoTextoDebug = "<color=#FF00FF><b>SENT (IDLE)</b></color>";
                }
            }
        }
    }

    void EnviarAhora() {
        // Aquí es donde el Controller usa al Networker
        networker.EnviarDatos(
            (int)ikCalculator.ThetaBase,
            (int)ikCalculator.ThetaShoulder,
            (int)ikCalculator.ThetaElbow,
            (int)ikCalculator.ThetaWristVertical,
            (int)ikCalculator.ThetaWristRotation,
            (int)ikCalculator.ThetaGripper,
            ikCalculator.EstadoPinzaLogico
        );
    }

    void ActualizarUI() {
        if (textoAngulos == null) return;

        string textoEstadoPinza = isPinching 
            ? $"<color=purple><b>Closed ({(int)ikCalculator.ThetaGripper}°)</b></color>" 
            : $"<color=purple>Open ({(int)ikCalculator.ThetaGripper}°)</color>";

        textoAngulos.text = 
            $"{_estadoTextoDebug}\n" + 
            $"<color=purple>STATUS: {textoEstadoPinza}</color>\n" + 
            $"<color=purple></color>\n" +
            $"<color=purple>M1 (Base):       <b>{(int)ikCalculator.ThetaBase}°</b></color>\n" +
            $"<color=purple>M2 (Shoulder):   <b>{(int)ikCalculator.ThetaShoulder}°</b></color>\n" +
            $"<color=purple>M3 (Elbow):      <b>{(int)ikCalculator.ThetaElbow}°</b></color>\n" +
            $"<color=purple>M4 (Wrist V):    <b>{(int)ikCalculator.ThetaWristVertical}°</b></color>\n" +
            $"<color=purple>M5 (Wrist R):    <b>{(int)ikCalculator.ThetaWristRotation}°</b></color>\n" +
            $"<color=purple>M6 (Gripper):    <b>{(int)ikCalculator.ThetaGripper}°</b></color>"; 
    }
}
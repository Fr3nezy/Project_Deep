using Deeploration.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Environment
{
    /// <summary>
    /// Fotocellula delle porte di Tidal Dynamics. Scatta col fascio rosso della torcia del diver (modalità
    /// <see cref="FlashlightMode.Red"/>, a qualunque intensità) tenuto sulla cella per <c>redHoldSeconds</c>, oppure con una luce in <c>extraLights</c> (per esempio
    /// un faro di sicurezza) di intensità almeno <c>minSourceIntensity</c>. In entrambi i casi la cella deve stare entro
    /// la portata della luce e, per gli spot, dentro il cono, senza collider (trigger esclusi) in mezzo.
    /// </summary>
    public sealed class Photocell : MonoBehaviour
    {
        [Tooltip("Intensità minima delle extraLights. Non vale per la torcia del diver, che scatta solo in rosso.")]
        [SerializeField, Min(0f)] private float minSourceIntensity = 200f;

        [Tooltip("Secondi di fascio rosso sulla cella per farla scattare. Se il fascio esce la carica scende alla stessa velocità.")]
        [SerializeField, Min(0.01f)] private float redHoldSeconds = 1.5f;

        [Tooltip("Stazione che alimenta la cella: finché è spenta la cella non scatta. Vuoto: sempre alimentata.")]
        [SerializeField] private OxygenRefillStation powerSource;

        [Tooltip("Luci della scena che possono far scattare la cella, oltre alla torcia del diver.")]
        [SerializeField] private Light[] extraLights = new Light[0];

        [Tooltip("Collider che bloccano la luce.")]
        [SerializeField] private LayerMask occluders = ~0;

        [Tooltip("Spia della cella: lockedColor finché non scatta, triggeredColor dopo. Opzionale.")]
        [SerializeField] private Light indicatorLight;
        [SerializeField] private Color lockedColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color triggeredColor = new Color(0.2f, 1f, 0.5f);

        public UnityEvent onTriggered = new UnityEvent();

        [Header("Debug (sola lettura)")]
        [SerializeField] private bool triggered;

        private DiverFlashlight flashlight;

        public bool Triggered => triggered;

        /// <summary>Carica del fascio rosso, 0-1: a 1 la cella scatta. Resta a 0 senza corrente, 1 dopo lo scatto.</summary>
        public float RedCharge { get; private set; }

        /// <summary>Modalità della torcia il cui fascio colpisce la cella in questo frame, null se non la colpisce.</summary>
        public FlashlightMode? BeamModeOnCell { get; private set; }

        /// <summary>Vero se la stazione che la alimenta è accesa (o se non ne ha una).</summary>
        public bool Powered => powerSource == null || powerSource.IsPowered;

        /// <summary>Luce più forte che arriva alla cella in questo frame, anche sotto soglia (null se nessuna).</summary>
        public Light BrightestLight { get; private set; }

        /// <summary>Punteggio di <see cref="BrightestLight"/> (vedi <see cref="ReceivedLight"/>), 0 se nessuna.</summary>
        public float BrightestScore { get; private set; }

        private void Awake()
        {
            flashlight = FindAnyObjectByType<DiverFlashlight>();
            if (indicatorLight != null) indicatorLight.color = lockedColor;
        }

        private void Update()
        {
            UpdateBrightest();
            if (triggered) return;
            if (!Powered)
            {
                RedCharge = 0f;
                return;
            }

            RedCharge = Mathf.MoveTowards(RedCharge, IsLitByRedBeam() ? 1f : 0f, Time.deltaTime / redHoldSeconds);
            if (RedCharge >= 1f)
            {
                Trigger();
                return;
            }

            foreach (Light light in extraLights)
            {
                if (!IsLitBy(light)) continue;
                Trigger();
                break;
            }
        }

        /// <summary>Fa scattare la cella come se fosse stata colpita (una volta sola).</summary>
        public void Trigger()
        {
            if (triggered) return;
            triggered = true;
            RedCharge = 1f;
            if (indicatorLight != null) indicatorLight.color = triggeredColor;
            onTriggered?.Invoke();
        }

        /// <summary>Vero se la torcia del diver è in rosso e il fascio colpisce la cella (letto dal frame corrente).</summary>
        public bool IsLitByRedBeam() => BeamModeOnCell == FlashlightMode.Red;

        /// <summary>Vero se la luce, così com'è in questo frame, colpisce la cella sopra la soglia.</summary>
        public bool IsLitBy(Light light) => light != null && light.intensity >= minSourceIntensity && Reaches(light);

        private bool Reaches(Light light)
        {
            if (light == null || !light.isActiveAndEnabled) return false;

            Vector3 toCell = transform.position - light.transform.position;
            float distance = toCell.magnitude;
            if (distance > light.range) return false;
            if (light.type == LightType.Spot && Vector3.Angle(light.transform.forward, toCell) > light.spotAngle * 0.5f)
                return false;
            return HasLineOfSight(light, toCell, distance);
        }

        /// <summary>
        /// Luce che arriva alla cella da questa sorgente, senza soglia: intensità × caduta con la distanza
        /// ((1 − d/range)²) × posizione nel cono per gli spot (1 al centro, 0 al bordo). 0 se spenta, fuori
        /// portata, fuori cono o coperta. La torcia in Full vicina dà qualche decina.
        /// </summary>
        public float ReceivedLight(Light light)
        {
            if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f) return 0f;

            Vector3 toCell = transform.position - light.transform.position;
            float distance = toCell.magnitude;
            if (distance > light.range || distance < 0.0001f) return 0f;

            float cone = 1f;
            if (light.type == LightType.Spot)
            {
                float halfAngle = light.spotAngle * 0.5f;
                cone = 1f - Vector3.Angle(light.transform.forward, toCell) / halfAngle;
                if (cone <= 0f) return 0f;
            }

            float falloff = 1f - distance / light.range;
            if (!HasLineOfSight(light, toCell, distance)) return 0f;
            return light.intensity * falloff * falloff * cone;
        }

        private void UpdateBrightest()
        {
            BrightestLight = null;
            BrightestScore = 0f;
            BeamModeOnCell = null;
            if (flashlight != null && Consider(flashlight.SpotLight) > 0f) BeamModeOnCell = flashlight.CurrentMode;
            foreach (Light light in extraLights) Consider(light);
        }

        private float Consider(Light light)
        {
            float score = ReceivedLight(light);
            if (score <= BrightestScore) return score;
            BrightestScore = score;
            BrightestLight = light;
            return score;
        }

        // Linea di vista dalla cella verso la luce. Si ignorano la cella con i suoi figli (guscio, occhio),
        // i collider che contengono la luce (la capsula del diver per la torcia) e i trigger.
        private bool HasLineOfSight(Light light, Vector3 toCell, float distance)
        {
            RaycastHit[] hits = Physics.RaycastAll(transform.position, -toCell / distance, distance, occluders,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || light.transform.IsChildOf(hit.collider.transform))
                    continue;
                return false;
            }
            return true;
        }
    }
}

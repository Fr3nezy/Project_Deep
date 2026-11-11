using Deeploration.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Environment
{
    /// <summary>
    /// Fotocellula delle porte di Tidal Dynamics: scatta quando la colpisce una luce abbastanza forte.
    /// Guarda il fascio della torcia del diver (forte solo durante il lampo del flash) e le luci in
    /// <c>extraLights</c> (per esempio un faro di sicurezza). Una luce conta se è accesa, ha intensità almeno
    /// <c>minSourceIntensity</c>, la cella è entro la sua portata e, per gli spot, dentro il cono, e nessun
    /// collider (trigger esclusi) sta in mezzo.
    /// </summary>
    public sealed class Photocell : MonoBehaviour
    {
        [Tooltip("Intensità minima della sorgente. La torcia in Full è 55, il lampo del flash 800.")]
        [SerializeField, Min(0f)] private float minSourceIntensity = 200f;

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

            if (flashlight != null && IsLitBy(flashlight.SpotLight)) Trigger();
            else
            {
                foreach (Light light in extraLights)
                {
                    if (!IsLitBy(light)) continue;
                    Trigger();
                    break;
                }
            }
        }

        /// <summary>Fa scattare la cella come se fosse stata colpita (una volta sola).</summary>
        public void Trigger()
        {
            if (triggered) return;
            triggered = true;
            if (indicatorLight != null) indicatorLight.color = triggeredColor;
            onTriggered?.Invoke();
        }

        /// <summary>Vero se la luce, così com'è in questo frame, colpisce la cella sopra la soglia.</summary>
        public bool IsLitBy(Light light)
        {
            if (light == null || !light.isActiveAndEnabled || light.intensity < minSourceIntensity) return false;

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
        /// portata, fuori cono o coperta. La torcia in Full vicina dà qualche decina, il lampo centinaia.
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
            if (flashlight != null) Consider(flashlight.SpotLight);
            foreach (Light light in extraLights) Consider(light);
        }

        private void Consider(Light light)
        {
            float score = ReceivedLight(light);
            if (score <= BrightestScore) return;
            BrightestScore = score;
            BrightestLight = light;
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

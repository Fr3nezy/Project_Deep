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

        private void Awake()
        {
            flashlight = FindAnyObjectByType<DiverFlashlight>();
            if (indicatorLight != null) indicatorLight.color = lockedColor;
        }

        private void Update()
        {
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

            // Linea di vista dalla cella verso la luce. Si ignorano i collider che contengono la luce
            // (la capsula del diver per la torcia) e i trigger.
            RaycastHit[] hits = Physics.RaycastAll(transform.position, -toCell / distance, distance, occluders,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform == transform || light.transform.IsChildOf(hit.collider.transform)) continue;
                return false;
            }
            return true;
        }
    }
}

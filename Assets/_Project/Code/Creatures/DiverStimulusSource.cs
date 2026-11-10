using System.Collections.Generic;
using Deeploration.Environment;
using Deeploration.Player;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Traduce lo stato del sub in stimoli per le creature, una volta per frame su richiesta del
    /// <see cref="CreatureManager"/>:
    /// <list type="bullet">
    /// <item>luce: il fascio della torcia, forte in Full e debole in Low (in Red le luci dello scafandro sono spente e
    /// la spia rossa è invisibile alle creature);</item>
    /// <item>flash: per un attimo dopo ogni passaggio a Full da un'altra modalità;</item>
    /// <item>rumore: dalla velocità del diver, con un picco quando atterra;</item>
    /// <item>scia del prato di penne: ogni tocco di <see cref="SeaPenTouchGlow"/> vicino a una penna di mare
    /// diventa un punto luce debole che si spegne come il glow dello shader.</item>
    /// </list>
    /// Va sul <c>PlayerCapsule</c>, accanto a <see cref="DiverFlashlight"/>, <see cref="DiverController"/> e
    /// <see cref="SeaPenTouchGlow"/>. Le portate sono per una creatura con sensibilità 1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiverStimulusSource : MonoBehaviour, IStimulusSource
    {
        private const string TouchGlowKeyword = "_CORAL_TOUCH_GLOW";
        private const int MaxSeaPenGlows = 12;

        [Header("Riferimenti (vuoti = cercati su questo GameObject)")]
        [SerializeField] private DiverFlashlight flashlight;
        [SerializeField] private DiverController diver;
        [SerializeField] private SeaPenTouchGlow seaPenTouch;

        [Header("Luce della torcia")]
        [Tooltip("Portata del fascio Full per le creature (m). Più lunga della portata visiva: un fascio si vede da lontano.")]
        [SerializeField, Min(0f)] private float fullLightReach = 25f;

        [Tooltip("Portata del fascio Low per le creature (m).")]
        [SerializeField, Min(0f)] private float lowLightReach = 8f;

        [Tooltip("Frazione della portata entro cui la lampada si vede anche da fuori del fascio.")]
        [SerializeField, Range(0f, 1f)] private float haloFraction = 0.4f;

        [Header("Flash")]
        [Tooltip("Secondi per cui il passaggio a Full conta come flash.")]
        [SerializeField, Min(0.05f)] private float flashDuration = 0.4f;

        [Tooltip("Frazione della portata di Full entro cui il flash si vede anche da fuori del fascio.")]
        [SerializeField, Range(0f, 1f)] private float flashHaloFraction = 0.6f;

        [Header("Rumore")]
        [Tooltip("Metri di portata del rumore per ogni m/s di velocità del diver.")]
        [SerializeField, Min(0f)] private float noisePerSpeed = 2.5f;

        [Tooltip("Portata del rumore nell'istante in cui il diver atterra (m).")]
        [SerializeField, Min(0f)] private float landingNoise = 8f;

        [Tooltip("Secondi in cui il rumore dell'atterraggio si spegne.")]
        [SerializeField, Min(0.01f)] private float landingNoiseDuration = 1f;

        [Header("Scia del prato di penne")]
        [Tooltip("Portata della luce di un tocco appena acceso (m); cala fino a zero in seaPenGlowDuration.")]
        [SerializeField, Min(0f)] private float seaPenGlowReach = 6f;

        [Tooltip("Durata della luce di un tocco (s). Allineata a _TouchLifetime dello shader Coral (2.5).")]
        [SerializeField, Min(0.01f)] private float seaPenGlowDuration = 2.5f;

        [Tooltip("Distanza massima fra un tocco e una penna di mare perché il tocco la accenda (m). Allineata a " +
                 "_TouchRadius dei materiali delle penne (1.6).")]
        [SerializeField, Min(0f)] private float seaPenTouchRadius = 1.6f;

        [Tooltip("Lato delle celle della griglia che ricorda dove ci sono penne di mare (m).")]
        [SerializeField, Min(0.25f)] private float seaPenCellSize = 1f;

        private readonly HashSet<Vector2Int> seaPenCells = new HashSet<Vector2Int>();
        private readonly Vector3[] glowPositions = new Vector3[MaxSeaPenGlows];
        private readonly float[] glowTimes = new float[MaxSeaPenGlows];
        private int nextGlow;

        private bool wasGrounded = true;
        private float landingTime = float.NegativeInfinity;
        private FlashlightMode lastMode;
        private float flashTime = float.NegativeInfinity;

        /// <summary>Celle della griglia (lato <c>seaPenCellSize</c>) in cui un tocco accende il prato.</summary>
        public int SeaPenCellCount => seaPenCells.Count;

        private void Awake()
        {
            if (flashlight == null) flashlight = GetComponent<DiverFlashlight>();
            if (diver == null) diver = GetComponent<DiverController>();
            if (seaPenTouch == null) seaPenTouch = GetComponent<SeaPenTouchGlow>();
            for (int i = 0; i < MaxSeaPenGlows; i++) glowTimes[i] = float.NegativeInfinity;
        }

        private void Start() => BuildSeaPenGrid();

        private void OnEnable()
        {
            if (seaPenTouch != null) seaPenTouch.Touched += HandleTouched;
            if (flashlight != null)
            {
                lastMode = flashlight.CurrentMode;
                flashlight.OnModeChanged += HandleModeChanged;
                flashlight.OnFlashFired += HandleFlashFired;
            }
        }

        private void OnDisable()
        {
            if (seaPenTouch != null) seaPenTouch.Touched -= HandleTouched;
            if (flashlight != null)
            {
                flashlight.OnModeChanged -= HandleModeChanged;
                flashlight.OnFlashFired -= HandleFlashFired;
            }
        }

        private void HandleFlashFired() => flashTime = Time.time;

        private void HandleModeChanged(FlashlightMode mode)
        {
            if (mode == FlashlightMode.Full && lastMode != FlashlightMode.Full) flashTime = Time.time;
            lastMode = mode;
        }

        private void Update()
        {
            if (diver == null) return;
            bool grounded = diver.IsGrounded;
            if (grounded && !wasGrounded) landingTime = Time.time;
            wasGrounded = grounded;
        }

        /// <inheritdoc />
        public void EmitStimuli(List<CreatureStimulus> output)
        {
            if (!isActiveAndEnabled) return;

            if (flashlight != null && flashlight.SpotLight != null)
            {
                Transform lamp = flashlight.SpotLight.transform;
                float halfAngle = flashlight.SpotLight.spotAngle * 0.5f;
                if (flashlight.CurrentMode != FlashlightMode.Red)
                {
                    bool full = flashlight.CurrentMode == FlashlightMode.Full;
                    float reach = full ? fullLightReach : lowLightReach;
                    output.Add(CreatureStimulus.Beam(lamp.position, lamp.forward, reach, halfAngle, reach * haloFraction,
                        full ? StimulusCategory.StrongLight : StimulusCategory.WeakLight, true));
                }

                // Flash: passaggio a Full da un'altra modalità oppure lampo del modulo (anche da Red).
                bool flashing = flashlight.CurrentMode == FlashlightMode.Full || flashlight.IsFlashing;
                if (flashing && Time.time - flashTime < flashDuration)
                    output.Add(CreatureStimulus.Beam(lamp.position, lamp.forward, fullLightReach, halfAngle,
                        fullLightReach * flashHaloFraction, StimulusCategory.Flash, true));
            }

            float noise = 0f;
            if (diver != null && diver.enabled) noise = diver.CurrentSpeed * noisePerSpeed;
            float sinceLanding = Time.time - landingTime;
            if (sinceLanding < landingNoiseDuration)
                noise = Mathf.Max(noise, landingNoise * (1f - sinceLanding / landingNoiseDuration));
            if (noise > 0.05f) output.Add(CreatureStimulus.Noise(transform.position, noise, true));

            for (int i = 0; i < MaxSeaPenGlows; i++)
            {
                float age = Time.time - glowTimes[i];
                if (age >= seaPenGlowDuration) continue;
                output.Add(CreatureStimulus.PointLight(glowPositions[i],
                    seaPenGlowReach * (1f - age / seaPenGlowDuration), StimulusCategory.WeakLight, true));
            }
        }

        private void HandleTouched(Vector3 position)
        {
            if (!seaPenCells.Contains(Cell(position))) return;

            // Il punto di tocco è a livello del fondale: lo alzo un po' perché il raggio di linea di vista non
            // parta da dentro il terreno.
            glowPositions[nextGlow] = position + Vector3.up * 0.3f;
            glowTimes[nextGlow] = Time.time;
            nextGlow = (nextGlow + 1) % MaxSeaPenGlows;
        }

        /// <summary>
        /// Segna le celle attorno a ogni renderer con un materiale Touch Glow (le penne di mare), allargate di
        /// <c>seaPenTouchRadius</c>. Una volta sola all'avvio: le penne sono statiche.
        /// </summary>
        private void BuildSeaPenGrid()
        {
            seaPenCells.Clear();
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!HasTouchGlow(renderer)) continue;

                Bounds bounds = renderer.bounds;
                bounds.Expand(seaPenTouchRadius * 2f);
                Vector2Int min = Cell(bounds.min);
                Vector2Int max = Cell(bounds.max);
                for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    seaPenCells.Add(new Vector2Int(x, y));
            }
        }

        private static bool HasTouchGlow(Renderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.IsKeywordEnabled(TouchGlowKeyword)) return true;
            return false;
        }

        private Vector2Int Cell(Vector3 position) => new Vector2Int(
            Mathf.FloorToInt(position.x / seaPenCellSize), Mathf.FloorToInt(position.z / seaPenCellSize));

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 1f, 0.4f, 0.8f);
            for (int i = 0; i < MaxSeaPenGlows; i++)
                if (Application.isPlaying && Time.time - glowTimes[i] < seaPenGlowDuration)
                    Gizmos.DrawWireSphere(glowPositions[i], 0.3f);
        }
    }
}

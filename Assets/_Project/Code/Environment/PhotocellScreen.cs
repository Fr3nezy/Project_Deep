using Deeploration.Player;
using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Schermo CRT ambra accanto alla fotocellula. Spento finché la stazione che lo alimenta non ha corrente, poi
    /// mostra senza testo cosa vuole la <see cref="Photocell"/>: in attesa l'occhio chiuso e l'icona della luce rossa
    /// che pulsa; con la luce sbagliata l'icona barrata della modalità della torcia che colpisce la cella; col rosso
    /// l'occhio si apre e un anello si riempie con <see cref="Photocell.RedCharge"/>; allo scatto il lucchetto lampeggia
    /// e si apre. Solo lettura: non tocca la cella.
    /// </summary>
    public sealed class PhotocellScreen : MonoBehaviour
    {
        [SerializeField] private Photocell photocell;
        [Tooltip("Stazione che alimenta lo schermo. Vuoto: sempre acceso.")]
        [SerializeField] private OxygenRefillStation powerSource;

        [Header("Schermo")]
        [Tooltip("Oggetto con le icone, acceso solo con la corrente.")]
        [SerializeField] private GameObject content;
        [Tooltip("Fondo dello schermo: emissione accesa solo con la corrente. Opzionale.")]
        [SerializeField] private Renderer background;
        [SerializeField, ColorUsage(false, true)] private Color backgroundGlow = new Color(0.12f, 0.06f, 0.01f);
        [SerializeField] private Color amber = new Color(1f, 0.6f, 0.15f, 1f);

        [Header("Occhio")]
        [SerializeField] private Renderer eyeIcon;
        [Tooltip("Fotogrammi dell'occhio da chiuso ad aperto.")]
        [SerializeField] private Texture2D[] eyeFrames = new Texture2D[0];
        [Tooltip("Velocità con cui l'occhio si apre col rosso e si richiude senza, frazione al secondo.")]
        [SerializeField, Min(0f)] private float eyeResponse = 6f;

        [Header("Modalità della torcia")]
        [Tooltip("Icona della luce richiesta, o della luce sbagliata barrata. Nascosta dopo lo scatto.")]
        [SerializeField] private Renderer modeIcon;
        [SerializeField] private Texture2D redIcon;
        [SerializeField] private Texture2D fullBarredIcon;
        [SerializeField] private Texture2D lowBarredIcon;
        [Tooltip("Pulsazioni al secondo dell'icona rossa in attesa.")]
        [SerializeField, Min(0.1f)] private float pulseRate = 1.2f;
        [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.25f;
        [Tooltip("Colore dell'icona della luce rossa: unica icona non ambra.")]
        [SerializeField] private Color redTint = new Color(0.85f, 0.13f, 0.14f, 1f);

        [Header("Anello di carica")]
        [Tooltip("Visibile solo mentre la carica è sopra 0; piena dopo lo scatto.")]
        [SerializeField] private Renderer chargeRing;
        [Tooltip("Fotogrammi dell'anello, dal primo spicchio al cerchio pieno.")]
        [SerializeField] private Texture2D[] ringFrames = new Texture2D[0];

        [Header("Lucchetto")]
        [SerializeField] private Renderer lockIcon;
        [SerializeField] private Texture2D lockClosed;
        [SerializeField] private Texture2D lockOpen;
        [SerializeField, Min(0f)] private float blinkDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float blinkRate = 6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;
        private float eyeOpen;
        private float triggeredAt = -1f;
        private bool wasPowered;
        private bool initialized;

        public bool IsPowered => powerSource == null || powerSource.IsPowered;

        private void Awake()
        {
            if (photocell == null) photocell = FindAnyObjectByType<Photocell>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            bool powered = IsPowered;
            if (!initialized || powered != wasPowered)
            {
                initialized = true;
                wasPowered = powered;
                if (content != null) content.SetActive(powered);
                if (background != null)
                {
                    background.GetPropertyBlock(block);
                    block.SetColor(EmissionColorId, powered ? backgroundGlow : Color.black);
                    background.SetPropertyBlock(block);
                }
            }
            if (!powered) return;

            bool triggered = photocell != null && photocell.Triggered;
            if (triggered && triggeredAt < 0f) triggeredAt = Time.time;
            float charge = photocell != null ? photocell.RedCharge : 0f;
            FlashlightMode? beam = photocell != null ? photocell.BeamModeOnCell : null;

            eyeOpen = Mathf.MoveTowards(eyeOpen, triggered || beam == FlashlightMode.Red ? 1f : 0f,
                eyeResponse * Time.deltaTime);
            if (eyeFrames.Length > 0)
                SetIcon(eyeIcon, eyeFrames[Mathf.RoundToInt(eyeOpen * (eyeFrames.Length - 1))], 1f);

            if (chargeRing != null)
            {
                bool ringOn = charge > 0f && ringFrames.Length > 0;
                chargeRing.enabled = ringOn;
                if (ringOn)
                {
                    int frame = Mathf.Clamp(Mathf.CeilToInt(charge * ringFrames.Length) - 1, 0, ringFrames.Length - 1);
                    SetIcon(chargeRing, ringFrames[frame], 1f);
                }
            }

            if (modeIcon != null)
            {
                modeIcon.enabled = !triggered;
                if (!triggered)
                {
                    Texture2D icon = beam == FlashlightMode.Full ? fullBarredIcon
                        : beam == FlashlightMode.Low ? lowBarredIcon : redIcon;
                    float pulse = 0.5f + 0.5f * Mathf.Cos(Time.time * pulseRate * 2f * Mathf.PI);
                    SetIcon(modeIcon, icon, beam == null ? Mathf.Lerp(pulseMinAlpha, 1f, pulse) : 1f, icon == redIcon ? redTint : amber);
                }
            }

            bool open = triggered && Time.time - triggeredAt >= blinkDuration;
            bool blinkOff = triggered && !open && Mathf.Repeat((Time.time - triggeredAt) * blinkRate, 1f) > 0.5f;
            SetIcon(lockIcon, open ? lockOpen : lockClosed, blinkOff ? 0.15f : 1f);
        }

        private void SetIcon(Renderer target, Texture2D texture, float alpha) => SetIcon(target, texture, alpha, amber);

        private void SetIcon(Renderer target, Texture2D texture, float alpha, Color tint)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            if (texture != null) block.SetTexture(BaseMapId, texture);
            block.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, tint.a * alpha));
            target.SetPropertyBlock(block);
        }
    }
}

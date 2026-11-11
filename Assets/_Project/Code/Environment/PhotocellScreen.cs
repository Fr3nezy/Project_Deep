using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Schermo CRT ambra accanto alla fotocellula. Spento finché la stazione che lo alimenta non ha corrente, poi
    /// mostra senza testo sulla luce: una barra a segmenti con la luce ricevuta dalla <see cref="Photocell"/>
    /// (<see cref="Photocell.BrightestScore"/>, con la tacca di soglia nel modello), un occhio che si apre con la luce e
    /// un lucchetto che allo scatto lampeggia e poi si apre. Solo lettura: non tocca la cella.
    /// </summary>
    public sealed class PhotocellScreen : MonoBehaviour
    {
        [SerializeField] private Photocell photocell;
        [Tooltip("Stazione che alimenta lo schermo. Vuoto: sempre acceso.")]
        [SerializeField] private OxygenRefillStation powerSource;

        [Header("Schermo")]
        [Tooltip("Oggetto con barra e icone, acceso solo con la corrente.")]
        [SerializeField] private GameObject content;
        [Tooltip("Fondo dello schermo: emissione accesa solo con la corrente. Opzionale.")]
        [SerializeField] private Renderer background;
        [SerializeField, ColorUsage(false, true)] private Color backgroundGlow = new Color(0.12f, 0.06f, 0.01f);
        [SerializeField] private Color amber = new Color(1f, 0.6f, 0.15f, 1f);

        [Header("Barra")]
        [Tooltip("Segmenti da sinistra a destra.")]
        [SerializeField] private Renderer[] barSegments = new Renderer[0];
        [Tooltip("Punteggio di luce ricevuta che riempie la barra. La torcia vicina fa 10-20, il lampo oltre 100.")]
        [SerializeField, Min(0.01f)] private float fullScore = 60f;
        [SerializeField, Range(0f, 1f)] private float unlitSegmentAlpha = 0.12f;
        [Tooltip("Velocità con cui la barra segue la luce, frazione al secondo.")]
        [SerializeField, Min(0f)] private float response = 4f;

        [Header("Icone")]
        [SerializeField] private Renderer eyeIcon;
        [Tooltip("Fotogrammi dell'occhio da chiuso ad aperto.")]
        [SerializeField] private Texture2D[] eyeFrames = new Texture2D[0];
        [SerializeField] private Renderer lockIcon;
        [SerializeField] private Texture2D lockClosed;
        [SerializeField] private Texture2D lockOpen;
        [SerializeField, Min(0f)] private float blinkDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float blinkRate = 6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;
        private float fill;
        private float triggeredAt = -1f;
        private bool wasPowered;
        private bool initialized;

        /// <summary>Riempimento attuale della barra, 0-1.</summary>
        public float Fill => fill;

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

            float score = photocell != null ? photocell.BrightestScore : 0f;
            float target = triggered ? 1f : Mathf.Clamp01(score / fullScore);
            fill = Mathf.MoveTowards(fill, target, response * Time.deltaTime);

            int lit = Mathf.RoundToInt(fill * barSegments.Length);
            for (int i = 0; i < barSegments.Length; i++) SetIcon(barSegments[i], null, i < lit ? 1f : unlitSegmentAlpha);

            if (eyeFrames.Length > 0)
            {
                int frame = Mathf.Clamp(Mathf.RoundToInt(fill * (eyeFrames.Length - 1)), 0, eyeFrames.Length - 1);
                SetIcon(eyeIcon, eyeFrames[frame], 1f);
            }

            bool open = triggered && Time.time - triggeredAt >= blinkDuration;
            bool blinkOff = triggered && !open && Mathf.Repeat((Time.time - triggeredAt) * blinkRate, 1f) > 0.5f;
            SetIcon(lockIcon, open ? lockOpen : lockClosed, blinkOff ? 0.15f : 1f);
        }

        private void SetIcon(Renderer target, Texture2D texture, float alpha)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            if (texture != null) block.SetTexture(BaseMapId, texture);
            block.SetColor(BaseColorId, new Color(amber.r, amber.g, amber.b, amber.a * alpha));
            target.SetPropertyBlock(block);
        }
    }
}

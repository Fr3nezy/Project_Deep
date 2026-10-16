using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Fotofori che cambiano con l'umore della creatura: moltiplica l'emissione del materiale (la mappa dà la forma
    /// dei puntini) con un colore e una pulsazione che dipendono da stato e aggressività. Calma: colore della mappa
    /// (arancione per il viperfish), pulsazione lenta. Aggressività piena: rosso, pulsazione veloce. Slancio:
    /// rosso acceso che lampeggia. In caccia l'umore non scende sotto <c>huntMood</c>, anche lontano dal sub. Il colore e l'intensità cambiano insieme, così il segnale si legge anche dove il
    /// rosso si confonde (luce rossa della torcia, daltonismo). I colori sono moltiplicatori lineari, come
    /// l'_EmissionColor salvato nel materiale: (8, 8, 8) = mappa per 8.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureGlow : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Renderer con il materiale emissivo (URP Lit con _EMISSION).")]
        [SerializeField] private Renderer[] renderers;

        [Tooltip("Moltiplicatore dell'emissione a riposo (HDR). Bianco = colore della mappa.")]
        [SerializeField, ColorUsage(false, true)] private Color calmColor = new Color(8f, 8f, 8f);

        [Tooltip("Moltiplicatore ad aggressività piena (HDR): sposta la mappa arancione verso il rosso. Tenere il rosso " +
                 "molto sopra verde e blu, altrimenti resta arancione.")]
        [SerializeField, ColorUsage(false, true)] private Color angryColor = new Color(16f, 2.5f, 2f);

        [Tooltip("Moltiplicatore durante lo slancio del morso (HDR).")]
        [SerializeField, ColorUsage(false, true)] private Color strikeColor = new Color(28f, 2.5f, 2f);

        [Tooltip("Pulsazioni al secondo a riposo e ad aggressività piena.")]
        [SerializeField] private Vector2 pulseFrequency = new Vector2(0.25f, 2f);

        [Tooltip("Profondità della pulsazione (0..1) a riposo e ad aggressività piena.")]
        [SerializeField] private Vector2 pulseDepth = new Vector2(0.3f, 0.5f);

        [Tooltip("Lampeggi al secondo durante lo slancio.")]
        [SerializeField, Min(0f)] private float strikeStrobe = 9f;

        [Tooltip("Intensità in caccia, rispetto al riposo: l'esca si accende per attirare la preda.")]
        [SerializeField, Min(0f)] private float huntBoost = 1.4f;

        [Tooltip("Umore minimo in caccia (0 = calma, 1 = aggressività piena): l'aggressività sale solo con il sub, " +
                 "così senza questo minimo la caccia alle prede resterebbe del colore della calma.")]
        [SerializeField, Range(0f, 1f)] private float huntMood = 0.8f;

        [Tooltip("Intensità in fuga, rispetto al riposo.")]
        [SerializeField, Range(0f, 1f)] private float fleeDim = 0.4f;

        [Tooltip("Velocità con cui l'umore visibile insegue l'aggressività (1/s).")]
        [SerializeField, Min(0.1f)] private float responsiveness = 2f;

        private Creature creature;
        private MaterialPropertyBlock block;
        private float mood;
        private float phase;

        private void Awake()
        {
            creature = GetComponent<Creature>();
            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            mood = 0f;
            phase = Random.value * Mathf.PI * 2f;
        }

        private void LateUpdate()
        {
            if (renderers == null || renderers.Length == 0) return;
            float dt = Time.deltaTime;
            CreatureState state = creature != null ? creature.State : CreatureState.Wander;
            float aggression = creature != null ? creature.Aggression : 0f;
            if (state == CreatureState.Hunt) aggression = Mathf.Max(aggression, huntMood);
            mood = Mathf.Lerp(mood, aggression, 1f - Mathf.Exp(-responsiveness * dt));

            Color color = Color.Lerp(calmColor, angryColor, mood);
            float frequency = Mathf.Lerp(pulseFrequency.x, pulseFrequency.y, mood);
            float depth = Mathf.Lerp(pulseDepth.x, pulseDepth.y, mood);
            float scale = 1f;
            bool strobe = false;
            switch (state)
            {
                case CreatureState.Strike:
                    color = strikeColor;
                    frequency = strikeStrobe;
                    depth = 0.8f;
                    strobe = true;
                    break;
                case CreatureState.Hunt:
                    scale = huntBoost;
                    frequency = Mathf.Max(frequency, 1f);
                    break;
                case CreatureState.Flee:
                    scale = fleeDim;
                    break;
            }

            phase += frequency * 2f * Mathf.PI * dt;
            if (phase > 1000f) phase -= 2f * Mathf.PI * 159f;
            float wave = Mathf.Sin(phase);
            if (strobe) wave = wave >= 0f ? 1f : -1f;
            float pulse = Mathf.Max(0.05f, 1f + depth * wave) * scale;

            bool visible = false;
            for (int i = 0; i < renderers.Length; i++) visible |= renderers[i] != null && renderers[i].isVisible;
            if (!visible) return;
            // SetVector e non SetColor: in spazio colore Linear SetColor tratta il colore come gamma e lo eleva a 2.2,
            // e con valori HDR (sopra 1) l'emissione esplode. Così il valore arriva allo shader com'è, come quello
            // salvato nel materiale.
            block.SetVector(EmissionColorId, color * pulse);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(block);
        }
    }
}

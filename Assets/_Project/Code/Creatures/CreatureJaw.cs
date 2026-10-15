using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Mascella procedurale: apre l'osso della mandibola di un angolo che dipende dallo stato della
    /// <see cref="Creature"/> sullo stesso GameObject. Socchiusa mentre vaga, chiusa in fuga, sempre più aperta
    /// mentre gira attorno al sub con l'aggressività che sale: è l'avvertimento prima del morso. Un respiro lento
    /// la muove di poco, così non resta mai immobile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureJaw : MonoBehaviour
    {
        [Tooltip("Osso della mandibola. Ruota attorno a openAxis partendo dalla posa di riposo, che deve essere chiusa.")]
        [SerializeField] private Transform jaw;

        [Tooltip("Asse di rotazione dell'apertura, nello spazio locale dell'osso (la cerniera della mandibola).")]
        [SerializeField] private Vector3 openAxis = Vector3.right;

        [Tooltip("Angolo (gradi) a mascella spalancata; il segno sceglie il verso dell'apertura.")]
        [SerializeField, Range(-120f, 120f)] private float openAngle = 40f;

        [Tooltip("Apertura (0..1) mentre vaga o osserva.")]
        [SerializeField, Range(0f, 1f)] private float idleOpen = 0.1f;

        [Tooltip("Apertura (0..1) mentre gira attorno al sub ad aggressività 0.")]
        [SerializeField, Range(0f, 1f)] private float stalkOpen = 0.2f;

        [Tooltip("Apertura (0..1) mentre gira attorno al sub ad aggressività piena.")]
        [SerializeField, Range(0f, 1f)] private float strikeOpen = 0.85f;

        [Tooltip("Apertura (0..1) mentre insegue una preda.")]
        [SerializeField, Range(0f, 1f)] private float huntOpen = 0.7f;

        [Tooltip("Secondi in cui la mascella resta chiusa di scatto dopo un morso.")]
        [SerializeField, Range(0.05f, 2f)] private float snapDuration = 0.35f;

        [Tooltip("Ampiezza del respiro lento attorno all'apertura (0..1). In fuga niente respiro.")]
        [SerializeField, Range(0f, 0.5f)] private float breathAmount = 0.08f;

        [Tooltip("Velocità con cui l'apertura insegue il valore voluto (1/s).")]
        [SerializeField, Min(0.1f)] private float responsiveness = 3f;

        [Tooltip("Renderer della creatura (opzionale): se assegnato, l'osso viene scritto solo quando è visibile.")]
        [SerializeField] private Renderer visibilityRenderer;

        // Slancio: la mascella insegue l'apertura 4 volte più in fretta; morso: chiusura quasi istantanea (1/s).
        private const float LungeResponse = 4f;
        private const float SnapResponse = 40f;

        private Creature creature;
        private Quaternion restRotation;
        private float openness;
        private float seed;

        /// <summary>Apertura attuale della mascella, da 0 (chiusa, posa di riposo) a 1 (openAngle).</summary>
        public float Openness => openness;

        private void Awake()
        {
            creature = GetComponent<Creature>();
            if (jaw != null) restRotation = jaw.localRotation;
        }

        private void OnEnable()
        {
            openness = 0f;
            seed = Random.value * 100f;
        }

        private void LateUpdate()
        {
            if (jaw == null) return;

            float target = idleOpen;
            float response = responsiveness;
            bool fleeing = false;
            if (creature != null)
            {
                switch (creature.State)
                {
                    case CreatureState.Flee:
                        target = 0f;
                        fleeing = true;
                        break;
                    case CreatureState.Stalk:
                        target = Mathf.Lerp(stalkOpen, strikeOpen, creature.Aggression);
                        break;
                    case CreatureState.Strike:
                        target = 1f;
                        response = responsiveness * LungeResponse;
                        break;
                    case CreatureState.Hunt:
                        target = huntOpen;
                        response = responsiveness * LungeResponse;
                        break;
                }
                // Il morso (al sub o a una preda) chiude la mascella di colpo.
                if (Time.time - creature.LastBiteTime < snapDuration)
                {
                    target = 0f;
                    fleeing = true;
                    response = SnapResponse;
                }
            }
            if (!fleeing) target += breathAmount * (Mathf.PerlinNoise(seed, Time.time * 0.3f) * 2f - 1f);

            openness = Mathf.Lerp(openness, Mathf.Clamp01(target), 1f - Mathf.Exp(-response * Time.deltaTime));

            if (visibilityRenderer != null && !visibilityRenderer.isVisible) return;
            jaw.localRotation = restRotation * Quaternion.AngleAxis(openness * openAngle, openAxis);
        }
    }
}

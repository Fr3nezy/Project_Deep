using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Animazione procedurale di chi striscia sul fondale (isopode): zampe che camminano a onda metacronale, una
    /// coppia dopo l'altra dalla testa alla coda, e antenne a catena che ondeggiano e si alzano in avanti quando la
    /// creatura percepisce uno stimolo. Legge solo velocità, stato e stimolo percepito della <see cref="Creature"/>;
    /// il moto sul fondale lo decide <c>CreatureProfile.crawlsOnFloor</c>. Niente IK: ogni osso ruota attorno al suo
    /// perno a partire dalla posa di riposo.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Creature))]
    public sealed class CreatureCrawl : MonoBehaviour
    {
        [Header("Zampe")]
        [Tooltip("Perni delle zampe sul lato sinistro, dalla testa alla coda. Ogni zampa parte dal perno verso -X.")]
        [SerializeField] private Transform[] legsLeft;

        [Tooltip("Perni delle zampe sul lato destro, dalla testa alla coda. Ogni zampa parte dal perno verso +X.")]
        [SerializeField] private Transform[] legsRight;

        [Tooltip("Metri percorsi per ogni ciclo di passo: più è corto, più le zampe sembrano trottare.")]
        [SerializeField, Min(0.01f)] private float strideLength = 0.12f;

        [Tooltip("Ampiezza dell'oscillazione avanti e indietro della zampa (gradi).")]
        [SerializeField, Range(0f, 60f)] private float swingAngle = 25f;

        [Tooltip("Quanto si solleva la zampa nella fase di ritorno (gradi).")]
        [SerializeField, Range(0f, 60f)] private float liftAngle = 22f;

        [Tooltip("Sfasamento del ciclo fra una coppia di zampe e la successiva, in frazioni di ciclo: l'onda che corre " +
                 "lungo il corpo.")]
        [SerializeField, Range(0f, 0.5f)] private float waveLag = 0.12f;

        [Tooltip("Frazione del ciclo in cui la zampa è a terra e arretra a velocità costante; il resto è il ritorno " +
                 "in avanti, più rapido e sollevato.")]
        [SerializeField, Range(0.5f, 0.9f)] private float dutyFactor = 0.65f;

        [Tooltip("Velocità (m/s) sotto la quale il passo si spegne del tutto. Sopra, l'ampiezza sale fino al massimo " +
                 "alla velocità di crociera.")]
        [SerializeField, Min(0f)] private float stillSpeed = 0.02f;

        [Header("Antenne")]
        [Tooltip("Segmenti dell'antenna sinistra, dalla base alla punta (catena: ogni osso figlio del precedente).")]
        [SerializeField] private Transform[] antennaLeft;

        [Tooltip("Segmenti dell'antenna destra, dalla base alla punta.")]
        [SerializeField] private Transform[] antennaRight;

        [Tooltip("Ondeggio laterale per segmento a riposo (gradi).")]
        [SerializeField, Range(0f, 30f)] private float antennaSway = 7f;

        [Tooltip("Quanto si alzano in avanti le antenne quando la creatura percepisce uno stimolo o scappa (gradi per segmento).")]
        [SerializeField, Range(0f, 40f)] private float antennaAlertRaise = 12f;

        [Tooltip("Quanto si divaricano di lato le antenne in allerta (gradi per segmento).")]
        [SerializeField, Range(0f, 40f)] private float antennaAlertSpread = 6f;

        [Tooltip("Prontezza con cui le antenne passano da riposo ad allerta (1/s).")]
        [SerializeField, Min(0.1f)] private float antennaResponse = 8f;

        [Header("Visibilità")]
        [Tooltip("Renderer del corpo (opzionale): se assegnato, le ossa sono scritte solo quando è visibile.")]
        [SerializeField] private Renderer visibilityRenderer;

        private Creature creature;
        private Quaternion[] legRestLeft, legRestRight, antennaRestLeft, antennaRestRight;
        // Rotazione a riposo di ogni osso rispetto alla radice: le oscillazioni usano gli assi della creatura, non quelli dell'osso importato.
        private Quaternion[] legRootLeft, legRootRight, antennaRootLeft, antennaRootRight;
        private float phase;
        private float alert;
        private float seed;

        private void Awake()
        {
            creature = GetComponent<Creature>();
            seed = Random.value * 100f;
            phase = Random.value;
            legRestLeft = Capture(legsLeft, out legRootLeft);
            legRestRight = Capture(legsRight, out legRootRight);
            antennaRestLeft = Capture(antennaLeft, out antennaRootLeft);
            antennaRestRight = Capture(antennaRight, out antennaRootRight);
        }

        private Quaternion[] Capture(Transform[] bones, out Quaternion[] toRoot)
        {
            int count = bones != null ? bones.Length : 0;
            var rest = new Quaternion[count];
            toRoot = new Quaternion[count];
            Quaternion rootInverse = Quaternion.Inverse(transform.rotation);
            for (int i = 0; i < count; i++)
            {
                rest[i] = bones[i].localRotation;
                toRoot[i] = rootInverse * bones[i].rotation;
            }
            return rest;
        }

        // Applica una rotazione espressa negli assi della creatura (X destra, Y su, Z avanti) all'osso a riposo.
        private static void Pose(Transform bone, Quaternion rest, Quaternion toRoot, Quaternion delta)
        {
            bone.localRotation = rest * (Quaternion.Inverse(toRoot) * delta * toRoot);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float speed = creature.Speed;
            float cruise = creature.Profile != null ? Mathf.Max(0.01f, creature.Profile.cruiseSpeed) : 1f;
            phase = Mathf.Repeat(phase + speed / strideLength * dt, 1f);

            bool alerted = creature.State != CreatureState.Wander || creature.Perceived != StimulusCategory.None;
            alert = Mathf.MoveTowards(alert, alerted ? 1f : 0f, antennaResponse * dt);

            if (visibilityRenderer != null && !visibilityRenderer.isVisible) return;

            float walk = Mathf.Clamp01((speed - stillSpeed) / cruise);
            AnimateLegs(legsLeft, legRestLeft, legRootLeft, -1f, walk);
            AnimateLegs(legsRight, legRestRight, legRootRight, 1f, walk);
            AnimateAntenna(antennaLeft, antennaRestLeft, antennaRootLeft, -1f);
            AnimateAntenna(antennaRight, antennaRestRight, antennaRootRight, 1f);
        }

        private void AnimateLegs(Transform[] legs, Quaternion[] rest, Quaternion[] toRoot, float side, float walk)
        {
            if (legs == null) return;
            // Le due file camminano in controfase.
            float sideOffset = side < 0f ? 0.5f : 0f;
            for (int i = 0; i < legs.Length; i++)
            {
                float cycle = Mathf.Repeat(phase - i * waveLag + sideOffset, 1f);
                float reach, raise;
                if (cycle < dutyFactor)
                {
                    // A terra: arretra in modo lineare, come una zampa piantata mentre il corpo avanza.
                    reach = 1f - 2f * cycle / dutyFactor;
                    raise = 0f;
                }
                else
                {
                    // Ritorno: torna avanti con partenza e arrivo morbidi, sollevata a metà corsa.
                    float t = (cycle - dutyFactor) / (1f - dutyFactor);
                    reach = Mathf.SmoothStep(-1f, 1f, t);
                    raise = Mathf.Sin(t * Mathf.PI);
                }
                // Rotazione positiva su Y porta la punta indietro su entrambi i lati, da qui il segno meno.
                float swing = -reach * swingAngle * walk;
                float lift = raise * liftAngle * walk;
                Pose(legs[i], rest[i], toRoot[i], Quaternion.Euler(0f, swing * side, lift * side));
            }
        }

        private void AnimateAntenna(Transform[] segments, Quaternion[] rest, Quaternion[] toRoot, float side)
        {
            if (segments == null) return;
            float time = Time.time + seed;
            for (int i = 0; i < segments.Length; i++)
            {
                float sway = Mathf.Sin(time * (1.1f + 0.35f * i) + i * 0.7f + side) * antennaSway;
                // In allerta tremano più in fretta e si tendono avanti e in alto.
                sway += alert * Mathf.Sin(time * 11f + i + side * 2f) * 3f;
                float raise = alert * antennaAlertRaise;
                float spread = alert * antennaAlertSpread * side;
                Pose(segments[i], rest[i], toRoot[i], Quaternion.Euler(-raise, sway + spread, 0f));
            }
        }
    }
}

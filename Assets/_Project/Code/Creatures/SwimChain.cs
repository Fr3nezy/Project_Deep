using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Catena di joint procedurale per le creature a corpo lungo (viperfish, Pelican Eel, tripode in fuga).
    /// "Follow the leader": la prima osso segue la <see cref="Creature"/> per gerarchia, ogni osso successivo insegue
    /// quello davanti tenendosi a distanza fissa, quindi il corpo ripercorre la traiettoria della testa. Sopra la
    /// catena corre un'onda di nuoto laterale, dalla testa alla coda, che cresce con la velocità e verso la coda.
    /// I punti simulati sono tenuti qui (non letti dai transform), così la gerarchia delle ossa non li sposta.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwimChain : MonoBehaviour
    {
        [Tooltip("Ossa della catena in ordine dalla testa alla coda. La prima segue la creatura e non viene toccata; " +
                 "le distanze fra ossa consecutive sono quelle della posa di riposo.")]
        [SerializeField] private Transform[] bones;

        [Tooltip("Piega massima fra due segmenti consecutivi (gradi): il corpo non fa mai spigoli.")]
        [SerializeField, Range(1f, 90f)] private float maxBend = 30f;

        [Tooltip("Ampiezza laterale dell'onda in coda, alla velocità di crociera, in frazione della lunghezza della catena.")]
        [SerializeField, Range(0f, 0.5f)] private float waveAmplitude = 0.08f;

        [Tooltip("Ampiezza dell'onda alla testa, in frazione di quella in coda.")]
        [SerializeField, Range(0f, 1f)] private float headAmplitude = 0.1f;

        [Tooltip("Onde lungo il corpo: 1 = un'onda intera dalla testa alla coda (anguilla), meno = corpo più rigido.")]
        [SerializeField, Range(0f, 3f)] private float waveCount = 0.9f;

        [Tooltip("Onde al secondo alla velocità di crociera (Hz); scalano con la velocità.")]
        [SerializeField, Min(0f)] private float waveFrequency = 1.2f;

        [Tooltip("Renderer della creatura (opzionale): se assegnato, le ossa vengono scritte solo quando è visibile. " +
                 "La simulazione avanza comunque, così il corpo non salta quando torna in vista.")]
        [SerializeField] private Renderer visibilityRenderer;

        private Creature creature;
        private Vector3[] points;
        private Vector3[] displaced;
        private float[] lengths;
        private Quaternion[] restOffsets;
        private float chainLength;
        private float phase;
        private bool needsReset = true;

        /// <summary>Lunghezza della catena a riposo (m).</summary>
        public float ChainLength => chainLength;

        private void Awake()
        {
            creature = GetComponent<Creature>();
            int count = bones != null ? bones.Length : 0;
            points = new Vector3[count];
            lengths = new float[count];
            restOffsets = new Quaternion[count];
            chainLength = 0f;

            displaced = new Vector3[count];

            // Posa di riposo: lunghezze dei segmenti e rotazione di ogni osso rispetto alla sua direzione di catena
            // (vedi AimDirection), così l'asse delle ossa del rig può essere qualunque.
            Vector3 up = transform.up;
            for (int i = 1; i < count; i++)
            {
                lengths[i] = (bones[i - 1].position - bones[i].position).magnitude;
                chainLength += lengths[i];
                displaced[i - 1] = bones[i - 1].position;
            }
            if (count > 0) displaced[count - 1] = bones[count - 1].position;
            for (int i = 1; i < count; i++)
            {
                Vector3 aim = AimDirection(i);
                Quaternion look = Quaternion.LookRotation(aim.sqrMagnitude > 1e-8f ? aim : transform.forward, up);
                restOffsets[i] = Quaternion.Inverse(look) * bones[i].rotation;
            }
        }

        // Direzione verso la testa per l'osso i: bisettrice fra il segmento davanti e quello dietro (la corda
        // dall'osso davanti a quello dietro), l'ultimo segue il segmento davanti. Un osso pesa la carne fra sé e
        // l'osso dietro: orientarlo sul solo segmento davanti la ruotava con un segmento di ritardo e apriva
        // gradini alle giunture.
        private Vector3 AimDirection(int i)
        {
            int behind = Mathf.Min(i + 1, displaced.Length - 1);
            return displaced[i - 1] - displaced[behind];
        }

        private void OnEnable() => needsReset = true;

        private void LateUpdate()
        {
            int count = points.Length;
            if (count < 2) return;

            Transform head = bones[0];
            Vector3 headPosition = head.position;
            Vector3 up = transform.up;
            Vector3 headBack = -transform.forward;

            // Uscita dal pool o teletrasporto: catena distesa dietro la testa.
            if (needsReset || (points[0] - headPosition).sqrMagnitude > chainLength * chainLength)
            {
                Vector3 p = headPosition;
                points[0] = p;
                for (int i = 1; i < count; i++)
                {
                    p += headBack * lengths[i];
                    points[i] = p;
                }
                needsReset = false;
            }

            // Follow the leader con limite di piega: ogni punto resta sulla retta verso il suo vecchio posto, a
            // distanza fissa dal punto davanti, e la direzione non si scosta più di maxBend da quella precedente.
            points[0] = headPosition;
            float maxBendRad = maxBend * Mathf.Deg2Rad;
            Vector3 previousDirection = headBack;
            for (int i = 1; i < count; i++)
            {
                Vector3 direction = points[i] - points[i - 1];
                direction = direction.sqrMagnitude > 1e-10f ? direction.normalized : previousDirection;
                direction = Vector3.RotateTowards(previousDirection, direction, maxBendRad, 0f);
                points[i] = points[i - 1] + direction * lengths[i];
                previousDirection = direction;
            }

            float pace = 1f;
            if (creature != null && creature.Profile != null)
                pace = creature.Speed / Mathf.Max(creature.Profile.cruiseSpeed, 0.01f);
            phase += waveFrequency * Mathf.Clamp(pace, 0.3f, 3f) * 2f * Mathf.PI * Time.deltaTime;
            if (phase > 1000f) phase -= 2f * Mathf.PI * 159f;

            if (visibilityRenderer != null && !visibilityRenderer.isVisible) return;

            // Onda laterale sopra la catena (non entra nella simulazione, altrimenti si accumulerebbe), poi ossa
            // orientate sulla catena già spostata.
            float amplitude = waveAmplitude * chainLength * Mathf.Clamp(pace, 0.3f, 1.5f);
            float k = waveCount * 2f * Mathf.PI;
            displaced[0] = headPosition;
            for (int i = 1; i < count; i++)
            {
                float u = i / (float)(count - 1);
                Vector3 back = points[i] - points[i - 1];
                Vector3 side = Vector3.Cross(up, back);
                side = side.sqrMagnitude > 1e-10f ? side.normalized : transform.right;
                float envelope = Mathf.Lerp(headAmplitude, 1f, u * u);
                displaced[i] = points[i] + side * (amplitude * envelope * Mathf.Sin(phase - u * k));
            }
            for (int i = 1; i < count; i++)
            {
                Vector3 aim = AimDirection(i);
                if (aim.sqrMagnitude < 1e-10f) aim = points[i - 1] - points[i];
                bones[i].SetPositionAndRotation(displaced[i], Quaternion.LookRotation(aim, up) * restOffsets[i]);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (points == null || points.Length < 2) return;
            Gizmos.color = Color.cyan;
            for (int i = 1; i < points.Length; i++) Gizmos.DrawLine(points[i - 1], points[i]);
        }
    }
}

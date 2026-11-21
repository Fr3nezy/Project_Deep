using UnityEngine;

namespace Deeploration.Interaction
{
    /// <summary>
    /// Cavo fisico tra un ancoraggio fisso (rocchetto) e una spina trascinabile (<see cref="Grabbable"/>):
    /// particelle Verlet con vincoli solo in trazione, collisioni a sfera sul mondo e attrito sul fondale.
    /// Quando il percorso supera maxLength tira la spina verso il cavo: oltre PlayerGrab.maxForce la presa si stacca da sola.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PhysicsCable : MonoBehaviour
    {
        [Header("Estremi")]
        [Tooltip("Punto fisso da cui esce il cavo. Vuoto = questo transform.")]
        [SerializeField] private Transform anchor;
        [Tooltip("Spina all'altro capo: Grabbable con Rigidbody.")]
        [SerializeField] private Grabbable plug;
        [Tooltip("Punto della spina, nello spazio locale della spina, dove entra il cavo.")]
        [SerializeField] private Vector3 plugAttachLocal = new Vector3(0f, 0f, -0.15f);

        [Header("Corda")]
        [Tooltip("Lunghezza a riposo del cavo, in metri.")]
        [SerializeField, Min(1f)] private float maxLength = 30f;
        [Tooltip("Distanza tra due particelle, in metri. Più corta = più liscia e più costosa.")]
        [SerializeField, Range(0.1f, 1f)] private float segmentLength = 0.25f;
        [Tooltip("Iterazioni dei vincoli per passo fisico.")]
        [SerializeField, Range(1, 60)] private int iterations = 24;
        [Tooltip("Gravità del cavo in acqua, in m/s² (verso il basso).")]
        [SerializeField, Min(0f)] private float gravity = 2.5f;
        [Tooltip("Smorzamento della velocità per passo: 0 = nessuno, 1 = fermo.")]
        [SerializeField, Range(0f, 1f)] private float damping = 0.08f;

        [Header("Collisioni")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [Tooltip("Raggio di collisione di ogni particella, in metri.")]
        [SerializeField, Min(0.005f)] private float radius = 0.04f;
        [Tooltip("Margine tenuto dalle superfici, in metri: evita che la sfera parta già dentro il collider.")]
        [SerializeField, Min(0f)] private float skin = 0.01f;
        [Tooltip("Quota di movimento persa a ogni contatto: 0 = ghiaccio, 1 = incollato.")]
        [SerializeField, Range(0f, 1f)] private float friction = 0.6f;

        [Header("Tensione sulla spina")]
        [Tooltip("Forza sulla spina per metro di cavo oltre maxLength, in N/m.")]
        [SerializeField, Min(0f)] private float tensionStiffness = 1500f;
        [Tooltip("Forza massima sulla spina, in N. Sopra PlayerGrab.maxForce (400) il cavo vince e la presa si stacca.")]
        [SerializeField, Min(0f)] private float maxTension = 900f;

        [Header("Debug (sola lettura)")]
        [SerializeField] private float currentLength;

        /// <summary>Lunghezza del percorso attuale del cavo, in metri.</summary>
        public float CurrentLength => currentLength;
        /// <summary>Metri oltre maxLength (0 se il cavo è lento).</summary>
        public float Excess => Mathf.Max(0f, currentLength - RestLength);
        public Grabbable Plug => plug;

        private Vector3[] pos;
        private Vector3[] prev;
        private float seg;
        private LineRenderer line;
        private SphereCollider probe;
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private readonly Collider[] overlaps = new Collider[8];

        private float RestLength => seg * (pos.Length - 1);
        private Vector3 PlugPoint => plug.transform.TransformPoint(plugAttachLocal);

        private void Awake()
        {
            if (anchor == null) anchor = transform;
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            int count = Mathf.Max(2, Mathf.CeilToInt(maxLength / segmentLength) + 1);
            seg = maxLength / (count - 1);
            pos = new Vector3[count];
            prev = new Vector3[count];
            line.positionCount = count;
            // Sfera di prova per ComputePenetration: trigger su Ignore Raycast, fuori da ogni query del cavo e del gioco.
            var probeObject = new GameObject("CableProbe") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            probeObject.transform.SetParent(transform, false);
            probe = probeObject.AddComponent<SphereCollider>();
            probe.isTrigger = true;
            probe.radius = radius;
            // Steso in linea retta tra gli estremi, compresso: i vincoli agiscono solo in trazione, il cavo cade e si posa.
            Vector3 a = anchor.position, b = plug != null ? PlugPoint : a;
            // Ogni punto sollevato sopra il fondale: una particella sepolta sotto un MeshCollider non tocca triangoli e non risale.
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)(count - 1));
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 3f + radius + skin,
                        collisionMask, QueryTriggerInteraction.Ignore))
                    p.y = Mathf.Max(p.y, ground.point.y + radius + skin);
                pos[i] = prev[i] = p;
            }
        }

        private void FixedUpdate()
        {
            if (plug == null) return;
            float dt = Time.fixedDeltaTime;
            int last = pos.Length - 1;
            Vector3 drop = Vector3.down * (gravity * dt * dt);

            for (int i = 1; i < last; i++)
            {
                Vector3 velocity = (pos[i] - prev[i]) * (1f - damping);
                prev[i] = pos[i];
                pos[i] += velocity + drop;
            }
            pos[0] = prev[0] = anchor.position;
            pos[last] = prev[last] = PlugPoint;

            for (int k = 0; k < iterations; k++) SolveSegments(last);
            for (int i = 1; i < last; i++) Collide(i);

            currentLength = 0f;
            for (int i = 0; i < last; i++) currentLength += Vector3.Distance(pos[i], pos[i + 1]);
            PullPlug(last);
        }

        // Solo trazione: un segmento più corto del riposo non spinge (un cavo vero non resiste alla compressione).
        private void SolveSegments(int last)
        {
            for (int i = 0; i < last; i++)
            {
                Vector3 d = pos[i + 1] - pos[i];
                float len = d.magnitude;
                if (len <= seg || len < 1e-6f) continue;
                Vector3 fix = d * ((len - seg) / len);
                bool pinA = i == 0, pinB = i + 1 == last;
                if (pinA && pinB) continue;
                if (pinA) pos[i + 1] -= fix;
                else if (pinB) pos[i] += fix;
                else { pos[i] += fix * 0.5f; pos[i + 1] -= fix * 0.5f; }
            }
        }

        // Sfera spazzata dalla posizione precedente: si ferma prima della superficie e perde parte del moto (attrito).
        private void Collide(int i)
        {
            Vector3 move = pos[i] - prev[i];
            float distance = move.magnitude;
            if (distance < 1e-5f) { Depenetrate(i); return; }
            Vector3 dir = move / distance;
            int n = Physics.SphereCastNonAlloc(prev[i], radius, dir, hits, distance + skin, collisionMask,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 normal = Vector3.up;
            for (int h = 0; h < n; h++)
            {
                RaycastHit hit = hits[h];
                if (hit.distance <= 0f || hit.rigidbody == plug.Body) continue;
                if (hit.distance < nearest) { nearest = hit.distance; normal = hit.normal; }
            }
            if (nearest != float.MaxValue)
            {
                // Fermo al contatto, poi scorre sul piano della superficie col resto del moto: senza, il cavo si incolla alle gobbe.
                Vector3 contact = prev[i] + dir * Mathf.Max(0f, nearest - skin);
                Vector3 slide = Vector3.ProjectOnPlane(pos[i] - contact, normal);
                prev[i] = Vector3.Lerp(prev[i], contact, friction);
                pos[i] = contact + slide;
            }
            Depenetrate(i);
        }

        // La sfera spazzata non vede un collider in cui parte già dentro (avvio, vincoli che tirano nella roccia): spinta fuori.
        private void Depenetrate(int i)
        {
            int n = Physics.OverlapSphereNonAlloc(pos[i], radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < n; h++)
            {
                Collider other = overlaps[h];
                if (other.attachedRigidbody == plug.Body) continue;
                Transform t = other.transform;
                if (!Physics.ComputePenetration(probe, pos[i], Quaternion.identity, other, t.position, t.rotation,
                        out Vector3 direction, out float depth)) continue;
                pos[i] += direction * (depth + skin);
                prev[i] = Vector3.Lerp(prev[i], pos[i], friction);
            }
        }

        private void PullPlug(int last)
        {
            Rigidbody body = plug.Body;
            float excess = Excess;
            if (body == null || body.isKinematic || excess <= 0f) return;
            Vector3 toward = pos[last - 1] - pos[last];
            if (toward.sqrMagnitude < 1e-8f) return;
            float force = Mathf.Min(excess * tensionStiffness, maxTension);
            body.AddForceAtPosition(toward.normalized * force, PlugPoint);
        }

        private void LateUpdate()
        {
            if (plug == null) return;
            int last = pos.Length - 1;
            line.SetPositions(pos);
            // Estremi sui transform interpolati: il punto dell'ultimo passo fisico resterebbe indietro di un frame.
            line.SetPosition(0, anchor.position);
            line.SetPosition(last, PlugPoint);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (plug == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere((anchor != null ? anchor : transform).position, maxLength);
        }
#endif
    }
}

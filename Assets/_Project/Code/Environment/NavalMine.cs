using UnityEngine;
using UnityEngine.Events;
using Deeploration.Player;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Deeploration.Environment
{
    /// <summary>
    /// Mina navale ancorata del perimetro: cassa sul fondo, catena e testa che oscillano con la corrente.
    /// La catena è una mesh unica generata da un segmento, lunga <see cref="chainLength"/>, e non viene salvata in scena.
    /// La scia del diver a breve distanza la scosta; il contatto con la testa uccide il diver (ossigeno a zero, poi DiverRespawn).
    /// </summary>
    [ExecuteAlways]
    public sealed class NavalMine : MonoBehaviour
    {
        [Header("Parti")]
        [Tooltip("Pivot dell'oscillazione, nel punto in cui la catena esce dalla cassa. Contiene catena e testa.")]
        [SerializeField] private Transform swayPivot;
        [Tooltip("Testa della mina, figlia del pivot; il suo pivot è il punto d'aggancio della catena.")]
        [SerializeField] private Transform head;
        [Tooltip("Renderer della testa, usato per il raggio di contatto e per nasconderla allo scoppio.")]
        [SerializeField] private Renderer headRenderer;
        [Tooltip("MeshFilter che riceve la catena generata, figlio del pivot.")]
        [SerializeField] private MeshFilter chainFilter;
        [Tooltip("Mesh di un segmento di catena (due anelli), ripetuta in verticale al passo della sua altezza.")]
        [SerializeField] private Mesh chainSegment;

        [Header("Catena")]
        [Tooltip("Metri di catena fra il pivot e l'aggancio della testa.")]
        [SerializeField, Min(0.5f)] private float chainLength = 6f;
        [Tooltip("Metri di catena che entrano nella testa, per nascondere l'ultimo anello aperto.")]
        [SerializeField, Min(0f)] private float headOverlap = 0.12f;

        [Header("Piazzamento")]
        [Tooltip("Metri di cassa affondati nel fondale da «Appoggia al fondale».")]
        [SerializeField, Min(0f)] private float sinkDepth = 0.35f;
        [Tooltip("Fase dell'oscillazione. 0 = ricavata dalla posizione, così due mine non si muovono all'unisono.")]
        [SerializeField] private int seed;

        [Header("Corrente")]
        [Tooltip("Gradi massimi di oscillazione con la corrente.")]
        [SerializeField, Range(0f, 30f)] private float swayAngle = 10f;
        [Tooltip("Secondi di un'oscillazione completa.")]
        [SerializeField, Min(0.5f)] private float swayPeriod = 8f;

        [Header("Scia del diver")]
        [Tooltip("Metri oltre il raggio di contatto entro cui la scia del diver sposta la mina.")]
        [SerializeField, Min(0f)] private float wakeRadius = 2f;
        [Tooltip("Accelerazione angolare (gradi/s²) per ogni m/s del diver, a distanza minima.")]
        [SerializeField, Min(0f)] private float wakeStrength = 20f;
        [Tooltip("Gradi massimi di scostamento dovuti alla scia.")]
        [SerializeField, Range(0f, 30f)] private float maxWakeAngle = 10f;
        [Tooltip("Rigidezza della molla che riporta la mina in posizione.")]
        [SerializeField, Min(0f)] private float springStiffness = 3f;
        [Tooltip("Smorzamento della molla.")]
        [SerializeField, Min(0f)] private float springDamping = 1.6f;

        [Header("Scoppio")]
        [Tooltip("Metri aggiunti al raggio della testa per il contatto mortale.")]
        [SerializeField] private float contactPadding;
        [Tooltip("Secondi dopo lo scoppio in cui la mina ricompare, se Detonate è stato chiamato con rearm. Più lunghi del respawn del diver.")]
        [SerializeField, Min(0f)] private float rearmDelay = 4f;
        public UnityEvent onDetonated = new UnityEvent();

        private const float TeleportDistance = 3f; // spostamento del diver in un frame oltre il quale è un respawn, non una nuotata
        private const float MaxDiverSpeed = 8f;

        private Mesh chainMesh;
        private float chainRadius;
        private Renderer chainRenderer;
        private Transform diver;
        private CharacterController diverController;
        private OxygenSystem diverOxygen;
        private Vector3 lastDiverPosition;
        private Vector2 wakeTilt;
        private Vector2 wakeVelocity;
        private float phase;
        private bool detonated;

        public bool IsDetonated => detonated;
        public float ChainLength => chainLength;

        private void OnEnable()
        {
            RebuildChain();
            if (!Application.isPlaying) return;

            phase = ((seed != 0 ? seed : transform.position.GetHashCode()) & 0x7fffffff) % 1000 * 0.1f;
            DiverController found = FindAnyObjectByType<DiverController>();
            if (found == null) return;
            diver = found.transform;
            diverController = found.GetComponent<CharacterController>();
            diverOxygen = found.GetComponent<OxygenSystem>();
            lastDiverPosition = DiverCenter();
        }

        private void OnDisable()
        {
            if (chainMesh != null) DestroyMesh(chainMesh);
            chainMesh = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // OnValidate non può creare mesh né toccare i transform: si rimanda al prossimo tick dell'editor.
            EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) RebuildChain(); };
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying || swayPivot == null) return;

            float dt = Time.deltaTime;
            if (diver != null && dt > 0f) UpdateDiver(dt);

            // Spring smorzata: la scia la spinge, la molla la riporta al centro.
            wakeVelocity += (-springStiffness * wakeTilt - springDamping * wakeVelocity) * dt;
            wakeTilt += wakeVelocity * dt;
            wakeTilt = Vector2.ClampMagnitude(wakeTilt, maxWakeAngle);

            float t = Time.time / swayPeriod;
            Vector2 current = new Vector2(Mathf.PerlinNoise(t, phase) - 0.5f, Mathf.PerlinNoise(phase, t) - 0.5f) * (2f * swayAngle);
            Vector2 tilt = current + wakeTilt;

            // tilt in gradi verso la direzione orizzontale (x, z) del mondo
            float angle = tilt.magnitude;
            Quaternion lean = angle > 1e-4f ? Quaternion.AngleAxis(angle, new Vector3(tilt.y, 0f, -tilt.x) / angle) : Quaternion.identity;
            swayPivot.rotation = lean * transform.rotation;
        }

        private void UpdateDiver(float dt)
        {
            Vector3 position = DiverCenter();
            Vector3 delta = position - lastDiverPosition;
            lastDiverPosition = position;
            if (detonated || delta.magnitude > TeleportDistance) return;

            Vector3 velocity = Vector3.ClampMagnitude(delta / dt, MaxDiverSpeed);
            Vector3 center = HeadCenter(out float headRadius);
            float diverRadius = diverController != null ? diverController.radius : 0.3f;
            float distance = Vector3.Distance(ClosestOnDiverAxis(position, center), center) - headRadius - diverRadius;

            if (distance <= contactPadding || TouchesChain(position, diverRadius))
            {
                if (diverOxygen != null) diverOxygen.SetNormalized(0f);
                Detonate(true);
                return;
            }

            if (distance < wakeRadius)
            {
                float proximity = 1f - distance / wakeRadius;
                wakeVelocity += new Vector2(velocity.x, velocity.z) * (wakeStrength * proximity * proximity * dt);
            }
        }

        // Mine vecchie e instabili: basta sfiorare la catena (segmento dal pivot all'aggancio della testa) per farle scoppiare.
        private bool TouchesChain(Vector3 diverCenter, float diverRadius)
        {
            if (head == null) return false;
            Vector3 a = swayPivot.position, b = head.position;
            Vector3 onChain = ClosestOnSegment(a, b, diverCenter);
            Vector3 onDiver = ClosestOnDiverAxis(diverCenter, onChain);
            onChain = ClosestOnSegment(a, b, onDiver);
            return Vector3.Distance(onDiver, onChain) - diverRadius - chainRadius <= contactPadding;
        }

        // Asse verticale della capsula del diver (il CharacterController resta sempre dritto): punto più vicino a target.
        private Vector3 ClosestOnDiverAxis(Vector3 diverCenter, Vector3 target)
        {
            float half = diverController != null ? Mathf.Max(0f, diverController.height * 0.5f - diverController.radius) : 0f;
            return diverCenter + Vector3.up * Mathf.Clamp(target.y - diverCenter.y, -half, half);
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        }

        /// <summary>
        /// Fa scoppiare la mina: nasconde testa e catena e lancia onDetonated.
        /// Con <paramref name="rearm"/> ricompare dopo rearmDelay (morte del diver); senza resta scoppiata (es. la lucertola alla chiusa 3).
        /// </summary>
        public void Detonate(bool rearm)
        {
            if (detonated) return;
            detonated = true;
            SetVisible(false);
            onDetonated.Invoke();
            if (rearm) Invoke(nameof(Rearm), rearmDelay);
        }

        /// <summary>Riporta la mina intatta e ferma.</summary>
        public void Rearm()
        {
            CancelInvoke(nameof(Rearm));
            detonated = false;
            wakeTilt = wakeVelocity = Vector2.zero;
            SetVisible(true);
            if (diver != null) lastDiverPosition = DiverCenter();
        }

        private void SetVisible(bool visible)
        {
            if (headRenderer != null) headRenderer.enabled = visible;
            if (chainRenderer != null) chainRenderer.enabled = visible;
        }

        private Vector3 DiverCenter() =>
            diverController != null ? diver.TransformPoint(diverController.center) : diver.position;

        private Vector3 HeadCenter(out float radius)
        {
            if (headRenderer == null) { radius = 1f; return head != null ? head.position : transform.position; }
            Bounds b = headRenderer.localBounds;
            Transform t = headRenderer.transform;
            Vector3 e = Vector3.Scale(b.extents, t.lossyScale);
            radius = Mathf.Max(e.x, e.y, e.z);
            return t.TransformPoint(b.center);
        }

        /// <summary>Rigenera la mesh della catena e porta la testa in cima. Non salva niente in scena.</summary>
        public void RebuildChain()
        {
            if (head != null) head.localPosition = new Vector3(0f, chainLength, 0f);
            if (chainFilter == null || chainSegment == null) return;

            chainRenderer = chainFilter.GetComponent<Renderer>();
            Bounds b = chainSegment.bounds;
            float pitch = Mathf.Max(0.01f, b.size.y);
            chainRadius = Mathf.Max(b.extents.x, b.extents.z);
            // shortcut: l'ultimo segmento intero sporge dentro la testa fino a un passo; non si scala mai per non deformare gli anelli
            int count = Mathf.CeilToInt((chainLength + headOverlap) / pitch);
            var parts = new CombineInstance[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = new CombineInstance
                {
                    mesh = chainSegment,
                    transform = Matrix4x4.Translate(new Vector3(-b.center.x, i * pitch - b.min.y, -b.center.z))
                };
            }

            Mesh mesh = new Mesh { name = "NavalMineChain (generata)", hideFlags = HideFlags.DontSave };
            mesh.indexFormat = chainSegment.vertexCount * count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.CombineMeshes(parts, true, true);

            if (chainMesh != null) DestroyMesh(chainMesh);
            chainMesh = mesh;
            chainFilter.sharedMesh = mesh;
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

#if UNITY_EDITOR
        [ContextMenu("Appoggia al fondale")]
        private void SnapToSeafloor()
        {
            Vector3 origin = transform.position + Vector3.up * 3f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 point = Vector3.zero; bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance; point = hit.point; found = true;
            }
            if (!found) { Debug.LogWarning($"{name}: nessun fondale sotto la mina.", this); return; }

            Undo.RecordObject(transform, "Appoggia la mina al fondale");
            transform.position = point - Vector3.up * sinkDepth;
        }

        [ContextMenu("Ruota a caso")]
        private void RandomYaw()
        {
            Undo.RecordObject(transform, "Ruota la mina");
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        private void OnDrawGizmosSelected()
        {
            if (head == null) return;
            Vector3 c = HeadCenter(out float r);
            Gizmos.color = new Color(1f, 0.35f, 0.1f);
            Gizmos.DrawWireSphere(c, r + contactPadding);
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(c, r + contactPadding + wakeRadius);
        }
#endif
    }
}

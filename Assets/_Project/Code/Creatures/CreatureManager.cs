using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Uno per scena. Tiene i pool delle creature (uno per <see cref="CreatureProfile"/>), la lista di quelle
    /// attive e le fa avanzare tutte in un unico ciclo. A ogni frame assegna a ciascuna un livello di dettaglio
    /// (<see cref="CreatureLod"/>) in base alla distanza dal sub e alla visibilità in camera, e da quello decide
    /// se la creatura ragiona e lancia raggi in questo frame. I ragionamenti delle creature con lo stesso
    /// intervallo sono sfasati nei frame, così il costo resta costante invece di arrivare a picchi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureManager : MonoBehaviour
    {
        [Header("Riferimenti")]
        [Tooltip("Sorgente degli stimoli del sub (DiverStimulusSource sul PlayerCapsule). Senza, le creature non " +
                 "percepiscono il sub e il livello di dettaglio si calcola dalla camera.")]
        [SerializeField] private DiverStimulusSource player;

        [Tooltip("Camera per la visibilità. Vuota = Camera.main all'avvio.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("Layer che bloccano vista e movimento delle creature (rocce, fondale). Non includere Player ed Entity.")]
        [SerializeField] private LayerMask obstacleMask = (1 << 0) | (1 << 7) | (1 << 10);

        [Header("Livello di dettaglio")]
        [Tooltip("Entro questa distanza dal sub, se visibile, la creatura ragiona a ogni frame (m)")]
        [SerializeField, Min(1f)] private float nearDistance = 25f;

        [Tooltip("Oltre questa distanza la creatura ragiona di rado e non lancia raggi (m)")]
        [SerializeField, Min(1f)] private float farDistance = 60f;

        [Tooltip("Livello Mid: ragiona e lancia raggi un frame ogni N")]
        [SerializeField, Range(1, 30)] private int midInterval = 4;

        [Tooltip("Livello Far: ragiona un frame ogni N")]
        [SerializeField, Range(1, 120)] private int farInterval = 16;

        [Tooltip("Margine attorno all'inquadratura entro cui una creatura conta come visibile (m)")]
        [SerializeField, Min(0f)] private float visibilityMargin = 1f;

        [Tooltip("Lato delle celle della griglia dei vicini (m). Non deve essere più piccolo della separationDistance " +
                 "delle specie; il raggio del banco arriva al massimo a 3 celle")]
        [SerializeField, Min(0.25f)] private float neighbourCellSize = 1f;

        private readonly Dictionary<CreatureProfile, ObjectPool<Creature>> pools =
            new Dictionary<CreatureProfile, ObjectPool<Creature>>();
        private readonly List<Creature> active = new List<Creature>();
        private readonly List<IStimulusSource> sources = new List<IStimulusSource>();
        private readonly List<CreatureStimulus> stimuli = new List<CreatureStimulus>();
        private readonly List<CreatureStimulus> pulses = new List<CreatureStimulus>();
        private readonly List<float> pulseEnds = new List<float>();
        private readonly List<Creature> eaten = new List<Creature>();
        private readonly Plane[] frustum = new Plane[6];
        private DiverBiteReceiver biteReceiver;
        private readonly int[] lodCounts = new int[3];
        private Transform poolRoot;
        private int nextSlot;

        // Griglia dei vicini, ricostruita a ogni frame: tabella hash di celle con liste concatenate negli array
        // (testa per secchio, successivo per creatura). Si rialloca solo quando le creature attive crescono.
        private int[] bucketHead = new int[64];
        private int[] entryNext = new int[32];
        private Vector3Int[] entryCell = new Vector3Int[32];
        private Vector3[] entryPosition = new Vector3[32];
        private int gridCount;
        // Anelli di celle guardati al massimo attorno a una creatura: il raggio del banco arriva a 3 celle.
        private const int MaxRings = 3;

        /// <summary>True se il manager ha un riferimento al sub.</summary>
        public bool HasPlayer => player != null;

        /// <summary>Posizione del sub letta all'inizio del frame.</summary>
        public Vector3 PlayerPosition { get; private set; }

        /// <summary>Stimoli raccolti all'inizio di questo frame da tutte le sorgenti registrate.</summary>
        public IReadOnlyList<CreatureStimulus> Stimuli => stimuli;

        /// <summary>Layer che bloccano vista e movimento delle creature.</summary>
        public LayerMask ObstacleMask => obstacleMask;

        /// <summary>Creature attive in questo momento.</summary>
        public int ActiveCount => active.Count;

        /// <summary>Creature attive nel livello indicato durante l'ultimo frame.</summary>
        public int CountAtLod(CreatureLod lod) => lodCounts[(int)lod];

        private void Awake()
        {
            poolRoot = new GameObject("CreaturePool").transform;
            poolRoot.SetParent(transform, false);
            if (player != null)
            {
                sources.Add(player);
                biteReceiver = player.GetComponent<DiverBiteReceiver>();
            }
            else
                Debug.LogWarning("[CreatureManager] Player non assegnato: le creature non percepiranno il sub.", this);
        }

        /// <summary>
        /// Il morso di un predatore è arrivato al sub: lo passa al <see cref="DiverBiteReceiver"/> sullo stesso
        /// GameObject del player. Senza ricevitore il morso non ha effetti.
        /// </summary>
        internal void ReportBite(Creature biter)
        {
            if (biteReceiver != null) biteReceiver.ReceiveBite(biter);
        }

        /// <summary>
        /// Una preda è stata presa: il suo lampo d'allarme (se la specie ne ha uno) fa scappare i compagni, e lei
        /// torna nel pool alla fine del frame, così la lista delle creature non cambia mentre il manager la scorre.
        /// </summary>
        internal void Eat(Creature prey)
        {
            if (prey == null || prey.ActiveIndex < 0 || eaten.Contains(prey)) return;
            CreatureProfile species = prey.Profile;
            if (species.alarmRadius > 0f)
            {
                CreatureStimulus alarm = CreatureStimulus.PointLight(prey.transform.position, species.alarmRadius,
                    StimulusCategory.Flash, false);
                alarm.Emitter = prey;
                AddPulse(alarm, 0.4f);
            }
            eaten.Add(prey);
        }

        /// <summary>
        /// La creatura attiva della specie indicata più vicina a <paramref name="position"/> entro
        /// <paramref name="radius"/>, o null. Scorre tutte le creature attive: va bene per pochi predatori.
        /// </summary>
        internal Creature FindNearest(CreatureProfile species, Vector3 position, float radius)
        {
            Creature best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < active.Count; i++)
            {
                Creature c = active[i];
                if (!ReferenceEquals(c.Profile, species) || eaten.Contains(c)) continue;
                float sqr = (c.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>Registra una sorgente di stimoli (per esempio una creatura luminosa). Ignora i duplicati.</summary>
        public void AddSource(IStimulusSource source)
        {
            if (source != null && !sources.Contains(source)) sources.Add(source);
        }

        /// <summary>Toglie una sorgente di stimoli registrata.</summary>
        public void RemoveSource(IStimulusSource source) => sources.Remove(source);

        /// <summary>
        /// Aggiunge uno stimolo breve che resta attivo per <paramref name="duration"/> secondi senza bisogno di una
        /// sorgente registrata (per esempio il lampo di un pesce spaventato). Entra negli stimoli dal frame dopo.
        /// </summary>
        public void AddPulse(CreatureStimulus stimulus, float duration)
        {
            pulses.Add(stimulus);
            pulseEnds.Add(Time.time + duration);
        }

        private void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnDestroy()
        {
            foreach (ObjectPool<Creature> pool in pools.Values) pool.Dispose();
            pools.Clear();
            active.Clear();
        }

        /// <summary>Crea in anticipo <paramref name="count"/> istanze della specie, così lo spawn non istanzia in gioco.</summary>
        public void Prewarm(CreatureProfile profile, int count)
        {
            ObjectPool<Creature> pool = GetPool(profile);
            int missing = count - pool.CountInactive;
            if (missing <= 0) return;

            var buffer = ListPool<Creature>.Get();
            for (int i = 0; i < missing; i++) buffer.Add(pool.Get());
            foreach (Creature creature in buffer) pool.Release(creature);
            ListPool<Creature>.Release(buffer);
        }

        /// <summary>
        /// Prende una creatura dal pool della specie e la attiva. <paramref name="home"/> e
        /// <paramref name="leashRadius"/> definiscono la zona entro cui vaga, <paramref name="ceilingY"/> la quota
        /// assoluta che non supera. Ritorna null se il profilo non ha prefab.
        /// </summary>
        public Creature Spawn(CreatureProfile profile, Vector3 position, Quaternion rotation, Vector3 home, float leashRadius,
            float ceilingY = float.PositiveInfinity)
        {
            if (profile == null || profile.prefab == null)
            {
                Debug.LogError("[CreatureManager] Profilo nullo o senza prefab: spawn annullato.", this);
                return null;
            }

            Creature creature = GetPool(profile).Get();
            creature.transform.SetPositionAndRotation(position, rotation);
            creature.Slot = nextSlot++;
            creature.ActiveIndex = active.Count;
            active.Add(creature);
            creature.OnSpawn(this, profile, home, leashRadius, ceilingY);
            return creature;
        }

        /// <summary>Rimette la creatura nel pool della sua specie. Ignora creature già nel pool.</summary>
        public void Despawn(Creature creature)
        {
            if (creature == null || creature.ActiveIndex < 0) return;

            // Rimozione con scambio dall'ultimo posto: O(1), l'ordine della lista non conta.
            int index = creature.ActiveIndex;
            int last = active.Count - 1;
            active[index] = active[last];
            active[index].ActiveIndex = index;
            active.RemoveAt(last);
            creature.ActiveIndex = -1;

            GetPool(creature.Profile).Release(creature);
        }

        private ObjectPool<Creature> GetPool(CreatureProfile profile)
        {
            if (pools.TryGetValue(profile, out ObjectPool<Creature> pool)) return pool;

            pool = new ObjectPool<Creature>(
                createFunc: () =>
                {
                    Creature creature = Instantiate(profile.prefab, poolRoot);
                    creature.name = profile.prefab.name;
                    creature.gameObject.SetActive(false);
                    return creature;
                },
                actionOnGet: creature => creature.gameObject.SetActive(true),
                actionOnRelease: creature => creature.gameObject.SetActive(false),
                actionOnDestroy: creature => { if (creature != null) Destroy(creature.gameObject); },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 1024);
            pools.Add(profile, pool);
            return pool;
        }

        private void Update()
        {
            int frame = Time.frameCount;
            float deltaTime = Time.deltaTime;

            Vector3 origin;
            if (player != null)
            {
                PlayerPosition = player.transform.position;
                origin = PlayerPosition;
            }
            else
            {
                origin = viewCamera != null ? viewCamera.transform.position : transform.position;
            }

            stimuli.Clear();
            for (int i = 0; i < sources.Count; i++) sources[i].EmitStimuli(stimuli);
            float now = Time.time;
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                if (now < pulseEnds[i])
                {
                    stimuli.Add(pulses[i]);
                    continue;
                }
                int last = pulses.Count - 1;
                pulses[i] = pulses[last];
                pulseEnds[i] = pulseEnds[last];
                pulses.RemoveAt(last);
                pulseEnds.RemoveAt(last);
            }

            BuildNeighbourGrid();

            bool hasCamera = viewCamera != null;
            if (hasCamera) GeometryUtility.CalculateFrustumPlanes(viewCamera, frustum);

            float sqrNear = nearDistance * nearDistance;
            float sqrFar = farDistance * farDistance;
            Vector3 margin = Vector3.one * (visibilityMargin * 2f);
            lodCounts[0] = lodCounts[1] = lodCounts[2] = 0;

            // All'indietro: se un giorno una creatura si rimette nel pool durante il suo Tick, la lista resta valida.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Creature creature = active[i];
                Vector3 position = creature.transform.position;
                float sqrDistance = (position - origin).sqrMagnitude;

                CreatureLod lod;
                if (sqrDistance > sqrFar)
                    lod = CreatureLod.Far;
                else if (sqrDistance > sqrNear || (hasCamera &&
                         !GeometryUtility.TestPlanesAABB(frustum, new Bounds(position, margin))))
                    lod = CreatureLod.Mid;
                else
                    lod = CreatureLod.Near;

                creature.Lod = lod;
                lodCounts[(int)lod]++;

                bool think;
                bool probe;
                switch (lod)
                {
                    case CreatureLod.Near:
                        think = probe = true;
                        break;
                    case CreatureLod.Mid:
                        think = probe = (frame + creature.Slot) % midInterval == 0;
                        break;
                    default:
                        think = (frame + creature.Slot) % farInterval == 0;
                        probe = false;
                        break;
                }

                creature.Tick(deltaTime, think, probe);
            }

            for (int i = 0; i < eaten.Count; i++) Despawn(eaten[i]);
            eaten.Clear();
        }

        private void BuildNeighbourGrid()
        {
            gridCount = active.Count;
            if (entryNext.Length < gridCount)
            {
                int size = Mathf.NextPowerOfTwo(gridCount);
                entryNext = new int[size];
                entryCell = new Vector3Int[size];
                entryPosition = new Vector3[size];
                bucketHead = new int[size * 2];
            }

            System.Array.Fill(bucketHead, -1);
            int mask = bucketHead.Length - 1;
            for (int i = 0; i < gridCount; i++)
            {
                Vector3 position = active[i].transform.position;
                Vector3Int cell = CellOf(position);
                int bucket = HashCell(cell) & mask;
                entryPosition[i] = position;
                entryCell[i] = cell;
                entryNext[i] = bucketHead[bucket];
                bucketHead[bucket] = i;
            }
        }

        private Vector3Int CellOf(Vector3 position)
        {
            float inverse = 1f / neighbourCellSize;
            return new Vector3Int(Mathf.FloorToInt(position.x * inverse), Mathf.FloorToInt(position.y * inverse),
                Mathf.FloorToInt(position.z * inverse));
        }

        private static int HashCell(Vector3Int cell) => (cell.x * 73856093) ^ (cell.y * 19349663) ^ (cell.z * 83492791);

        /// <summary>
        /// Vicini della stessa specie entro <paramref name="radius"/>, con le posizioni di inizio frame. Ritorna il
        /// numero di vicini usati e, sommati su di loro: la spinta di separazione (lontano da chi è più vicino di
        /// <paramref name="separationDistance"/>, più forte quanto più è vicino), il baricentro e la media delle
        /// rotte. Cerca ad anelli di celle dal centro verso l'esterno: il primo anello (27 celle) contiene tutti i
        /// vicini troppo vicini, purché <paramref name="separationDistance"/> non superi il lato della cella, e la
        /// separazione li conta tutti; baricentro e rotte si fermano a <paramref name="maxNeighbours"/> vicini e,
        /// raggiunti quelli, gli anelli esterni non vengono guardati. Così in un banco fitto ogni pesce esamina
        /// solo le celle attorno a sé invece dell'intero banco.
        /// </summary>
        internal int Flock(Creature self, Vector3 position, float radius, float separationDistance, int maxNeighbours,
            out Vector3 separation, out Vector3 centre, out Vector3 alignment)
        {
            separation = centre = alignment = Vector3.zero;
            float sqrRadius = radius * radius;
            float sqrSeparation = separationDistance * separationDistance;
            int rings = Mathf.Clamp(Mathf.CeilToInt(radius / neighbourCellSize), 1, MaxRings);
            Vector3Int home = CellOf(position);
            CreatureProfile species = self.Profile;
            int mask = bucketHead.Length - 1;
            int count = 0;

            for (int ring = 1; ring <= rings; ring++)
            {
                if (ring > 1 && count >= maxNeighbours) break;
                for (int dx = -ring; dx <= ring; dx++)
                for (int dy = -ring; dy <= ring; dy++)
                for (int dz = -ring; dz <= ring; dz++)
                {
                    // Il primo giro copre il cubo 3x3x3, i successivi solo il guscio esterno del cubo.
                    if (ring > 1 && Mathf.Max(Mathf.Abs(dx), Mathf.Max(Mathf.Abs(dy), Mathf.Abs(dz))) != ring)
                        continue;
                    Vector3Int cell = new Vector3Int(home.x + dx, home.y + dy, home.z + dz);
                    for (int j = bucketHead[HashCell(cell) & mask]; j >= 0; j = entryNext[j])
                    {
                        // Il secchio può contenere altre celle con lo stesso hash: conta solo quella cercata.
                        if (entryCell[j] != cell || j >= active.Count) continue;
                        Vector3 offset = position - entryPosition[j];
                        float sqrDistance = offset.sqrMagnitude;
                        if (sqrDistance > sqrRadius || sqrDistance < 1e-6f) continue;
                        bool close = sqrDistance < sqrSeparation;
                        if (!close && count >= maxNeighbours) continue;
                        Creature other = active[j];
                        if (ReferenceEquals(other, self) || !ReferenceEquals(other.Profile, species)) continue;

                        if (close)
                        {
                            float distance = Mathf.Sqrt(sqrDistance);
                            separation += offset / distance * (1f - distance / separationDistance);
                        }
                        if (count >= maxNeighbours) continue;
                        centre += entryPosition[j];
                        alignment += other.Heading;
                        count++;
                    }
                }
            }
            return count;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = player != null ? player.transform.position : transform.position;
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(origin, nearDistance);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(origin, farDistance);
        }
    }
}

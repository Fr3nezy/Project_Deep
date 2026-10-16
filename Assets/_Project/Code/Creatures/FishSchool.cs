using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Banco di pesci ambientali simulato sulla GPU: centinaia di pesci senza intelligenza che nuotano insieme in
    /// un volume ellissoidale attorno a questo GameObject e si aprono attorno al sub e al fascio della torcia. È la
    /// seconda forma del pesce ambientale accanto alla <see cref="Creature"/>: stessa mesh e stesso materiale
    /// (shader <c>Deeplonauts/Fish</c>, percorso di instancing procedurale), nessun GameObject per pesce.
    /// <para>
    /// A ogni <c>LateUpdate</c> (dopo che il <see cref="CreatureManager"/> ha raccolto gli stimoli del frame) un
    /// dispatch di <c>FishSchool.compute</c> fa avanzare tutti i pesci, li scarta fuori dall'inquadratura e oltre
    /// <see cref="cullDistance"/>, e li mette nella lista del loro LOD; poi due <c>Graphics.RenderMeshIndirect</c>
    /// disegnano LOD0 e LOD1 con il conteggio scritto dalla GPU. La CPU non legge mai i pesci.
    /// </para>
    /// Niente collisioni: il volume va tenuto in acqua libera, lontano da rocce e fondale.
    /// <para>
    /// Con un <see cref="path"/> il volume viaggia lungo i waypoint (avanti e indietro, o in cerchio) a
    /// <see cref="roamSpeed"/>, ruotando sull'asse verticale verso la direzione di marcia: i pesci lo seguono e il
    /// banco si sposta invece di girare sempre nello stesso punto. Il percorso va tracciato in acqua libera.
    /// </para>
    /// Gira dopo la camera (<c>DiverController.RotateView</c> a ordine 0, <c>RotationalFollowThrough</c> a 200):
    /// il cull sull'inquadratura usa la camera di questo frame, altrimenti girando la testa i pesci ai bordi
    /// mancavano per un frame.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class FishSchool : MonoBehaviour
    {
        private const int Threads = 256;
        private const int MaxBeams = 4;
        private const int FishStride = 48;
        /// <summary>Byte di una voce della lista di disegno: deve coincidere con FishDraw nel compute e nello shader.</summary>
        private const int DrawStride = 80;

        [StructLayout(LayoutKind.Sequential)]
        private struct FishData
        {
            public Vector3 Position;
            public float Phase;
            public Vector3 Velocity;
            public float Bend;
            public float Panic;
            public float Scale;
            public float Seed;
            public float Pad;
        }

        [Header("Riferimenti")]
        [Tooltip("Manager delle creature della scena: da qui arrivano la posizione del sub e i fasci della torcia. " +
                 "Senza, il banco nuota ma non reagisce al sub.")]
        [SerializeField] private CreatureManager manager;

        [Tooltip("FishSchool.compute.")]
        [SerializeField] private ComputeShader simulation;

        [Tooltip("Mesh del pesce vicino (LOD0), lungo +Z come per lo shader Fish.")]
        [SerializeField] private Mesh mesh;

        [Tooltip("Mesh del pesce lontano (LOD1), allineata a quella del LOD0. Vuota = LOD0 fino al cull.")]
        [SerializeField] private Mesh meshLod1;

        [Tooltip("Materiale con lo shader Deeplonauts/Fish (lo stesso delle creature).")]
        [SerializeField] private Material material;

        [Tooltip("Camera per il cull e il LOD. Vuota = Camera.main all'avvio.")]
        [SerializeField] private Camera viewCamera;

        [Header("Banco")]
        [Tooltip("Numero di pesci.")]
        [SerializeField, Range(1, 8192)] private int count = 500;

        [Tooltip("Semiassi dell'ellissoide del banco (m), negli assi locali di questo GameObject (la scala è ignorata).")]
        [SerializeField] private Vector3 volumeExtents = new Vector3(6f, 2f, 6f);

        [Tooltip("Fascia interna al bordo, in frazione del semiasse, in cui i pesci vengono richiamati verso il centro.")]
        [SerializeField, Range(0.05f, 1f)] private float volumeSoftness = 0.3f;

        [Tooltip("Forza del richiamo verso il centro al bordo del volume (m/s²).")]
        [SerializeField, Min(0f)] private float containWeight = 4f;

        [Tooltip("Quanto cala il richiamo per un pesce in panico pieno (0-1): spaventato, si spinge fino al limite " +
                 "duro del volume (1.2 volte i semiassi) invece di restare nel banco.")]
        [SerializeField, Range(0f, 1f)] private float panicContainRelief = 0.7f;

        [Tooltip("Spinta tangente attorno all'asse verticale del volume (m/s²): il banco gira lentamente su sé stesso.")]
        [SerializeField] private float swirl = 0.3f;

        [Tooltip("Scala casuale di ogni pesce (min, max).")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.85f, 1.1f);

        [Tooltip("Seme della disposizione iniziale.")]
        [SerializeField] private int seed = 1;

        [Header("Spostamento")]
        [Tooltip("Waypoint del percorso del banco, in acqua libera. Vuoto = il banco resta attorno a questo GameObject.")]
        [SerializeField] private Transform[] path = new Transform[0];

        [Tooltip("Acceso: dopo l'ultimo waypoint torna al primo. Spento: avanti e indietro.")]
        [SerializeField] private bool loopPath;

        [Tooltip("Velocità del volume lungo il percorso (m/s). Sotto la velocità di crociera dei pesci, che così lo seguono.")]
        [SerializeField, Min(0f)] private float roamSpeed = 0.45f;

        [Tooltip("Gradi al secondo con cui il volume si gira verso la direzione di marcia.")]
        [SerializeField, Min(1f)] private float roamTurnRate = 20f;

        [Tooltip("Quanto i pesci sono spinti nella direzione di marcia del volume (1/s): accelerazione = velocità del " +
                 "volume × questo valore.")]
        [SerializeField, Min(0f)] private float followWeight = 1.5f;

        [Header("Comportamento di banco")]
        [Tooltip("Distanza entro cui un pesce conta come vicino per coesione e allineamento (m).")]
        [SerializeField, Min(0.1f)] private float neighbourRadius = 1.2f;

        [Tooltip("Distanza sotto cui due pesci si allontanano (m).")]
        [SerializeField, Min(0.05f)] private float separationDistance = 0.45f;

        [SerializeField, Min(0f)] private float separationWeight = 6f;
        [SerializeField, Min(0f)] private float cohesionWeight = 0.6f;
        [SerializeField, Min(0f)] private float alignmentWeight = 1.5f;

        [Tooltip("Rumore lento sulla rotta di ogni pesce (m/s²).")]
        [SerializeField, Min(0f)] private float wanderWeight = 0.3f;

        [Tooltip("Velocità a cui il banco torna quando niente lo spinge (m/s).")]
        [SerializeField, Min(0.05f)] private float cruiseSpeed = 0.8f;

        [SerializeField, Min(0f)] private float minSpeed = 0.4f;
        [SerializeField, Min(0.05f)] private float maxSpeed = 1.3f;

        [Tooltip("Velocità massima in fuga, con il panico pieno (m/s).")]
        [SerializeField, Min(0.05f)] private float panicSpeed = 3.5f;

        [Tooltip("Accelerazione massima a riposo (m/s²); in fuga fino al triplo.")]
        [SerializeField, Min(0.1f)] private float maxAcceleration = 3f;

        [Tooltip("Smorzamento della velocità verticale (1/s): i pesci restano quasi orizzontali.")]
        [SerializeField, Min(0f)] private float verticalDamping = 2f;

        [Header("Repulsione dal sub")]
        [Tooltip("Raggio della bolla vuota attorno al sub (m). 0 = nessuna repulsione.")]
        [SerializeField, Min(0f)] private float diverRadius = 3.5f;

        [Tooltip("Spinta al centro della bolla (m/s²).")]
        [SerializeField, Min(0f)] private float diverWeight = 12f;

        [Tooltip("Centro della bolla rispetto alla posizione del sub letta dal manager (il PlayerCapsule).")]
        [SerializeField] private Vector3 diverOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Repulsione dal fascio")]
        [Tooltip("Spinta fuori dal fascio Full sull'asse, vicino alla lampada (m/s²).")]
        [SerializeField, Min(0f)] private float strongLightWeight = 10f;

        [Tooltip("Spinta fuori dal fascio Low (m/s²).")]
        [SerializeField, Min(0f)] private float weakLightWeight = 5f;

        [Tooltip("Spinta fuori dal flash (m/s²).")]
        [SerializeField, Min(0f)] private float flashWeight = 20f;

        [Tooltip("Margine attorno al cono del fascio (m).")]
        [SerializeField, Min(0f)] private float beamMargin = 0.6f;

        [Tooltip("Semi-angolo del cono che respinge, in frazione di quello del fascio: la luce piena (57°) coprirebbe " +
                 "tutto il banco da 10 m e il banco non avrebbe dove andare; il nucleo apre un tunnel dove si punta.")]
        [SerializeField, Range(0.1f, 1f)] private float beamConeScale = 0.4f;

        [Tooltip("Portata del fascio per il banco, in frazione della portata dello stimolo (Full 25 m, Low 8 m).")]
        [SerializeField, Range(0.05f, 1f)] private float beamReachScale = 1f;

        [Tooltip("Secondi in cui il panico di un pesce si dimezza circa, una volta fuori pericolo.")]
        [SerializeField, Min(0.05f)] private float panicDecay = 1.5f;

        [Header("Aspetto")]
        [Tooltip("Colpi di coda al secondo alla velocità di crociera, come Creature.swimFrequency.")]
        [SerializeField, Min(0f)] private float swimFrequency = 2.5f;

        [Tooltip("Velocità di virata (°/s) a cui il corpo è piegato del tutto, come Creature.fullBendTurnRate.")]
        [SerializeField, Min(1f)] private float fullBendTurnRate = 120f;

        [Tooltip("Lampo dei fotofori con il panico pieno (_GlowBoost). 0 = nessun lampo.")]
        [SerializeField, Min(0f)] private float panicGlow = 1.5f;

        [Header("LOD e cull")]
        [Tooltip("Distanza dalla camera del passaggio a LOD1 (m), al centro della dissolvenza.")]
        [SerializeField, Min(0f)] private float lod1Distance = 8f;

        [Tooltip("Larghezza della dissolvenza a retino fra LOD0 e LOD1 (m).")]
        [SerializeField, Min(0.01f)] private float lodFadeWidth = 1.5f;

        [Tooltip("Oltre questa distanza dalla camera i pesci non si disegnano (m).")]
        [SerializeField, Min(1f)] private float cullDistance = 35f;

        [Tooltip("Larghezza della dissolvenza prima del cull (m).")]
        [SerializeField, Min(0.01f)] private float cullFadeWidth = 4f;

        [Tooltip("Raggio della sfera di un pesce per il cull sull'inquadratura (m).")]
        [SerializeField, Min(0.05f)] private float cullRadius = 0.35f;

        private static readonly int FishInId = Shader.PropertyToID("_FishIn");
        private static readonly int FishOutId = Shader.PropertyToID("_FishOut");
        private static readonly int DrawLod0Id = Shader.PropertyToID("_DrawLod0");
        private static readonly int DrawLod1Id = Shader.PropertyToID("_DrawLod1");
        private static readonly int SchoolDrawsId = Shader.PropertyToID("_SchoolDraws");
        private static readonly int CountId = Shader.PropertyToID("_Count");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int TimeId = Shader.PropertyToID("_SimTime");
        private static readonly int WorldToVolumeId = Shader.PropertyToID("_WorldToVolume");
        private static readonly int VolumeToWorldId = Shader.PropertyToID("_VolumeToWorld");
        private static readonly int VolumeCentreId = Shader.PropertyToID("_VolumeCentre");
        private static readonly int VolumeVelocityId = Shader.PropertyToID("_VolumeVelocity");
        private static readonly int FollowWeightId = Shader.PropertyToID("_FollowWeight");
        private static readonly int VolumeSoftnessId = Shader.PropertyToID("_VolumeSoftness");
        private static readonly int ContainWeightId = Shader.PropertyToID("_ContainWeight");
        private static readonly int PanicContainReliefId = Shader.PropertyToID("_PanicContainRelief");
        private static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        private static readonly int NeighbourRadiusId = Shader.PropertyToID("_NeighbourRadius");
        private static readonly int SeparationDistanceId = Shader.PropertyToID("_SeparationDistance");
        private static readonly int SeparationWeightId = Shader.PropertyToID("_SeparationWeight");
        private static readonly int CohesionWeightId = Shader.PropertyToID("_CohesionWeight");
        private static readonly int AlignmentWeightId = Shader.PropertyToID("_AlignmentWeight");
        private static readonly int WanderWeightId = Shader.PropertyToID("_WanderWeight");
        private static readonly int CruiseSpeedId = Shader.PropertyToID("_CruiseSpeed");
        private static readonly int MinSpeedId = Shader.PropertyToID("_MinSpeed");
        private static readonly int MaxSpeedId = Shader.PropertyToID("_MaxSpeed");
        private static readonly int PanicSpeedId = Shader.PropertyToID("_PanicSpeed");
        private static readonly int MaxAccelId = Shader.PropertyToID("_MaxAccel");
        private static readonly int VerticalDampingId = Shader.PropertyToID("_VerticalDamping");
        private static readonly int DiverId = Shader.PropertyToID("_Diver");
        private static readonly int DiverWeightId = Shader.PropertyToID("_DiverWeight");
        private static readonly int BeamCountId = Shader.PropertyToID("_BeamCount");
        private static readonly int BeamOriginId = Shader.PropertyToID("_BeamOrigin");
        private static readonly int BeamAxisId = Shader.PropertyToID("_BeamAxis");
        private static readonly int BeamParamsId = Shader.PropertyToID("_BeamParams");
        private static readonly int PanicDecayId = Shader.PropertyToID("_PanicDecay");
        private static readonly int SwimFrequencyId = Shader.PropertyToID("_SwimFrequency");
        private static readonly int FullBendTurnRateId = Shader.PropertyToID("_FullBendTurnRate");
        private static readonly int PanicGlowId = Shader.PropertyToID("_PanicGlow");
        private static readonly int CameraPositionId = Shader.PropertyToID("_CameraPosition");
        private static readonly int FrustumPlanesId = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int Lod1DistanceId = Shader.PropertyToID("_Lod1Distance");
        private static readonly int LodFadeWidthId = Shader.PropertyToID("_LodFadeWidth");
        private static readonly int CullDistanceId = Shader.PropertyToID("_CullDistance");
        private static readonly int CullFadeWidthId = Shader.PropertyToID("_CullFadeWidth");
        private static readonly int CullRadiusId = Shader.PropertyToID("_CullRadius");

        private GraphicsBuffer fishRead;
        private GraphicsBuffer fishWrite;
        private GraphicsBuffer drawLod0;
        private GraphicsBuffer drawLod1;
        private GraphicsBuffer args;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] argsData = new GraphicsBuffer.IndirectDrawIndexedArgs[2];
        private MaterialPropertyBlock propsLod0;
        private MaterialPropertyBlock propsLod1;
        private int kernel;
        private int allocated;

        // Volume in viaggio: centro, rotazione, velocità, waypoint verso cui va e verso di percorrenza (+1/-1).
        private Vector3 centre;
        private Quaternion volumeRotation;
        private Vector3 volumeVelocity;
        private int waypoint;
        private int pathDirection = 1;
        private bool volumeReady;

        private readonly Plane[] planes = new Plane[6];
        private readonly Vector4[] planeVectors = new Vector4[6];
        private readonly Vector4[] beamOrigins = new Vector4[MaxBeams];
        private readonly Vector4[] beamAxes = new Vector4[MaxBeams];
        private readonly Vector4[] beamParams = new Vector4[MaxBeams];

        /// <summary>Numero di pesci richiesto nell'Inspector.</summary>
        public int Count => count;

        /// <summary>Pesci nei buffer della GPU in questo momento (0 se il banco non è attivo).</summary>
        public int AllocatedCount => allocated;

        /// <summary>Fasci della torcia passati al compute nell'ultimo frame (0-4).</summary>
        public int BeamCount { get; private set; }

        /// <summary>Centro attuale del volume del banco (si sposta lungo il percorso).</summary>
        public Vector3 VolumeCentre => volumeReady ? centre : transform.position;

        private void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("[FishSchool] Compute shader non supportati: il banco resta spento.", this);
                enabled = false;
                return;
            }
            if (simulation == null || mesh == null || material == null)
            {
                Debug.LogWarning("[FishSchool] Mancano compute, mesh o materiale: il banco resta spento.", this);
                enabled = false;
                return;
            }
            if (manager == null)
                Debug.LogWarning("[FishSchool] Manager non assegnato: il banco non reagirà al sub.", this);
            kernel = simulation.FindKernel("Simulate");
            centre = transform.position;
            volumeRotation = transform.rotation;
            volumeVelocity = Vector3.zero;
            waypoint = 0;
            pathDirection = 1;
            volumeReady = true;
            Allocate();
        }

        private void OnDisable() => Release();

        private void Allocate()
        {
            Release();
            allocated = count;
            fishRead = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allocated, FishStride);
            fishWrite = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allocated, FishStride);
            drawLod0 = new GraphicsBuffer(GraphicsBuffer.Target.Append, allocated, DrawStride);
            drawLod1 = new GraphicsBuffer(GraphicsBuffer.Target.Append, allocated, DrawStride);
            args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 2,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);

            Mesh far = meshLod1 != null ? meshLod1 : mesh;
            argsData[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(0), startIndex = mesh.GetIndexStart(0),
                baseVertexIndex = mesh.GetBaseVertex(0)
            };
            argsData[1] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = far.GetIndexCount(0), startIndex = far.GetIndexStart(0),
                baseVertexIndex = far.GetBaseVertex(0)
            };
            args.SetData(argsData);

            propsLod0 ??= new MaterialPropertyBlock();
            propsLod1 ??= new MaterialPropertyBlock();
            propsLod0.SetBuffer(SchoolDrawsId, drawLod0);
            propsLod1.SetBuffer(SchoolDrawsId, drawLod1);

            var fish = new FishData[allocated];
            var random = new System.Random(seed);
            for (int i = 0; i < allocated; i++)
            {
                // uniform inside the inner 80% of the unit sphere, then stretched to the ellipsoid
                Vector3 unit;
                do
                {
                    unit = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f,
                        (float)random.NextDouble() * 2f - 1f);
                } while (unit.sqrMagnitude > 1f);
                Vector3 position = VolumeToWorld().MultiplyPoint3x4(unit * 0.8f);
                float angle = (float)random.NextDouble() * 2f * Mathf.PI;
                fish[i] = new FishData
                {
                    Position = position,
                    Velocity = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * cruiseSpeed,
                    Phase = (float)random.NextDouble() * 2f * Mathf.PI,
                    Scale = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)random.NextDouble()),
                    Seed = (float)random.NextDouble() * 100f
                };
            }
            fishRead.SetData(fish);
            fishWrite.SetData(fish);
        }

        private void Release()
        {
            fishRead?.Release();
            fishWrite?.Release();
            drawLod0?.Release();
            drawLod1?.Release();
            args?.Release();
            fishRead = fishWrite = drawLod0 = drawLod1 = args = null;
            allocated = 0;
        }

        private Matrix4x4 VolumeToWorld() => Matrix4x4.TRS(volumeReady ? centre : transform.position,
            volumeReady ? volumeRotation : transform.rotation, Vector3.Max(volumeExtents, Vector3.one * 0.1f));

        /// <summary>
        /// Fa avanzare il volume verso il waypoint corrente; arrivato a meno di 1 m passa al successivo (in cerchio o
        /// avanti e indietro). Velocità e virata cambiano piano, così il banco non scatta.
        /// </summary>
        private void Roam(float deltaTime)
        {
            int valid = 0;
            for (int i = 0; i < path.Length; i++) if (path[i] != null) valid++;
            if (valid == 0 || roamSpeed <= 0f)
            {
                volumeVelocity = Vector3.MoveTowards(volumeVelocity, Vector3.zero, deltaTime);
                centre += volumeVelocity * deltaTime;
                return;
            }

            waypoint = Mathf.Clamp(waypoint, 0, path.Length - 1);
            Transform target = path[waypoint];
            Vector3 toTarget = target != null ? target.position - centre : Vector3.zero;
            if (target == null || toTarget.magnitude < 1f)
            {
                NextWaypoint();
                target = path[waypoint];
                toTarget = target != null ? target.position - centre : Vector3.zero;
            }

            // Rallenta solo sull'ultimo tratto prima di un'inversione; in cerchio passa oltre senza fermarsi.
            float distance = toTarget.magnitude;
            bool turningBack = !loopPath && (waypoint == 0 || waypoint == path.Length - 1);
            float speed = roamSpeed * (turningBack ? Mathf.Clamp01(distance / 4f) : 1f);
            Vector3 desired = distance > 1e-3f ? toTarget / distance * speed : Vector3.zero;
            volumeVelocity = Vector3.MoveTowards(volumeVelocity, desired, roamSpeed * 0.3f * deltaTime);
            centre += volumeVelocity * deltaTime;

            // L'ellissoide è simmetrico: segue l'asse di marcia, non il verso. Così all'inversione non ruota di
            // 180° passando di traverso, con l'asse lungo contro le pareti di un canyon.
            Vector3 flat = new Vector3(volumeVelocity.x, 0f, volumeVelocity.z);
            if (flat.sqrMagnitude > 1e-4f)
            {
                Vector3 forward = volumeRotation * Vector3.forward;
                if (Vector3.Dot(forward, flat) < 0f) flat = -flat;
                volumeRotation = Quaternion.RotateTowards(volumeRotation, Quaternion.LookRotation(flat),
                    roamTurnRate * deltaTime);
            }
        }

        private void NextWaypoint()
        {
            if (path.Length < 2) return;
            if (loopPath)
            {
                waypoint = (waypoint + 1) % path.Length;
                return;
            }
            if (waypoint + pathDirection < 0 || waypoint + pathDirection >= path.Length) pathDirection = -pathDirection;
            waypoint += pathDirection;
        }

        private void LateUpdate()
        {
            if (allocated != count) Allocate();

            float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
            Roam(deltaTime);
            Matrix4x4 volumeToWorld = VolumeToWorld();
            ComputeShader cs = simulation;
            cs.SetInt(CountId, allocated);
            cs.SetFloat(DeltaTimeId, deltaTime);
            cs.SetFloat(TimeId, Time.time);
            cs.SetMatrix(VolumeToWorldId, volumeToWorld);
            cs.SetMatrix(WorldToVolumeId, volumeToWorld.inverse);
            cs.SetVector(VolumeCentreId, centre);
            cs.SetVector(VolumeVelocityId, volumeVelocity);
            cs.SetFloat(FollowWeightId, followWeight);
            cs.SetFloat(VolumeSoftnessId, volumeSoftness);
            cs.SetFloat(ContainWeightId, containWeight);
            cs.SetFloat(PanicContainReliefId, panicContainRelief);
            // In viaggio il vortice si spegne: con il banco che gira su sé stesso metà dei pesci nuoterebbe all'indietro.
            float travel = roamSpeed > 0f ? Mathf.Clamp01(volumeVelocity.magnitude / roamSpeed) : 0f;
            cs.SetFloat(SwirlId, swirl * (1f - 0.8f * travel));
            cs.SetFloat(NeighbourRadiusId, neighbourRadius);
            cs.SetFloat(SeparationDistanceId, separationDistance);
            cs.SetFloat(SeparationWeightId, separationWeight);
            cs.SetFloat(CohesionWeightId, cohesionWeight);
            cs.SetFloat(AlignmentWeightId, alignmentWeight);
            cs.SetFloat(WanderWeightId, wanderWeight);
            cs.SetFloat(CruiseSpeedId, cruiseSpeed);
            cs.SetFloat(MinSpeedId, Mathf.Min(minSpeed, maxSpeed));
            cs.SetFloat(MaxSpeedId, maxSpeed);
            cs.SetFloat(PanicSpeedId, Mathf.Max(panicSpeed, maxSpeed));
            cs.SetFloat(MaxAccelId, maxAcceleration);
            cs.SetFloat(VerticalDampingId, verticalDamping);
            cs.SetFloat(PanicDecayId, panicDecay);
            cs.SetFloat(SwimFrequencyId, swimFrequency);
            cs.SetFloat(FullBendTurnRateId, fullBendTurnRate);
            cs.SetFloat(PanicGlowId, panicGlow);

            SetThreats(cs);
            SetCulling(cs);

            drawLod0.SetCounterValue(0);
            drawLod1.SetCounterValue(0);
            cs.SetBuffer(kernel, FishInId, fishRead);
            cs.SetBuffer(kernel, FishOutId, fishWrite);
            cs.SetBuffer(kernel, DrawLod0Id, drawLod0);
            cs.SetBuffer(kernel, DrawLod1Id, drawLod1);
            cs.Dispatch(kernel, (allocated + Threads - 1) / Threads, 1, 1);
            GraphicsBuffer.CopyCount(drawLod0, args, 4);
            GraphicsBuffer.CopyCount(drawLod1, args, GraphicsBuffer.IndirectDrawIndexedArgs.size + 4);
            (fishRead, fishWrite) = (fishWrite, fishRead);

            float reach = volumeExtents.magnitude * 1.2f + 1f;
            var bounds = new Bounds(centre, Vector3.one * reach * 2f);
            Draw(mesh, propsLod0, 0, bounds);
            if (meshLod1 != null) Draw(meshLod1, propsLod1, 1, bounds);
        }

        private void Draw(Mesh drawMesh, MaterialPropertyBlock props, int command, Bounds bounds)
        {
            var rp = new RenderParams(material)
            {
                worldBounds = bounds,
                matProps = props,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off
            };
            Graphics.RenderMeshIndirect(rp, drawMesh, args, 1, command);
        }

        /// <summary>Bolla del sub e fasci di luce (al massimo <see cref="MaxBeams"/>) dagli stimoli del manager.</summary>
        private void SetThreats(ComputeShader cs)
        {
            Vector4 diver = Vector4.zero;
            int beams = 0;
            if (manager != null && manager.HasPlayer)
            {
                Vector3 p = manager.PlayerPosition + diverOffset;
                diver = new Vector4(p.x, p.y, p.z, diverRadius);

                IReadOnlyList<CreatureStimulus> stimuli = manager.Stimuli;
                for (int i = 0; i < stimuli.Count && beams < MaxBeams; i++)
                {
                    CreatureStimulus s = stimuli[i];
                    if (!s.IsLight || s.ConeCos <= 0f) continue;
                    float weight = s.Category switch
                    {
                        StimulusCategory.StrongLight => strongLightWeight,
                        StimulusCategory.WeakLight => weakLightWeight,
                        StimulusCategory.Flash => flashWeight,
                        _ => 0f
                    };
                    if (weight <= 0f) continue;
                    float tanHalf = Mathf.Tan(Mathf.Acos(Mathf.Clamp01(s.ConeCos)) * beamConeScale);
                    float margin = s.Category == StimulusCategory.Flash ? beamMargin * 2f : beamMargin;
                    beamOrigins[beams] = new Vector4(s.Position.x, s.Position.y, s.Position.z, s.Radius * beamReachScale);
                    beamAxes[beams] = new Vector4(s.Direction.x, s.Direction.y, s.Direction.z, tanHalf);
                    beamParams[beams] = new Vector4(weight, margin, 0f, 0f);
                    beams++;
                }
            }
            BeamCount = beams;
            cs.SetVector(DiverId, diver);
            cs.SetFloat(DiverWeightId, diverWeight);
            cs.SetInt(BeamCountId, beams);
            cs.SetVectorArray(BeamOriginId, beamOrigins);
            cs.SetVectorArray(BeamAxisId, beamAxes);
            cs.SetVectorArray(BeamParamsId, beamParams);
        }

        private void SetCulling(ComputeShader cs)
        {
            Vector3 cameraPosition = transform.position;
            if (viewCamera != null)
            {
                cameraPosition = viewCamera.transform.position;
                GeometryUtility.CalculateFrustumPlanes(viewCamera, planes);
                for (int i = 0; i < 6; i++)
                    planeVectors[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z,
                        planes[i].distance);
            }
            else
            {
                // no camera: planes that let everything through
                for (int i = 0; i < 6; i++) planeVectors[i] = new Vector4(0f, 0f, 0f, 1f);
            }
            cs.SetVector(CameraPositionId, cameraPosition);
            cs.SetVectorArray(FrustumPlanesId, planeVectors);
            cs.SetFloat(Lod1DistanceId, meshLod1 != null ? lod1Distance : float.MaxValue);
            cs.SetFloat(LodFadeWidthId, lodFadeWidth);
            cs.SetFloat(CullDistanceId, cullDistance);
            cs.SetFloat(CullFadeWidthId, cullFadeWidth);
            cs.SetFloat(CullRadiusId, cullRadius);
        }

        /// <summary>
        /// Copia sulla CPU le posizioni di tutti i pesci (lettura sincrona dalla GPU: blocca il frame, solo per
        /// debug e verifica). Restituisce quanti pesci ha letto.
        /// </summary>
        public int ReadPositions(List<Vector3> output)
        {
            output.Clear();
            if (fishRead == null) return 0;
            var fish = new FishData[allocated];
            fishRead.GetData(fish);
            for (int i = 0; i < fish.Length; i++) output.Add(fish[i].Position);
            return fish.Length;
        }

        /// <summary>
        /// Pesci disegnati nell'ultimo frame in LOD0 (x) e LOD1 (y); nella fascia di dissolvenza un pesce conta in
        /// tutti e due. Lettura sincrona dalla GPU: solo per debug e verifica.
        /// </summary>
        public Vector2Int ReadDrawCounts()
        {
            if (args == null) return Vector2Int.zero;
            var data = new GraphicsBuffer.IndirectDrawIndexedArgs[2];
            args.GetData(data);
            return new Vector2Int((int)data[0].instanceCount, (int)data[1].instanceCount);
        }

        private void OnValidate()
        {
            if (sizeRange.y < sizeRange.x) sizeRange.y = sizeRange.x;
            if (maxSpeed < minSpeed) maxSpeed = minSpeed;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.matrix = VolumeToWorld();
            Gizmos.DrawWireSphere(Vector3.zero, 1f);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(Vector3.zero, 1f - volumeSoftness);
            Gizmos.matrix = Matrix4x4.identity;

            // percorso
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            Transform previous = null;
            foreach (Transform point in path)
            {
                if (point == null) continue;
                Gizmos.DrawWireSphere(point.position, 0.4f);
                if (previous != null) Gizmos.DrawLine(previous.position, point.position);
                previous = point;
            }
            if (loopPath && previous != null && path.Length > 1 && path[0] != null)
                Gizmos.DrawLine(previous.position, path[0].position);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Passa a SG_Rock_Blend tutto ciò che serve per fondere una roccia con quello che tocca:
    /// - il piano del suolo sotto la roccia (raycast verso il basso), per la fusione nel fango;
    /// - i proxy SDF (ellissoide o box arrotondato) delle rocce vicine, per la fusione roccia-roccia.
    /// Ogni roccia con questo componente pubblica il proprio proxy e legge quello dei vicini più vicini:
    /// nessuna modifica alle mesh, le rocce si possono spostare e la fusione si aggiorna da sola.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class GroundBlendProbe : MonoBehaviour
    {
        public enum ProxyShape
        {
            Ellipsoid = 1,
            Box = 2,
        }

        public const int MaxNeighbors = 4;

        private static readonly int GroundPlanePoint = Shader.PropertyToID("_GroundPlanePoint");
        private static readonly int GroundPlaneNormal = Shader.PropertyToID("_GroundPlaneNormal");
        private static readonly int ProxyMatrices = Shader.PropertyToID("_RockProxyM");
        private static readonly int ProxyShapes = Shader.PropertyToID("_RockProxyR");
        private static readonly int NeighborParams = Shader.PropertyToID("_RockNeighborParams");

        private static readonly List<GroundBlendProbe> Active = new();
        private static int neighborVersion;

        [Header("Suolo")]
        [Tooltip("Layer considerati fondale. Conviene limitarli al layer del terreno.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("Quanto sopra la cima della roccia parte il raycast (m).")]
        [SerializeField] private float probeStartAbove = 2f;

        [Tooltip("Lunghezza massima del raycast sotto la cima della roccia (m).")]
        [SerializeField] private float probeDistance = 50f;

        [Header("Proxy di questa roccia (visto dalle vicine)")]
        [Tooltip("Forma che approssima la roccia. Ellissoide per massi, Box per pareti e scarpate.")]
        [SerializeField] private ProxyShape proxyShape = ProxyShape.Ellipsoid;

        [Tooltip("Scala del proxy rispetto ai bounds della mesh, per asse locale.")]
        [SerializeField] private Vector3 proxyScale = Vector3.one;

        [Tooltip("Spostamento del centro del proxy, in coordinate locali della mesh.")]
        [SerializeField] private Vector3 proxyOffset = Vector3.zero;

        [Tooltip("Raggio di arrotondamento degli spigoli del Box (m, massimo 0.99).")]
        [SerializeField, Range(0f, 0.99f)] private float boxRounding = 0.3f;

        [Header("Fusione con le rocce vicine")]
        [Tooltip("Se falso la roccia fa solo da ricevitore: le vicine si fondono su di lei, lei no.")]
        [SerializeField] private bool blendWithNeighbors = true;

        [Tooltip("Larghezza della sfumatura dalla superficie del vicino (m).")]
        [SerializeField, Min(0.01f)] private float neighborBlendWidth = 0.5f;

        [Tooltip("Quanto la normale si piega verso quella condivisa all'intersezione.")]
        [SerializeField, Range(0f, 1f)] private float neighborNormalBlend = 0.8f;

        [Tooltip("Quanto fango si deposita nella giuntura tra le rocce.")]
        [SerializeField, Range(0f, 1f)] private float neighborSediment = 0.6f;

        [SerializeField, HideInInspector] private bool hasGround;
        [SerializeField, HideInInspector] private Vector3 groundPoint;
        [SerializeField, HideInInspector] private Vector3 groundNormal = Vector3.up;

        private readonly Matrix4x4[] neighborMatrices = new Matrix4x4[MaxNeighbors];
        private readonly Vector4[] neighborShapes = new Vector4[MaxNeighbors];
        private readonly List<GroundBlendProbe> neighbors = new();
        private int seenVersion = -1;

        private Renderer rockRenderer;
        private MaterialPropertyBlock block;

        private void OnEnable()
        {
            rockRenderer = GetComponent<Renderer>();
            block ??= new MaterialPropertyBlock();
            Active.Add(this);
            neighborVersion++;
            Probe();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            neighborVersion++;
            if (rockRenderer != null)
            {
                rockRenderer.SetPropertyBlock(null);
            }
        }

        // Chi si sposta rifà il raycast e alza la versione; tutte le altre rocce rileggono i vicini.
        private void Update()
        {
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                neighborVersion++;
                Probe();
            }
            else if (seenVersion != neighborVersion)
            {
                Apply();
            }
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                neighborVersion++;
                Probe();
            }
        }

        /// <summary>Ricalcola il piano del suolo sotto la roccia e aggiorna il renderer.</summary>
        public void Probe()
        {
            if (rockRenderer == null)
            {
                rockRenderer = GetComponent<Renderer>();
            }

            if (!Application.isPlaying)
            {
                Physics.SyncTransforms();
            }

            Bounds bounds = rockRenderer.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + probeStartAbove, bounds.center.z);
            float distance = probeStartAbove + bounds.size.y + probeDistance;

            hasGround = false;
            float closest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= closest)
                {
                    continue;
                }

                closest = hit.distance;
                hasGround = true;
                groundPoint = hit.point;
                groundNormal = hit.normal;
            }

            Apply();
        }

        /// <summary>Raccoglie i vicini e scrive suolo e proxy nel property block.</summary>
        private void Apply()
        {
            seenVersion = neighborVersion;
            block ??= new MaterialPropertyBlock();

            int count = blendWithNeighbors ? GatherNeighbors() : 0;
            for (int i = 0; i < MaxNeighbors; i++)
            {
                if (i < count)
                {
                    neighbors[i].GetProxy(out neighborMatrices[i], out neighborShapes[i]);
                }
                else
                {
                    neighborMatrices[i] = Matrix4x4.identity;
                    neighborShapes[i] = Vector4.zero;
                }
            }

            rockRenderer.GetPropertyBlock(block);
            block.SetVector(GroundPlanePoint, groundPoint);
            block.SetVector(GroundPlaneNormal, new Vector4(groundNormal.x, groundNormal.y, groundNormal.z, hasGround ? 1f : 0f));
            block.SetMatrixArray(ProxyMatrices, neighborMatrices);
            block.SetVectorArray(ProxyShapes, neighborShapes);
            block.SetVector(NeighborParams, new Vector4(neighborBlendWidth, neighborNormalBlend, neighborSediment, count));
            rockRenderer.SetPropertyBlock(block);
        }

        // I vicini sono le rocce i cui bounds toccano i nostri allargati della larghezza di sfumatura;
        // se sono più di MaxNeighbors tiene quelle con la sovrapposizione maggiore.
        private int GatherNeighbors()
        {
            neighbors.Clear();
            Bounds own = rockRenderer.bounds;
            own.Expand(neighborBlendWidth * 2f);

            foreach (GroundBlendProbe other in Active)
            {
                if (other != this && other.rockRenderer != null && own.Intersects(other.rockRenderer.bounds))
                {
                    neighbors.Add(other);
                }
            }

            if (neighbors.Count > MaxNeighbors)
            {
                neighbors.Sort((a, b) => OverlapVolume(own, b.rockRenderer.bounds).CompareTo(OverlapVolume(own, a.rockRenderer.bounds)));
            }

            return Mathf.Min(neighbors.Count, MaxNeighbors);
        }

        private static float OverlapVolume(Bounds a, Bounds b)
        {
            Vector3 size = Vector3.Max(Vector3.zero, Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min));
            return size.x * size.y * size.z;
        }

        /// <summary>
        /// Proxy in world space: matrice mondo → spazio del proxy (solo rotazione e traslazione)
        /// e semiassi in metri. w = forma + arrotondamento: 1 ellissoide, 2 + r box con spigoli di raggio r.
        /// </summary>
        private void GetProxy(out Matrix4x4 worldToProxy, out Vector4 shape)
        {
            Bounds local = GetLocalBounds();
            Vector3 center = transform.TransformPoint(local.center + proxyOffset);
            Vector3 radii = Vector3.Scale(Vector3.Scale(local.extents, transform.lossyScale), proxyScale);
            radii = new Vector3(Mathf.Abs(radii.x), Mathf.Abs(radii.y), Mathf.Abs(radii.z));

            worldToProxy = Matrix4x4.TRS(center, transform.rotation, Vector3.one).inverse;
            float minRadius = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
            float rounding = proxyShape == ProxyShape.Box ? Mathf.Min(boxRounding, minRadius) : 0f;
            shape = new Vector4(radii.x, radii.y, radii.z, (int)proxyShape + rounding);
        }

        private Bounds GetLocalBounds()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                return filter.sharedMesh.bounds;
            }

            return rockRenderer != null ? rockRenderer.localBounds : new Bounds(Vector3.zero, Vector3.one);
        }

        private void OnDrawGizmosSelected()
        {
            if (hasGround)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
                Gizmos.DrawLine(groundPoint, groundPoint + groundNormal);
                Gizmos.DrawWireSphere(groundPoint, 0.1f);
            }

            if (rockRenderer == null)
            {
                return;
            }

            GetProxy(out Matrix4x4 worldToProxy, out Vector4 shape);
            Vector3 radii = shape;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.matrix = worldToProxy.inverse;
            if (proxyShape == ProxyShape.Box)
            {
                Gizmos.DrawWireCube(Vector3.zero, radii * 2f);
            }
            else
            {
                Gizmos.matrix *= Matrix4x4.Scale(radii);
                Gizmos.DrawWireSphere(Vector3.zero, 1f);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

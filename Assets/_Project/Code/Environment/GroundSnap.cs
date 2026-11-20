using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Solo in Editor: quando l'oggetto viene spostato, lo appoggia sul fondale.
    /// Campiona i quattro angoli dell'impronta, inclina l'oggetto sul piano del terreno
    /// (quanto deciso da slopeAlign, la rotazione attorno all'alto resta quella data a mano)
    /// e lo abbassa finché nessun angolo resta sospeso. Il pivot deve stare alla base.
    /// </summary>
    [ExecuteAlways]
    public sealed class GroundSnap : MonoBehaviour
    {
        private const float RayStartAbove = 10f;
        private const float RayLength = 200f;

        [Tooltip("Layer del terreno su cui appoggiarsi.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("Larghezza (X) e profondità (Z) dell'impronta alla base, in metri locali.")]
        [SerializeField] private Vector2 footprintSize = new(2f, 2f);

        [Tooltip("Centro dell'impronta rispetto al pivot (X, Z locali), in metri.")]
        [SerializeField] private Vector2 footprintCenter = Vector2.zero;

        [Tooltip("0 = sempre dritto, 1 = inclinato quanto il terreno.")]
        [SerializeField, Range(0f, 1f)] private float slopeAlign = 0.5f;

        [Tooltip("Quanto affondare la base sotto l'angolo più basso, in metri.")]
        [SerializeField] private float sink = 0.05f;

#if UNITY_EDITOR
        private void Reset()
        {
            int seafloor = LayerMask.GetMask("Seafloor");
            if (seafloor != 0) groundMask = seafloor;
        }

        // Al caricamento hasChanged è vero: senza questo, aggiungere il componente sposterebbe le istanze già posate.
        private void OnEnable() => transform.hasChanged = false;

        private void Update()
        {
            if (Application.isPlaying || !transform.hasChanged) return;
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
            Snap();
            transform.hasChanged = false;
        }

        [ContextMenu("Appoggia al terreno")]
        private void Snap()
        {
            Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 pivot = transform.position;
            Vector3 half = new(footprintSize.x * 0.5f, 0f, footprintSize.y * 0.5f);
            Vector3 center = new(footprintCenter.x, 0f, footprintCenter.y);
            Vector3[] corners =
            {
                center + new Vector3(-half.x, 0f, -half.z),
                center + new Vector3(half.x, 0f, -half.z),
                center + new Vector3(half.x, 0f, half.z),
                center + new Vector3(-half.x, 0f, half.z),
            };

            var hits = new Vector3[4];
            for (int i = 0; i < 4; i++)
                if (!Probe(pivot + yaw * corners[i], out hits[i])) return;

            Vector3 normal = Vector3.Cross(hits[2] - hits[0], hits[3] - hits[1]).normalized;
            if (normal.y < 0f) normal = -normal;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, slopeAlign)) * yaw;

            // Il pivot più basso tra quelli che appoggerebbero ciascun angolo: nessun angolo resta sospeso.
            float y = float.MaxValue;
            for (int i = 0; i < 4; i++)
                y = Mathf.Min(y, hits[i].y - (rotation * corners[i]).y);

            transform.SetPositionAndRotation(new Vector3(pivot.x, y - sink, pivot.z), rotation);
        }

        private bool Probe(Vector3 point, out Vector3 ground)
        {
            ground = default;
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * RayStartAbove, Vector3.down, RayLength, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance;
                ground = hit.point;
            }
            return best < float.MaxValue;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(footprintCenter.x, 0f, footprintCenter.y), new Vector3(footprintSize.x, 0f, footprintSize.y));
        }
#endif
    }
}

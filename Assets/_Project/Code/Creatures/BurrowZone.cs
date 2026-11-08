using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Zona di fango in cui le creature all'agguato (<see cref="CreatureAmbush"/>) possono insabbiarsi e spostarsi
    /// sotto il fondale. Rettangolo orientato col transform: conta solo la pianta (assi X e Z locali), non l'altezza.
    /// Le zone attive stanno in un elenco statico; senza zone in scena l'agguato usa il guinzaglio del nido.
    /// </summary>
    public sealed class BurrowZone : MonoBehaviour
    {
        [Tooltip("Lati del rettangolo sugli assi X e Z locali (m, moltiplicati dalla scala del transform).")]
        [SerializeField] private Vector2 size = new Vector2(10f, 10f);

        private static readonly List<BurrowZone> active = new List<BurrowZone>();

        /// <summary>Zone abilitate in scena.</summary>
        public static IReadOnlyList<BurrowZone> Active => active;

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        /// <summary>True se la pianta del punto cade dentro il rettangolo.</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
        }

        /// <summary>True se il punto cade in almeno una zona attiva.</summary>
        public static bool AnyContains(Vector3 point)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].Contains(point)) return true;
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.4f, 0.25f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0.05f, size.y));
        }
    }
}

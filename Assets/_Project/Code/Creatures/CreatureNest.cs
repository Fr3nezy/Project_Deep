using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Punto della mappa da cui escono le creature di una specie: all'avvio ne fa uscire <c>count</c> dal pool del
    /// <see cref="CreatureManager"/> attorno a sé, e le rimette nel pool quando si disabilita. Le creature vagano
    /// entro il guinzaglio attorno al nido. Versione base: spawn fuori vista, uscita a nuoto e despawn per
    /// distanza arrivano con la task dei nidi (M2).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureNest : MonoBehaviour
    {
        private const int MaxSpawnAttempts = 8;
        private const float FreeCheckHeight = 100f;

        [Tooltip("Manager della scena che possiede i pool.")]
        [SerializeField] private CreatureManager manager;

        [Tooltip("Specie che esce da questo nido.")]
        [SerializeField] private CreatureProfile profile;

        [Tooltip("Quante creature fa uscire all'avvio.")]
        [SerializeField, Min(0)] private int count = 5;

        [Tooltip("Raggio della sfera attorno al nido in cui compaiono (m)")]
        [SerializeField, Min(0f)] private float spawnRadius = 3f;

        [Tooltip("Oltre questa distanza dal nido le creature piegano per tornare indietro (m)")]
        [SerializeField, Min(1f)] private float leashRadius = 20f;

        private readonly List<Creature> spawned = new List<Creature>();
        private bool started;

        /// <summary>Creature di questo nido attualmente fuori dal pool.</summary>
        public int SpawnedCount => spawned.Count;

        private void Start()
        {
            started = true;
            SpawnAll();
        }

        private void OnEnable()
        {
            // Al primo avvio ci pensa Start, quando anche il manager ha fatto Awake.
            if (started) SpawnAll();
        }

        private void OnDisable() => DespawnAll();

        /// <summary>Cambia il numero di creature e rifà uscire il nido da capo.</summary>
        public void Respawn(int newCount)
        {
            count = Mathf.Max(0, newCount);
            DespawnAll();
            SpawnAll();
        }

        private void SpawnAll()
        {
            if (manager == null || profile == null)
            {
                Debug.LogError($"[CreatureNest] {name}: manager o profilo non assegnati.", this);
                return;
            }

            manager.Prewarm(profile, count);
            Vector3 center = transform.position;
            for (int i = spawned.Count; i < count; i++)
            {
                Vector3 position = FindFreePoint(center);
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Creature creature = manager.Spawn(profile, position, rotation, center, leashRadius);
                if (creature != null) spawned.Add(creature);
            }
        }

        /// <summary>
        /// Punto a caso nella sfera di spawn che non sia sepolto: guardando dall'alto, la prima superficie deve
        /// stare sotto il punto, con margine per il corpo. Dopo qualche tentativo ripiega sul centro del nido.
        /// </summary>
        private Vector3 FindFreePoint(Vector3 center)
        {
            float clearance = profile.bodyRadius;
            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                Vector3 candidate = center + Random.insideUnitSphere * spawnRadius;
                Vector3 above = candidate + Vector3.up * FreeCheckHeight;
                if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, FreeCheckHeight, manager.ObstacleMask,
                        QueryTriggerInteraction.Ignore) && hit.point.y > candidate.y - clearance)
                    continue;
                if (Physics.CheckSphere(candidate, clearance, manager.ObstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
                return candidate;
            }
            return center;
        }

        private void DespawnAll()
        {
            if (manager != null)
                foreach (Creature creature in spawned) manager.Despawn(creature);
            spawned.Clear();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, leashRadius);
        }
    }
}

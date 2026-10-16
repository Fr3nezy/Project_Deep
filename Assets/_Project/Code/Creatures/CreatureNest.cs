using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Punto della mappa da cui escono le creature di una specie, fino a <c>count</c>. All'avvio riempie il nido
    /// attorno a sé. Poi rimpiazza le creature che mancano (mangiate o tornate nel pool) una alla volta: ne fa
    /// uscire una dalla bocca del nido, che nuota lungo il suo asse Z, solo quando il nido non si vede (fuori
    /// inquadratura, coperto o oltre la nebbia). Se resta in vista troppo a lungo, la fa uscire comunque con una
    /// dissolvenza (paracadute). Con il sub lontano lavora al contrario: le creature che non si vedono tornano nel
    /// pool. Le creature vagano entro il guinzaglio attorno al nido.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureNest : MonoBehaviour
    {
        private const int MaxSpawnAttempts = 8;
        private const float FreeCheckHeight = 100f;
        // Intervallo fra due controlli di visibilità quando il nido aspetta di poter far uscire una creatura (s).
        private const float CheckInterval = 0.25f;

        [Tooltip("Manager della scena che possiede i pool.")]
        [SerializeField] private CreatureManager manager;

        [Tooltip("Specie che esce da questo nido.")]
        [SerializeField] private CreatureProfile profile;

        [Tooltip("Massimo di creature del nido fuori dal pool.")]
        [SerializeField, Min(0)] private int count = 5;

        [Tooltip("All'avvio il nido si riempie subito attorno a sé; spento, parte vuoto e le creature escono una alla volta.")]
        [SerializeField] private bool fillOnStart = true;

        [Tooltip("Raggio della sfera attorno al nido in cui compaiono quando il nido si riempie all'avvio (m)")]
        [SerializeField, Min(0f)] private float spawnRadius = 3f;

        [Tooltip("Oltre questa distanza dal nido le creature piegano per tornare indietro (m)")]
        [SerializeField, Min(1f)] private float leashRadius = 20f;

        [Tooltip("Quota massima delle creature sopra il nido (m): dove il fondale sale oltre, lo aggirano invece di " +
                 "seguirlo. Tiene i pesci all'altezza del sub sui pendii.")]
        [SerializeField, Min(0f)] private float maxHeightAboveNest = 4f;

        [Header("Ricambio")]
        [Tooltip("Secondi medi fra due uscite dal nido quando mancano creature (±50%).")]
        [SerializeField, Min(0.1f)] private float respawnInterval = 3f;

        [Tooltip("Semi-angolo del cono attorno all'asse Z del nido in cui escono le creature (°).")]
        [SerializeField, Range(0f, 90f)] private float exitSpread = 30f;

        [Tooltip("Il nido rimpiazza le creature solo con il sub entro questa distanza (m); oltre il 120%, quelle che " +
                 "non si vedono tornano nel pool.")]
        [SerializeField, Min(1f)] private float activeDistance = 60f;

        [Tooltip("Se il nido resta in vista per questi secondi con posti liberi, fa uscire comunque una creatura in " +
                 "dissolvenza. Solo per le specie con lo shader Fish; 0 = mai.")]
        [SerializeField, Min(0f)] private float fadeInAfter = 6f;

        [Tooltip("Durata della dissolvenza d'entrata (s).")]
        [SerializeField, Min(0.05f)] private float fadeDuration = 1.5f;

        // Creature del nido con il loro Slot al momento dello spawn: una creatura tornata nel pool (anche mangiata)
        // e magari ripresa da un altro nido ha ActiveIndex -1 o uno Slot diverso, e il nido la toglie dalla lista.
        private readonly List<Creature> spawned = new List<Creature>();
        private readonly List<int> spawnedSlots = new List<int>();
        private bool started;
        private float nextCheckTime;
        // Da quando il nido aspetta di far uscire una creatura: posto libero o ultima uscita. -1 se pieno.
        private float waitingSince = -1f;

        /// <summary>Creature di questo nido attualmente fuori dal pool.</summary>
        public int SpawnedCount
        {
            get
            {
                Prune();
                return spawned.Count;
            }
        }

        private void Start()
        {
            started = true;
            if (fillOnStart) SpawnAll();
        }

        private void OnEnable()
        {
            // Al primo avvio ci pensa Start, quando anche il manager ha fatto Awake.
            if (started && fillOnStart) SpawnAll();
        }

        private void OnDisable() => DespawnAll();

        /// <summary>Cambia il numero di creature e rifà uscire il nido da capo, riempito subito.</summary>
        public void Respawn(int newCount)
        {
            count = Mathf.Max(0, newCount);
            DespawnAll();
            SpawnAll();
        }

        private void Update()
        {
            float now = Time.time;
            if (!started || manager == null || profile == null || now < nextCheckTime) return;
            nextCheckTime = now + CheckInterval;
            Prune();

            // Fra activeDistance e il 120% il nido non fa niente, così non svuota e riempie di continuo sul bordo.
            float sqrPlayer = manager.HasPlayer ? (manager.PlayerPosition - transform.position).sqrMagnitude : 0f;
            if (sqrPlayer > activeDistance * activeDistance)
            {
                waitingSince = -1f;
                if (sqrPlayer > activeDistance * activeDistance * 1.44f) DespawnUnseen();
                return;
            }

            if (spawned.Count >= count)
            {
                waitingSince = -1f;
                return;
            }
            if (waitingSince < 0f) waitingSince = now;

            if (manager.IsHidden(transform.position, profile.bodyRadius))
            {
                // Oltre la nebbia nessuno vede la zona: il nido si riempie subito, così chi torna da lontano non
                // trova il nido vuoto. Più vicino le creature escono una alla volta dalla bocca.
                if (manager.IsBeyondFog(transform.position, spawnRadius + leashRadius)) SpawnAll();
                else SpawnFromNest(false);
                return;
            }

            // In vista: aspetta fadeInAfter dall'ultima uscita (o da quando manca una creatura), anche se nel frattempo
            // il nido è stato nascosto per un attimo, poi la fa uscire in dissolvenza.
            if (fadeInAfter > 0f && now - waitingSince >= fadeInAfter && profile.prefab != null &&
                profile.prefab.CanFadeIn)
                SpawnFromNest(true);
        }

        /// <summary>Una creatura esce dalla bocca del nido nuotando dentro il cono di uscita.</summary>
        private void SpawnFromNest(bool fade)
        {
            Vector3 center = transform.position;
            Vector3 position = center;
            if (Physics.CheckSphere(position, profile.bodyRadius, manager.ObstacleMask, QueryTriggerInteraction.Ignore))
                position = FindFreePoint(center);

            Quaternion rotation = transform.rotation *
                                  Quaternion.Euler(Random.Range(-exitSpread, exitSpread), Random.Range(-exitSpread, exitSpread), 0f);
            Creature creature = manager.Spawn(profile, position, rotation, center, leashRadius,
                center.y + maxHeightAboveNest);
            if (creature == null) return;
            if (fade) creature.FadeIn(fadeDuration);
            Track(creature);
            waitingSince = Time.time;
            nextCheckTime = Time.time + respawnInterval * Random.Range(0.5f, 1.5f);
        }

        private void SpawnAll()
        {
            if (manager == null || profile == null)
            {
                Debug.LogError($"[CreatureNest] {name}: manager o profilo non assegnati.", this);
                return;
            }

            Prune();
            manager.Prewarm(profile, count);
            Vector3 center = transform.position;
            float ceiling = center.y + maxHeightAboveNest;
            for (int i = spawned.Count; i < count; i++)
            {
                Vector3 position = FindFreePoint(center);
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Creature creature = manager.Spawn(profile, position, rotation, center, leashRadius, ceiling);
                if (creature != null) Track(creature);
            }
        }

        private void Track(Creature creature)
        {
            spawned.Add(creature);
            spawnedSlots.Add(creature.Slot);
        }

        /// <summary>Toglie dalla lista le creature tornate nel pool (mangiate o rimesse da altri).</summary>
        private void Prune()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                Creature creature = spawned[i];
                if (creature != null && creature.ActiveIndex >= 0 && creature.Slot == spawnedSlots[i]) continue;
                int last = spawned.Count - 1;
                spawned[i] = spawned[last];
                spawnedSlots[i] = spawnedSlots[last];
                spawned.RemoveAt(last);
                spawnedSlots.RemoveAt(last);
            }
        }

        /// <summary>Con il sub lontano: rimette nel pool le creature del nido che la camera non vede.</summary>
        private void DespawnUnseen()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                Creature creature = spawned[i];
                if (!manager.IsHidden(creature.transform.position, profile.bodyRadius)) continue;
                manager.Despawn(creature);
            }
            Prune();
        }

        /// <summary>
        /// Punto a caso nella sfera di spawn che non sia sepolto: guardando dall'alto, la prima superficie deve
        /// stare sotto il punto, con margine per il corpo. Dopo qualche tentativo ripiega sul centro del nido.
        /// </summary>
        private Vector3 FindFreePoint(Vector3 center)
        {
            float clearance = profile.bodyRadius;
            float ceiling = center.y + maxHeightAboveNest - clearance;
            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                Vector3 candidate = center + Random.insideUnitSphere * spawnRadius;
                candidate.y = Mathf.Min(candidate.y, ceiling);
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
            Prune();
            if (manager != null)
                foreach (Creature creature in spawned) manager.Despawn(creature);
            spawned.Clear();
            spawnedSlots.Clear();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            // Bocca del nido: direzione in cui escono le creature.
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, leashRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * maxHeightAboveNest,
                new Vector3(leashRadius * 2f, 0f, leashRadius * 2f));
        }
    }
}

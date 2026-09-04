using UnityEngine;
using System.Collections.Generic;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Gestisce il pooling delle entità per ottimizzare le performance.
    /// Ricicla le istanze invece di distruggerle e ricrearle continuamente.
    /// </summary>
    public class EntityPooler : MonoBehaviour
    {
        [System.Serializable]
        public class PoolConfig
        {
            public string poolName;
            public GameObject prefab;
            public int initialSize = 10;
            public int maxSize = 50;
            public bool allowExpansion = true;
        }

        [Header("Configurazione Pool")]
        [SerializeField, Tooltip("Configurazioni dei pool per diversi tipi di entità")]
        private List<PoolConfig> poolConfigs = new List<PoolConfig>();

        [SerializeField, Tooltip("Parent container per le entità poolate")]
        private Transform poolContainer;

        [Header("Debug")]
        [SerializeField, Tooltip("Mostra statistiche dei pool")]
        private bool showPoolStats = true;

        // Dizionario dei pool (key = nome pool)
        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, PoolConfig> poolConfigMap = new Dictionary<string, PoolConfig>();
        private Dictionary<string, int> activeCount = new Dictionary<string, int>();
        private Dictionary<string, int> totalCreated = new Dictionary<string, int>();

        // Singleton
        private static EntityPooler instance;
        public static EntityPooler Instance => instance;

        // Getter per accedere ai pool configs (usato da EntitySpawner)
        public System.Collections.Generic.List<PoolConfig> PoolConfigs => poolConfigs;

        private void Awake()
        {
            // Singleton pattern
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            // Crea container se non esiste
            if (poolContainer == null)
            {
                GameObject container = new GameObject("EntityPool_Container");
                poolContainer = container.transform;
                poolContainer.SetParent(transform);
            }

            // Inizializza i pool
            InitializePools();
        }

        /// <summary>
        /// Inizializza tutti i pool configurati.
        /// </summary>
        private void InitializePools()
        {
            Debug.Log($"[EntityPooler] Starting InitializePools with {poolConfigs.Count} configs");

            foreach (PoolConfig config in poolConfigs)
            {
                Debug.Log($"[EntityPooler] Processing config '{config.poolName}' with initialSize {config.initialSize}, prefab: {config.prefab}");

                if (config.prefab == null)
                {
                    Debug.LogWarning($"[EntityPooler] Prefab nullo per pool '{config.poolName}'. Saltato.");
                    continue;
                }

                // Crea il pool
                Queue<GameObject> pool = new Queue<GameObject>();

                // Pre-istanzia le entità
                totalCreated[config.poolName] = config.initialSize;
                Debug.Log($"[EntityPooler] Set totalCreated['{config.poolName}'] to {config.initialSize}");

                for (int i = 0; i < config.initialSize; i++)
                {
                    GameObject obj = CreateNewEntity(config.prefab, config.poolName);
                    pool.Enqueue(obj);
                }

                // Registra il pool
                pools[config.poolName] = pool;
                poolConfigMap[config.poolName] = config;
                activeCount[config.poolName] = 0;

                Debug.Log($"[EntityPooler] Pool '{config.poolName}' inizializzato con {config.initialSize} entità");
            }
        }

        /// <summary>
        /// Crea una nuova istanza di entità.
        /// </summary>
        private GameObject CreateNewEntity(GameObject prefab, string poolName)
        {
            Debug.Log($"[EntityPooler] Creating entity for '{poolName}'");
            GameObject obj = Instantiate(prefab, poolContainer);
            Debug.Log($"[EntityPooler] Instantiated obj: {obj}, totalCreated contains '{poolName}': {totalCreated.ContainsKey(poolName)}");

            if (totalCreated.ContainsKey(poolName))
            {
                obj.name = $"{poolName}_{totalCreated[poolName]}";
                Debug.Log($"[EntityPooler] Set name to '{obj.name}'");
            }
            else
            {
                Debug.LogError($"[EntityPooler] totalCreated missing key '{poolName}'");
                obj.name = $"{poolName}_error";
            }

            obj.SetActive(false);

            // Aggiungi component per il ritorno al pool
            PooledEntity pooledEntity = obj.GetComponent<PooledEntity>();
            if (pooledEntity == null)
            {
                pooledEntity = obj.AddComponent<PooledEntity>();
            }
            pooledEntity.SetPoolName(poolName);

            return obj;
        }

        /// <summary>
        /// Ottiene un'entità dal pool specificato.
        /// </summary>
        /// <param name="poolName">Nome del pool</param>
        /// <param name="position">Posizione dove spawnare l'entità</param>
        /// <param name="rotation">Rotazione dell'entità</param>
        /// <returns>GameObject dell'entità spawned, o null se non disponibile</returns>
        public GameObject Spawn(string poolName, Vector3 position, Quaternion rotation)
        {
            if (!pools.ContainsKey(poolName))
            {
                Debug.LogError($"[EntityPooler] Pool '{poolName}' non trovato!");
                return null;
            }

            Queue<GameObject> pool = pools[poolName];
            GameObject obj = null;

            // Cerca un'entità disponibile
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else
            {
                // Pool vuoto
                PoolConfig config = poolConfigMap[poolName];
                
                if (config.allowExpansion && totalCreated[poolName] < config.maxSize)
                {
                    // Crea una nuova istanza
                    obj = CreateNewEntity(config.prefab, poolName);
                    totalCreated[poolName]++;
                    Debug.LogWarning($"[EntityPooler] Pool '{poolName}' espanso. Totale: {totalCreated[poolName]}");
                }
                else
                {
                    Debug.LogWarning($"[EntityPooler] Pool '{poolName}' esaurito e non può espandersi!");
                    return null;
                }
            }

            // Attiva e posiziona l'entità
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            // Reset dello stato dell'entità
            EntityStatus status = obj.GetComponent<EntityStatus>();
            if (status != null)
            {
                status.ResetStatus();
            }

            // Reset stato a Idle per spawn freschi (entità potrebbero essere in Dead dal pool)
            EntityStateManager stateManager = obj.GetComponent<EntityStateManager>();
            if (stateManager != null)
            {
                stateManager.ForceState(EntityState.Idle);
            }

            activeCount[poolName]++;

            return obj;
        }

        /// <summary>
        /// Restituisce un'entità al pool.
        /// </summary>
        /// <param name="obj">GameObject da restituire</param>
        /// <param name="poolName">Nome del pool</param>
        public void Despawn(GameObject obj, string poolName)
        {
            if (obj == null) return;

            if (!pools.ContainsKey(poolName))
            {
                Debug.LogError($"[EntityPooler] Pool '{poolName}' non trovato per despawn!");
                Destroy(obj);
                return;
            }

            // Disattiva l'oggetto
            obj.SetActive(false);
            obj.transform.SetParent(poolContainer);

            // Restituisci al pool
            pools[poolName].Enqueue(obj);
            activeCount[poolName]--;
        }

        /// <summary>
        /// Despawna tutte le entità attive di un pool specifico.
        /// </summary>
        public void DespawnAll(string poolName)
        {
            if (!pools.ContainsKey(poolName))
            {
                Debug.LogError($"[EntityPooler] Pool '{poolName}' non trovato!");
                return;
            }

            // Trova tutte le entità attive con questo pool name
            PooledEntity[] entities = FindObjectsOfType<PooledEntity>();
            foreach (PooledEntity entity in entities)
            {
                if (entity.PoolName == poolName && entity.gameObject.activeInHierarchy)
                {
                    Despawn(entity.gameObject, poolName);
                }
            }
        }

        /// <summary>
        /// Despawna tutte le entità attive di tutti i pool.
        /// </summary>
        public void DespawnAllEntities()
        {
            foreach (string poolName in pools.Keys)
            {
                DespawnAll(poolName);
            }
        }

        /// <summary>
        /// Ottiene statistiche di un pool specifico.
        /// </summary>
        public string GetPoolStats(string poolName)
        {
            if (!pools.ContainsKey(poolName))
            {
                return $"Pool '{poolName}' non trovato";
            }

            int available = pools[poolName].Count;
            int active = activeCount[poolName];
            int total = totalCreated[poolName];

            return $"{poolName}: Active={active}, Available={available}, Total={total}";
        }

        /// <summary>
        /// Ottiene statistiche di tutti i pool.
        /// </summary>
        public string GetAllPoolStats()
        {
            string stats = "=== Entity Pool Stats ===\n";
            foreach (string poolName in pools.Keys)
            {
                stats += GetPoolStats(poolName) + "\n";
            }
            return stats;
        }

        #region Debug & Gizmos

        private void OnGUI()
        {
            if (!showPoolStats) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Box("Entity Pool Statistics");
            
            foreach (string poolName in pools.Keys)
            {
                GUILayout.Label(GetPoolStats(poolName));
            }

            GUILayout.EndArea();
        }

        #endregion
    }

    /// <summary>
    /// Component aggiunto alle entità poolate per tracciare il loro pool di appartenenza.
    /// </summary>
    public class PooledEntity : MonoBehaviour
    {
        private string poolName;
        public string PoolName => poolName;

        private EntityStatus entityStatus;

        private void Awake()
        {
            entityStatus = GetComponent<EntityStatus>();
        }

        private void OnEnable()
        {
            // Sottoscrivi all'evento di morte per auto-despawn
            if (entityStatus != null)
            {
                entityStatus.OnEntityDied += HandleEntityDied;
            }
        }

        private void OnDisable()
        {
            // Rimuovi sottoscrizione
            if (entityStatus != null)
            {
                entityStatus.OnEntityDied -= HandleEntityDied;
            }
        }

        public void SetPoolName(string name)
        {
            poolName = name;
        }

        private void HandleEntityDied(string entityID, EntityType entityType)
        {
            // Auto-despawn dopo morte (con un piccolo delay per animazioni)
            Invoke(nameof(ReturnToPool), 2f);
        }

        private void ReturnToPool()
        {
            if (EntityPooler.Instance != null)
            {
                EntityPooler.Instance.Despawn(gameObject, poolName);
            }
            else
            {
                Debug.LogWarning($"[PooledEntity] EntityPooler non trovato per restituire {gameObject.name}");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Metodo pubblico per despawn manuale.
        /// </summary>
        public void Despawn()
        {
            ReturnToPool();
        }
    }
}

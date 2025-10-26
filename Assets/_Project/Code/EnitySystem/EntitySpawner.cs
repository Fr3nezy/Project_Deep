using UnityEngine;
using UnityEngine.InputSystem;
using Deeploration.EntitySystem;

/// <summary>
/// Script flessibile per spawnare entità dai pool configurati.
/// Premi 1 (primo pool), 2 (secondo pool), etc., C per despawnare tutto.
/// </summary>
public class EntitySpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private Vector3 spawnAreaCenter = Vector3.zero;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(20f, 10f, 20f);

    [Header("Auto Spawn")]
    [SerializeField] private bool autoSpawnOnStart = true;

    [Header("Entity Pooler Reference")]
    [SerializeField, Tooltip("Riferimento al Pooler (lascia vuoto per usare Instance)")]
    private EntityPooler entityPoolerReference;

    private EntityPooler Pooler => entityPoolerReference != null ? entityPoolerReference : EntityPooler.Instance;

    [Header("Debug")]
    [SerializeField] private bool showSpawnArea = true;
    
    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnInitialEntities();
        }
    }
    
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Spawn manuale per pool specifici (1 per primo pool, 2 per secondo, etc.)
        for (int i = 0; i < Pooler.PoolConfigs.Count && i < 9; i++)
        {
            Key key = Key.Digit1 + i;
            if (keyboard[key].wasPressedThisFrame)
            {
                string poolName = Pooler.PoolConfigs[i].poolName;
                SpawnFromPool(poolName);
                Debug.Log($"Spawned {poolName} - Press {(i + 1)} to spawn more");
            }
        }

        if (keyboard[Key.C].wasPressedThisFrame)
        {
            DespawnAll();
            Debug.Log("Cleared all entities");
        }
    }
    
    private void SpawnInitialEntities()
    {
        string spawnSummary = "=== Auto Spawn from Pooler Configurations ===";

        foreach (var config in Pooler.PoolConfigs)
        {
            spawnSummary += $"\n- {config.poolName}: {config.initialSize}";

            for (int i = 0; i < config.initialSize; i++)
            {
                SpawnFromPool(config.poolName);
            }
        }

        Debug.Log(spawnSummary);
        Debug.Log("✓ Spawn completato! Osserva i pesci cacciare.");
    }
    

    
    /// <summary>
    /// Spawna un'entità da un pool specifico alla posizione random nella zona.
    /// </summary>
    private void SpawnFromPool(string poolName)
    {
        Vector3 position = GetRandomPositionInArea();
        GameObject entity = Pooler.Spawn(poolName, position, Quaternion.identity);

        if (entity == null)
        {
            Debug.LogError($"Failed to spawn {poolName} - Pool exhausted?");
        }
    }

    private Vector3 GetRandomPositionInArea()
    {
        float x = Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2);
        float y = Random.Range(-spawnAreaSize.y / 2, spawnAreaSize.y / 2);
        float z = Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2);

        return spawnAreaCenter + new Vector3(x, y, z);
    }
    
    private void DespawnAll()
    {
        if (Pooler != null)
        {
            Pooler.DespawnAllEntities();
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showSpawnArea) return;
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
    }
    
    private void OnGUI()
    {
        // Dynamic help text basato sui pool configurati
        int numPools = Mathf.Min(Pooler.PoolConfigs.Count, 9);
        int boxHeight = 30 + numPools * 15 + 15; // header + linee + clear
        GUI.Box(new Rect(10, Screen.height - boxHeight - 10, 280, boxHeight + 10), "=== Entity Spawner ===");

        int yPos = Screen.height - boxHeight;
        GUI.Label(new Rect(20, yPos, 260, 20), "Auto-spawn all setup complete!");
        yPos += 20;

        for (int i = 0; i < numPools; i++)
        {
            string poolName = Pooler.PoolConfigs[i].poolName;
            GUI.Label(new Rect(20, yPos, 260, 20), $"{(i + 1)}: Spawn {poolName}");
            yPos += 15;
        }

        GUI.Label(new Rect(20, yPos, 260, 20), "C: Clear All Entities");
    }
}

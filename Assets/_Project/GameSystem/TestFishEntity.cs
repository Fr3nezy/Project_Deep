using UnityEngine;

/// <summary>
/// Test fish entity implementation.
/// </summary>
public class TestFishEntity : Entity
{
    [Header("Test Fish Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    protected override void Initialize()
    {
        base.Initialize();
        
        if (showDebugLogs)
            Debug.Log($"[Entity] {entityName} initialized as {entityType}");
    }
    
    protected override void OnDeath(Entity killer)
    {
        base.OnDeath(killer);
        
        if (showDebugLogs)
        {
            if (killer != null)
                Debug.Log($"💀 {entityName} killed by {killer.GetEntityName()}");
            else
                Debug.Log($"💀 {entityName} died");
        }
    }
}

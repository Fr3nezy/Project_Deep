using UnityEngine;

/// <summary>
/// Fake threat entity for testing fear/hunt systems.
/// </summary>
public class FakeThreatEntity : Entity
{
    [Header("Fake Threat Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    protected override void Initialize()
    {
        base.Initialize();
        
        if (showDebugLogs)
            Debug.Log($"[Entity] FakeThreat initialized: {entityName}");
    }
    
    protected override void OnDeath(Entity killer)
    {
        base.OnDeath(killer);
        
        if (showDebugLogs)
        {
            if (killer != null)
                Debug.Log($"💀 FakeThreat killed by {killer.GetEntityName()}");
            else
                Debug.Log($"💀 FakeThreat died");
        }
    }
}

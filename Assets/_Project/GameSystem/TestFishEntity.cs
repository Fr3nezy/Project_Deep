using UnityEngine;

/// <summary>
/// Entità di test per validare sistema.
/// </summary>
public class TestFishEntity : Entity
{
    protected override void InitializeEntity()
    {
        base.InitializeEntity();
        
        entityName = "Test Fish";
        entityType = EntityType.NormalFish;
        
        Debug.Log($"✓ {entityName} initialized!");
    }
    
    public override void OnEntityDeath(Entity killer)
    {
        base.OnEntityDeath(killer);
        Debug.Log($"💀 {entityName} death callback received!");
    }
}

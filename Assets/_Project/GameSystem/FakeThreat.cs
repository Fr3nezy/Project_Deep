using UnityEngine;

/// <summary>
/// Entity fittizia per testing Fear system.
/// </summary>
public class FakeThreatEntity : Entity
{
    protected override void InitializeEntity()
    {
        base.InitializeEntity();
        
        entityName = "Fake Threat";
        entityType = EntityType.Player; // Simula player
    }
}

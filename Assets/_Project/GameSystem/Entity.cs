using UnityEngine;

/// <summary>
/// Classe base per tutte le entità del gioco.
/// Versione semplice con componenti hardcoded.
/// </summary>
public abstract class Entity : MonoBehaviour
{
    [Header("Entity Info")]
    [SerializeField] protected string entityName = "Unnamed Entity";
    [SerializeField] protected EntityType entityType;
    
    [Header("Component References")]
    protected HealthComponent health;
    protected StaminaComponent stamina;
    protected HungerComponent hunger;
    protected FearComponent fear;
    protected AttackComponent attack;
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugInfo = false;
    
    protected virtual void Awake()
    {
        CacheComponents();
    }
    
    protected virtual void Start()
    {
        InitializeEntity();
        
        if (showDebugInfo)
            LogComponentStatus();
    }
    
    /// <summary>
    /// Cache automatico dei componenti.
    /// </summary>
    private void CacheComponents()
    {
        health = GetComponent<HealthComponent>();
        stamina = GetComponent<StaminaComponent>();
        hunger = GetComponent<HungerComponent>();
        fear = GetComponent<FearComponent>();
        attack = GetComponent<AttackComponent>();
    }
    
    /// <summary>
    /// Override in classi figlie per setup custom.
    /// </summary>
    protected virtual void InitializeEntity()
    {
        // Override in subclass
    }
    
    #region Component Getters
    
    public HealthComponent GetHealth() => health;
    public StaminaComponent GetStamina() => stamina;
    public HungerComponent GetHunger() => hunger;
    public FearComponent GetFear() => fear;
    public AttackComponent GetAttack() => attack;
    
    public bool HasHealth() => health != null;
    public bool HasStamina() => stamina != null;
    public bool HasHunger() => hunger != null;
    public bool HasFear() => fear != null;
    public bool HasAttack() => attack != null;
    
    #endregion
    
    #region Entity Info
    
    public string GetEntityName() => entityName;
    public EntityType GetEntityType() => entityType;
    
    public bool IsAlive() => health != null && !health.IsDead();
    public bool IsDead() => health != null && health.IsDead();
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Shortcut per ricevere danno.
    /// </summary>
    public virtual void TakeDamage(float damage, Entity attacker = null)
    {
        if (health != null)
        {
            health.TakeDamage(damage, attacker);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"[Entity] {entityName} non ha HealthComponent!");
        }
    }
    
    /// <summary>
    /// Shortcut per curarsi.
    /// </summary>
    public virtual void Heal(float amount)
    {
        if (health != null)
        {
            health.Heal(amount);
        }
    }
    
    /// <summary>
    /// Callback quando l'entità muore.
    /// </summary>
    public virtual void OnEntityDeath(Entity killer)
    {
        if (showDebugInfo)
            Debug.Log($"[Entity] {entityName} è morto. Killer: {(killer != null ? killer.GetEntityName() : "Unknown")}");
    }
    
    #endregion
    
    #region Debug
    
    private void LogComponentStatus()
    {
        Debug.Log($"[Entity] {entityName} ({entityType}) - Componenti:" +
                 $"\n  Health: {(health != null ? "✓" : "✗")}" +
                 $"\n  Stamina: {(stamina != null ? "✓" : "✗")}" +
                 $"\n  Hunger: {(hunger != null ? "✓" : "✗")}" +
                 $"\n  Fear: {(fear != null ? "✓" : "✗")}" +
                 $"\n  Attack: {(attack != null ? "✓" : "✗")}");
    }
    
protected virtual void OnDrawGizmos()
{
    if (!Application.isPlaying || !showDebugInfo) return;
    
    #if UNITY_EDITOR
    
    // === MAIN INFO LABEL (TOP) ===
    Vector3 mainLabelPos = transform.position + Vector3.up * 4.5f;
    
    string mainInfo = $"<b>{entityName}</b> ({entityType})";
    
    if (health != null)
        mainInfo += $"\nHP: {health.GetCurrentHealth():F0}/{health.GetMaxHealth():F0}";
    
    if (stamina != null)
    {
        mainInfo += $"\nStamina: {stamina.GetCurrentStamina():F0}";
        mainInfo += $" | State: {stamina.GetCurrentState()}";
        mainInfo += $"\nSpeed: x{stamina.GetSpeedMultiplier():F2}";
    }
    
    if (hunger != null)
    {
        mainInfo += $"\nHunger: {hunger.GetCurrentHunger():F0}";
        mainInfo += $" | {hunger.GetCurrentState()}";
    }
    
    UnityEditor.Handles.Label(mainLabelPos, mainInfo);
    
    #endif
}


    #endregion
}



/// <summary>
/// Enum per tipo di entità.
/// </summary>
public enum EntityType
{
    Player,
    NormalFish,
    Leviathan,
    Plankton,
    NPC,
    Other
}

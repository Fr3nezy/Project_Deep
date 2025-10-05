using UnityEngine;

/// <summary>
/// Tipi di entità nell'ecosistema.
/// </summary>
public enum EntityType
{
    Unknown,
    Plankton,
    Fish,
    SmallFish,
    MediumFish,
    LargeFish,
    Leviathan,
    Player,
    NPC,
    
    // Future expansion
    Scavenger,
    ElectricEel,
    Jellyfish,
    Crab
}

/// <summary>
/// Base class per tutte le entità (abstract).
/// </summary>
public abstract class Entity : MonoBehaviour
{
    [Header("Entity Identity")]
    [SerializeField] protected string entityName = "Entity";
    [SerializeField] protected EntityType entityType = EntityType.Unknown;
    [SerializeField] protected string entityID;
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugInfo = true;
    
    // Component cache
    protected HealthComponent health;
    protected StaminaComponent stamina;
    protected HungerComponent hunger;
    protected FearComponent fear;
    protected HuntComponent hunt;
    protected AttackComponent attack;
    protected MovementController movement;
    protected DietComponent diet;
    
    // State
    protected bool isDead = false;
    
    // Static event per tracking global entity events
    public static event System.Action<Entity, Entity> OnAnyEntityDied;
    
    protected virtual void Awake()
    {
        CacheComponents();
        GenerateUniqueID();
    }
    
    protected virtual void CacheComponents()
    {
        health = GetComponent<HealthComponent>();
        stamina = GetComponent<StaminaComponent>();
        hunger = GetComponent<HungerComponent>();
        fear = GetComponent<FearComponent>();
        hunt = GetComponent<HuntComponent>();
        attack = GetComponent<AttackComponent>();
        movement = GetComponent<MovementController>();
        diet = GetComponent<DietComponent>();
    }
    
    protected virtual void Start()
    {
        Initialize();
    }
    
    protected virtual void Initialize()
    {
        // Subscribe to death (FIX: usa metodo separato)
        if (health != null)
        {
            health.OnDeath.AddListener(HandleHealthDeath);
        }
    }
    
    /// <summary>
    /// Handler per death event da HealthComponent.
    /// </summary>
    private void HandleHealthDeath()
    {
        OnDeath(null);
    }
    
    private void GenerateUniqueID()
    {
        if (string.IsNullOrEmpty(entityID))
        {
            entityID = $"{entityType}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        }
    }
    
    protected virtual void OnDeath(Entity killer)
    {
        isDead = true;
        OnAnyEntityDied?.Invoke(this, killer);
    }
    
    public virtual void TakeDamage(float damage, Entity source)
    {
        if (health != null)
        {
            health.TakeDamage(damage);
            
            if (health.IsDead() && !isDead)
            {
                OnDeath(source);
            }
        }
    }
    
    // Getters
    public string GetEntityName() => entityName;
    public EntityType GetEntityType() => entityType;
    public string GetEntityID() => entityID;
    public bool IsDead() => isDead;
    public bool IsAlive() => !isDead;
    
    // Setters (per EntityConfig)
    public void SetEntityType(EntityType type) => entityType = type;
    public void SetEntityName(string name) => entityName = name;
    
    // Component getters
    public HealthComponent GetHealthComponent() => health;
    public StaminaComponent GetStaminaComponent() => stamina;
    public HungerComponent GetHungerComponent() => hunger;
    public FearComponent GetFearComponent() => fear;
    public HuntComponent GetHuntComponent() => hunt;
    public AttackComponent GetAttackComponent() => attack;
    public MovementController GetMovementController() => movement;
    public DietComponent GetDietComponent() => diet;
    
    /// <summary>
    /// Debug visualization con labels + bars.
    /// </summary>
    protected virtual void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showDebugInfo) return;
        
        #if UNITY_EDITOR
        
        // ========== MAIN STATS LABEL (4.5m height) ==========
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
        
        // ========== HUNT STATE LABEL (3.8m height) ==========
        if (hunt != null)
        {
            Vector3 huntLabelPos = transform.position + Vector3.up * 3.8f;
            
            GUIStyle huntStyle = new GUIStyle();
            huntStyle.alignment = TextAnchor.MiddleCenter;
            huntStyle.fontSize = 11;
            huntStyle.fontStyle = FontStyle.Bold;
            
            string huntText = hunt.GetHuntState() switch
            {
                HuntState.Idle => "🔍 IDLE",
                HuntState.Spotting => $"👁️ SPOTTING",
                HuntState.Stalking => $"🚶 STALKING → {hunt.GetCurrentTarget()?.GetEntityName()}",
                HuntState.Chasing => $"🏃 CHASING → {hunt.GetCurrentTarget()?.GetEntityName()}",
                HuntState.Attacking => $"⚔️ ATTACKING → {hunt.GetCurrentTarget()?.GetEntityName()}",
                _ => "???"
            };
            
            huntStyle.normal.textColor = hunt.GetHuntState() switch
            {
                HuntState.Spotting => Color.yellow,
                HuntState.Stalking => Color.green,
                HuntState.Chasing => Color.red,
                HuntState.Attacking => Color.magenta,
                _ => Color.white
            };
            
            UnityEditor.Handles.Label(huntLabelPos, huntText, huntStyle);
        }
        
        // ========== FEAR STATE LABEL (3.5m height) ==========
        if (fear != null)
        {
            Vector3 fearLabelPos = transform.position + Vector3.up * 3.5f;
            
            GUIStyle fearStyle = new GUIStyle();
            fearStyle.alignment = TextAnchor.MiddleCenter;
            fearStyle.fontSize = 11;
            fearStyle.fontStyle = FontStyle.Bold;
            
            string fearText = "";
            
            if (fear.IsFleeing())
            {
                fearText = "😱 FLEEING";
                fearStyle.normal.textColor = new Color(0.5f, 0f, 1f);
            }
            else if (fear.GetCurrentFearLevel() >= 25f)
            {
                fearText = "😰 NERVOUS";
                fearStyle.normal.textColor = new Color(0.7f, 0.3f, 1f);
            }
            else
            {
                fearText = "😌 CALM";
                fearStyle.normal.textColor = new Color(0.9f, 0.7f, 1f);
            }
            
            fearText += $" | Fear: {fear.GetCurrentFearLevel():F0}";
            
            UnityEditor.Handles.Label(fearLabelPos, fearText, fearStyle);
        }
        
        // ========== HP BAR (2.5m) ==========
        if (health != null)
        {
            Vector3 hpBarPos = transform.position + Vector3.up * 2.5f;
            DrawBar(hpBarPos, health.GetHealthPercent(), Color.green, Color.red, 1f, 0.1f);
        }
        
        // ========== STAMINA BAR (2.2m) ==========
        if (stamina != null)
        {
            Vector3 stamBarPos = transform.position + Vector3.up * 2.2f;
            Color stamColor = stamina.GetCurrentState() switch
            {
                StaminaState.Exhausted => Color.red,
                StaminaState.Recovering => Color.yellow,
                _ => Color.cyan
            };
            DrawBar(stamBarPos, stamina.GetStaminaPercent(), stamColor, Color.gray, 1f, 0.1f);
        }
        
        // ========== HUNGER BAR (1.9m) ==========
        if (hunger != null)
        {
            Vector3 hungerBarPos = transform.position + Vector3.up * 1.9f;
            float hungerPercent = hunger.GetHungerPercent();
            Color hungerColor = hungerPercent > 0.6f ? Color.green : 
                               hungerPercent > 0.3f ? Color.yellow : Color.red;
            DrawBar(hungerBarPos, hungerPercent, hungerColor, Color.gray, 1f, 0.1f);
        }
        
        // ========== FEAR BAR (1.6m) ==========
        if (fear != null)
        {
            Vector3 fearBarPos = transform.position + Vector3.up * 1.6f;
            float fearPercent = fear.GetCurrentFearLevel() / fear.GetMaxFearLevel();
            Color fearColor = new Color(0.5f, 0f, 1f, 1f);
            DrawBar(fearBarPos, fearPercent, fearColor, Color.gray, 1f, 0.1f);
        }
        
        #endif
    }
    
    /// <summary>
    /// Helper method per disegnare progress bar.
    /// </summary>
    private void DrawBar(Vector3 position, float fillPercent, Color fillColor, Color bgColor, float width, float height)
    {
        // Background
        Gizmos.color = bgColor;
        Gizmos.DrawCube(position, new Vector3(width, height, 0.01f));
        
        // Fill
        Gizmos.color = fillColor;
        float fillWidth = width * fillPercent;
        Vector3 fillPos = position - new Vector3(width * (1 - fillPercent) * 0.5f, 0, 0);
        Gizmos.DrawCube(fillPos, new Vector3(fillWidth, height, 0.02f));
    }
}

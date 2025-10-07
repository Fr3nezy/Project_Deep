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
    protected Entity lastDamageSource = null;

    
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
        EntityManager.Instance.RegisterEntity(this);
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
    OnDeath(lastDamageSource); // ← CAMBIA da null a lastDamageSource!
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
    lastDamageSource = source;
    
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
    
    // ========== SOLO NOME IN ALTO (4.5m) ==========
    Vector3 namePos = transform.position + Vector3.up * 4.5f;
    
    GUIStyle nameStyle = new GUIStyle();
    nameStyle.alignment = TextAnchor.MiddleCenter;
    nameStyle.fontSize = 14;
    nameStyle.fontStyle = FontStyle.Bold;
    nameStyle.normal.textColor = Color.white;
    
    string nameText = $"{entityName}";
    UnityEditor.Handles.Label(namePos, nameText, nameStyle);
    
    // ========== BARS CON LABELS (2.8m - 1.6m) ==========
    float barStartY = 2.8f;
    float barSpacing = 0.3f;
    float barWidth = 2f;
    float barHeight = 0.15f;
    int barIndex = 0;
    
    // HP Bar
    if (health != null)
    {
        Vector3 barPos = transform.position + Vector3.up * (barStartY - barSpacing * barIndex);
        DrawLabeledBar(barPos, "HP", health.GetHealthPercent(), 
                      health.GetCurrentHealth(), health.GetMaxHealth(),
                      Color.green, Color.red, barWidth, barHeight);
        barIndex++;
    }
    
    // Stamina Bar
    if (stamina != null)
    {
        Vector3 barPos = transform.position + Vector3.up * (barStartY - barSpacing * barIndex);
        Color stamColor = stamina.GetCurrentState() switch
        {
            StaminaState.Exhausted => Color.red,
            StaminaState.Recovering => Color.yellow,
            _ => Color.cyan
        };
        DrawLabeledBar(barPos, "Stamina", stamina.GetStaminaPercent(),
                      stamina.GetCurrentStamina(), stamina.GetMaxStamina(),
                      stamColor, Color.gray, barWidth, barHeight);
        barIndex++;
    }
    
    // Hunger Bar
    if (hunger != null)
    {
        Vector3 barPos = transform.position + Vector3.up * (barStartY - barSpacing * barIndex);
        float hungerPercent = hunger.GetHungerPercent();
        Color hungerColor = hungerPercent > 0.6f ? Color.green : 
                           hungerPercent > 0.3f ? Color.yellow : Color.red;
        DrawLabeledBar(barPos, "Hunger", hungerPercent,
                      hunger.GetCurrentHunger(), hunger.GetMaxHunger(),
                      hungerColor, Color.gray, barWidth, barHeight);
        barIndex++;
    }
    
    // Fear Bar
    if (fear != null)
    {
        Vector3 barPos = transform.position + Vector3.up * (barStartY - barSpacing * barIndex);
        float fearPercent = fear.GetCurrentFearLevel() / fear.GetMaxFearLevel();
        Color fearColor = new Color(0.5f, 0f, 1f, 1f);
        DrawLabeledBar(barPos, "Fear", fearPercent,
                      fear.GetCurrentFearLevel(), fear.GetMaxFearLevel(),
                      fearColor, Color.gray, barWidth, barHeight);
        barIndex++;
    }
    
    // ========== HUNT STATE (sotto bars) ==========
    if (hunt != null && hunt.IsHunting())
    {
        Vector3 huntPos = transform.position + Vector3.up * (barStartY - barSpacing * barIndex - 0.2f);
        
        GUIStyle huntStyle = new GUIStyle();
        huntStyle.alignment = TextAnchor.MiddleCenter;
        huntStyle.fontSize = 10;
        huntStyle.fontStyle = FontStyle.Bold;
        
        string huntText = hunt.GetHuntState() switch
        {
            HuntState.Spotting => $"👁️ SPOTTING",
            HuntState.Stalking => $"🚶 STALKING → {hunt.GetCurrentTarget()?.GetEntityName()}",
            HuntState.Chasing => $"🏃 CHASING → {hunt.GetCurrentTarget()?.GetEntityName()}",
            HuntState.Attacking => $"⚔️ ATTACKING → {hunt.GetCurrentTarget()?.GetEntityName()}",
            _ => ""
        };
        
        huntStyle.normal.textColor = hunt.GetHuntState() switch
        {
            HuntState.Spotting => Color.yellow,
            HuntState.Stalking => Color.green,
            HuntState.Chasing => Color.red,
            HuntState.Attacking => Color.magenta,
            _ => Color.white
        };
        
        if (!string.IsNullOrEmpty(huntText))
        {
            UnityEditor.Handles.Label(huntPos, huntText, huntStyle);
            barIndex++;
        }
    }
    
    // ========== FEAR STATE (sotto hunt) ==========
    if (fear != null && fear.IsFleeing())
    {
        Vector3 fearPos = transform.position + Vector3.up * (barStartY - barSpacing * barIndex - 0.2f);
        
        GUIStyle fearStyle = new GUIStyle();
        fearStyle.alignment = TextAnchor.MiddleCenter;
        fearStyle.fontSize = 10;
        fearStyle.fontStyle = FontStyle.Bold;
        fearStyle.normal.textColor = new Color(0.5f, 0f, 1f);
        
        string fearText = $"😱 FLEEING from {fear.GetPrimaryThreat()?.GetEntityName()}";
        
        UnityEditor.Handles.Label(fearPos, fearText, fearStyle);
    }
    
    #endif
}

/// <summary>
/// Disegna bar con label a sinistra e value a destra.
/// </summary>
private void DrawLabeledBar(Vector3 position, string label, float fillPercent, 
                            float currentValue, float maxValue,
                            Color fillColor, Color bgColor, float width, float height)
{
    #if UNITY_EDITOR
    
    // Label a sinistra
    Vector3 labelPos = position + Vector3.left * (width / 2f + 0.5f);
    GUIStyle labelStyle = new GUIStyle();
    labelStyle.alignment = TextAnchor.MiddleRight;
    labelStyle.fontSize = 9;
    labelStyle.normal.textColor = Color.white;
    UnityEditor.Handles.Label(labelPos, $"{label}:", labelStyle);
    
    // Background bar
    Gizmos.color = bgColor;
    Gizmos.DrawCube(position, new Vector3(width, height, 0.01f));
    
    // Fill bar
    Gizmos.color = fillColor;
    float fillWidth = width * fillPercent;
    Vector3 fillPos = position - new Vector3(width * (1 - fillPercent) * 0.5f, 0, 0);
    Gizmos.DrawCube(fillPos, new Vector3(fillWidth, height, 0.02f));
    
    // Value a destra
    Vector3 valuePos = position + Vector3.right * (width / 2f + 0.3f);
    GUIStyle valueStyle = new GUIStyle();
    valueStyle.alignment = TextAnchor.MiddleLeft;
    valueStyle.fontSize = 9;
    valueStyle.normal.textColor = fillColor;
    string valueText = $"{currentValue:F0}/{maxValue:F0}";
    UnityEditor.Handles.Label(valuePos, valueText, valueStyle);
    
    #endif
}
}

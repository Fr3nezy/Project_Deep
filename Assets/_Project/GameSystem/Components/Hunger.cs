using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestisce fame per entità.
/// Include decay, starvation damage, e feeding mechanics.
/// </summary>
public class HungerComponent : MonoBehaviour
{
    [Header("Hunger Settings")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float currentHunger = 0f; // 0 = sazio, 100 = affamato
    
    [Header("Decay Settings")]
    [SerializeField] private float baseDecayRate = 0.011f; // ~1 punto ogni 90 sec
    [SerializeField] private bool useMovementBasedDecay = false;
    [SerializeField] private float movementDecayMultiplier = 0.1f;
    
    [Header("Thresholds")]
    [SerializeField] private float hungryThreshold = 40f;   // Inizia a cercare cibo
    [SerializeField] private float starvingThreshold = 70f; // Priorità assoluta cibo
    
    [Header("Starvation Damage")]
    [SerializeField] private bool enableStarvationDamage = true;
    [SerializeField] private float starvationDamageInterval = 10f; // Damage ogni X secondi
    [SerializeField] private float baseDamagePerTick = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Events
    public UnityEvent<float> OnHungerChanged;
    public UnityEvent OnBecomeHungry;      // Hunger > hungryThreshold
    public UnityEvent OnBecomeStarving;    // Hunger > starvingThreshold
    public UnityEvent OnFed;               // Quando mangia
    public UnityEvent<float> OnStarvationDamage; // Quando riceve danno da fame
    
    // State
    private HungerState currentState = HungerState.Normal;
    private float lastStarvationDamageTime = 0f;
    private Rigidbody rb;
    private Entity entity;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        entity = GetComponent<Entity>();
    }
    
    void Update()
    {
        UpdateHunger();
        UpdateStarvationDamage();
        UpdateHungerState();
    }
    
    /// <summary>
    /// Aumenta hunger nel tempo.
    /// </summary>
    private void UpdateHunger()
    {
        float decayAmount = baseDecayRate * Time.deltaTime;
        
        // Movement-based decay (future enhancement)
        if (useMovementBasedDecay && rb != null)
        {
            float velocityMagnitude = rb.linearVelocity.magnitude;
            float movementMultiplier = 1f + (velocityMagnitude * movementDecayMultiplier);
            decayAmount *= movementMultiplier;
        }
        
        currentHunger += decayAmount;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);
        
        OnHungerChanged?.Invoke(currentHunger);
    }
    
    /// <summary>
    /// Applica damage se in starvation.
    /// </summary>
    private void UpdateStarvationDamage()
    {
        if (!enableStarvationDamage) return;
        if (currentHunger < starvingThreshold) return;
        
        // Check interval
        if (Time.time - lastStarvationDamageTime < starvationDamageInterval)
            return;
        
        lastStarvationDamageTime = Time.time;
        
        // Calcola damage con multiplier basato su quanto è affamato
        float hungerOverThreshold = currentHunger - starvingThreshold;
        float maxHungerOverThreshold = maxHunger - starvingThreshold;
        float damageMultiplier = hungerOverThreshold / maxHungerOverThreshold;
        
        float damage = baseDamagePerTick * (1f + damageMultiplier);
        
        if (showDebugLogs)
            Debug.Log($"💀 {gameObject.name} starvation damage: {damage:F1} (Hunger: {currentHunger:F0})");
        
        OnStarvationDamage?.Invoke(damage);
        
        // Applica damage a health component
        if (entity != null && entity.HasHealth())
        {
            entity.TakeDamage(damage, null);
        }
    }
    
    /// <summary>
    /// Aggiorna stato hunger e triggera eventi.
    /// </summary>
    private void UpdateHungerState()
    {
        HungerState newState = HungerState.Normal;
        
        if (currentHunger >= starvingThreshold)
            newState = HungerState.Starving;
        else if (currentHunger >= hungryThreshold)
            newState = HungerState.Hungry;
        
        // Check transizioni stato
        if (newState != currentState)
        {
            if (newState == HungerState.Hungry && currentState == HungerState.Normal)
            {
                OnBecomeHungry?.Invoke();
                if (showDebugLogs)
                    Debug.Log($"🍽️ {gameObject.name} is now HUNGRY");
            }
            else if (newState == HungerState.Starving && currentState == HungerState.Hungry)
            {
                OnBecomeStarving?.Invoke();
                if (showDebugLogs)
                    Debug.Log($"💀 {gameObject.name} is now STARVING!");
            }
            
            currentState = newState;
        }
    }
    
    /// <summary>
    /// Riduce hunger quando mangia.
    /// </summary>
    public void Feed(float amount)
    {
        float previousHunger = currentHunger;
        
        currentHunger -= amount;
        currentHunger = Mathf.Max(currentHunger, 0);
        
        if (showDebugLogs)
            Debug.Log($"🍖 {gameObject.name} ate! Hunger: {previousHunger:F0} → {currentHunger:F0} (-{amount})");
        
        OnFed?.Invoke();
        OnHungerChanged?.Invoke(currentHunger);
        
        // Se torna sotto threshold, resetta stato
        UpdateHungerState();
    }
    
    /// <summary>
    /// Aumenta hunger manualmente (per testing/debuff).
    /// </summary>
    public void IncreaseHunger(float amount)
    {
        currentHunger += amount;
        currentHunger = Mathf.Min(currentHunger, maxHunger);
        
        OnHungerChanged?.Invoke(currentHunger);
        
        if (showDebugLogs)
            Debug.Log($"{gameObject.name} hunger increased by {amount} → {currentHunger:F0}");
    }
    
    /// <summary>
    /// Resetta hunger a 0 (sazio).
    /// </summary>
    public void ResetHunger()
    {
        currentHunger = 0f;
        currentState = HungerState.Normal;
        OnHungerChanged?.Invoke(currentHunger);
        
        if (showDebugLogs)
            Debug.Log($"{gameObject.name} hunger reset to 0");
    }
    
    #region Getters
    
    public float GetCurrentHunger() => currentHunger;
    public float GetMaxHunger() => maxHunger;
    public float GetHungerPercentage() => currentHunger / maxHunger;
    public HungerState GetCurrentState() => currentState;
    
    public bool IsHungry() => currentHunger >= hungryThreshold;
    public bool IsStarving() => currentHunger >= starvingThreshold;
    public bool IsSatiated() => currentHunger < hungryThreshold;
    
    public float GetHungryThreshold() => hungryThreshold;
    public float GetStarvingThreshold() => starvingThreshold;
    
    #endregion
    
    #region Debug Visualization
    
void OnDrawGizmos()
{
    if (!Application.isPlaying) return;
    
    Vector3 pos = transform.position + Vector3.up * 1.9f; // ← CAMBIATO da 1.2f
    float barWidth = 1f;
    float barHeight = 0.1f;
    
    // Background (grigio)
    Gizmos.color = Color.gray;
    Gizmos.DrawCube(pos, new Vector3(barWidth, barHeight, 0.01f));
    
    // Foreground (colore basato su stato)
    Color hungerColor = currentState switch
    {
        HungerState.Normal => Color.green,
        HungerState.Hungry => Color.yellow,
        HungerState.Starving => Color.red,
        _ => Color.white
    };
    
    Gizmos.color = hungerColor;
    float hungerPercent = currentHunger / maxHunger;
    Vector3 hungerBarPos = pos - new Vector3(barWidth * (1 - hungerPercent) * 0.5f, 0, 0);
    Gizmos.DrawCube(hungerBarPos, new Vector3(barWidth * hungerPercent, barHeight, 0.02f));
}
    
    #endregion
}

/// <summary>
/// Stati hunger.
/// </summary>
public enum HungerState
{
    Normal,   // 0-40: Non cerca cibo attivamente
    Hungry,   // 40-70: Cerca cibo
    Starving  // 70-100: Priorità assoluta + damage
}

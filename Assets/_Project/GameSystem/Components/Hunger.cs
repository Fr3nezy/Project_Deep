using UnityEngine;
using UnityEngine.Events;

public enum HungerState
{
    Normal,
    Hungry,
    Starving
}

/// <summary>
/// Gestisce hunger, decay, starvation damage.
/// </summary>
public class HungerComponent : MonoBehaviour
{
    [Header("Hunger Settings")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float currentHunger;
    [SerializeField] private float baseDecayRate = 0.5f;
    
    [Header("Thresholds")]
    [SerializeField] private float hungryThreshold = 40f;
    [SerializeField] private float starvingThreshold = 20f;
    
    [Header("Starvation")]
    [SerializeField] private float starvationDamage = 5f;
    [SerializeField] private float starvationDamageInterval = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Events
    public UnityEvent OnHungry;
    public UnityEvent OnStarving;
    public UnityEvent OnFed;
    public UnityEvent<float> OnHungerChanged;
    
    // State
    private HungerState currentState = HungerState.Normal;
    private float lastStarvationDamageTime = 0f;
    
    // Component cache
    private Entity entity;
    
    void Awake()
    {
        entity = GetComponent<Entity>();
        currentHunger = maxHunger;
    }
    
    void Update()
    {
        UpdateHungerDecay();
        UpdateHungerState();
        UpdateStarvation();
    }
    
    private void UpdateHungerDecay()
    {
        currentHunger -= baseDecayRate * Time.deltaTime;
        currentHunger = Mathf.Max(currentHunger, 0f);
        
        OnHungerChanged?.Invoke(currentHunger);
    }
    
    private void UpdateHungerState()
    {
        HungerState newState = currentHunger switch
        {
            <= 0f => HungerState.Starving,
            < 20f => HungerState.Starving,
            < 40f => HungerState.Hungry,
            _ => HungerState.Normal
        };
        
        if (newState != currentState)
        {
            currentState = newState;
            
            if (currentState == HungerState.Hungry)
            {
                OnHungry?.Invoke();
            }
            else if (currentState == HungerState.Starving)
            {
                OnStarving?.Invoke();
            }
        }
    }
    
    private void UpdateStarvation()
    {
        if (currentState != HungerState.Starving) return;
        
        // FIX: Usa GetHealthComponent() invece di HasHealth()
        if (entity.GetHealthComponent() == null) return;
        
        if (Time.time - lastStarvationDamageTime >= starvationDamageInterval)
        {
            entity.TakeDamage(starvationDamage, null);
            lastStarvationDamageTime = Time.time;
            
            if (showDebugLogs)
                Debug.Log($"[Hunger] {gameObject.name} taking starvation damage!");
        }
    }
    
    public void Feed(float amount)
    {
        float oldHunger = currentHunger;
        currentHunger += amount;
        currentHunger = Mathf.Min(currentHunger, maxHunger);
        
        if (currentHunger > oldHunger)
        {
            if (showDebugLogs)
                Debug.Log($"[Hunger] {gameObject.name} fed +{amount}! Hunger: {currentHunger}/{maxHunger}");
            
            OnFed?.Invoke();
            OnHungerChanged?.Invoke(currentHunger);
        }
    }
    
    public void IncreaseHunger(float amount)
    {
        currentHunger -= amount;
        currentHunger = Mathf.Max(currentHunger, 0f);
        OnHungerChanged?.Invoke(currentHunger);
    }
    
    public void ResetHunger()
    {
        currentHunger = maxHunger;
        currentState = HungerState.Normal;
        OnHungerChanged?.Invoke(currentHunger);
    }
    
    // Setters per EntityConfig
    public void SetBaseDecayRate(float rate)
    {
        baseDecayRate = rate;
    }
    
    public void SetHungryThreshold(float threshold)
    {
        hungryThreshold = threshold;
    }
    
    // Getters
    public float GetCurrentHunger() => currentHunger;
    public float GetMaxHunger() => maxHunger;
    public float GetHungerPercent() => currentHunger / maxHunger;
    public HungerState GetCurrentState() => currentState;
    public bool IsHungry() => currentState == HungerState.Hungry || currentState == HungerState.Starving;
    public bool IsStarving() => currentState == HungerState.Starving;
    public float GetHungryThreshold() => hungryThreshold;
}

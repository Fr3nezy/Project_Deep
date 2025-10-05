using UnityEngine;
using UnityEngine.Events;

public enum StaminaState
{
    Normal,
    Recovering,
    Exhausted
}

/// <summary>
/// Gestisce stamina, sprint, exhaustion, recovery.
/// </summary>
public class StaminaComponent : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float regenRate = 10f;
    [SerializeField] private float regenDelay = 1f;
    [SerializeField] private float exhaustedDuration = 3f;
    
    [Header("Speed Multipliers")]
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float exhaustedMultiplier = 0.75f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Events
    public UnityEvent OnStaminaExhausted;
    public UnityEvent OnStaminaRecovered;
    public UnityEvent<float> OnStaminaChanged;
    
    // State
    private StaminaState currentState = StaminaState.Normal;
    private float lastStaminaUseTime = 0f;
    private float lastExhaustionTime = 0f;
    
    void Awake()
    {
        currentStamina = maxStamina;
    }
    
    void Update()
    {
        UpdateStaminaState();
        UpdateRegeneration();
    }
    
    private void UpdateStaminaState()
    {
        switch (currentState)
        {
            case StaminaState.Exhausted:
                if (Time.time - lastExhaustionTime >= exhaustedDuration)
                {
                    EnterRecovering();
                }
                break;
                
            case StaminaState.Recovering:
                if (currentStamina >= maxStamina)
                {
                    EnterNormal();
                }
                break;
        }
    }
    
    private void UpdateRegeneration()
    {
        if (currentState == StaminaState.Exhausted) return;
        if (currentStamina >= maxStamina) return;
        
        if (Time.time - lastStaminaUseTime >= regenDelay)
        {
            float regenAmount = regenRate * Time.deltaTime;
            currentStamina += regenAmount;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
            
            OnStaminaChanged?.Invoke(currentStamina);
        }
    }
    
    /// <summary>
    /// Consume stamina (chiamato da Movement/Hunt/Fear).
    /// </summary>
    public void ConsumeStamina(float amount)
    {
        if (currentState == StaminaState.Exhausted) return;
        
        currentStamina -= amount;
        lastStaminaUseTime = Time.time;
        
        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            SetExhausted();
        }
        
        OnStaminaChanged?.Invoke(currentStamina);
    }
    
    /// <summary>
    /// Enter exhausted state.
    /// </summary>
    private void SetExhausted()
    {
        currentState = StaminaState.Exhausted;
        lastExhaustionTime = Time.time;
        
        if (showDebugLogs)
            Debug.Log($"[Stamina] {gameObject.name} is EXHAUSTED!");
        
        OnStaminaExhausted?.Invoke();
    }
    
    private void EnterRecovering()
    {
        currentState = StaminaState.Recovering;
        
        if (showDebugLogs)
            Debug.Log($"[Stamina] {gameObject.name} is RECOVERING");
    }
    
    private void EnterNormal()
    {
        currentState = StaminaState.Normal;
        
        if (showDebugLogs)
            Debug.Log($"[Stamina] {gameObject.name} RECOVERED!");
        
        OnStaminaRecovered?.Invoke();
    }
    
    /// <summary>
    /// Start sprint (marker per Hunt/Fear).
    /// </summary>
    public void StartSprint()
    {
        if (showDebugLogs)
            Debug.Log($"[Stamina] {gameObject.name} started sprinting");
    }
    
    /// <summary>
    /// Stop sprint.
    /// </summary>
    public void StopSprint()
    {
        if (showDebugLogs)
            Debug.Log($"[Stamina] {gameObject.name} stopped sprinting");
    }
    
    public void ResetStamina()
    {
        currentStamina = maxStamina;
        currentState = StaminaState.Normal;
        OnStaminaChanged?.Invoke(currentStamina);
    }
    
    // Setters per EntityConfig
    public void SetMaxStamina(float max)
    {
        maxStamina = max;
        currentStamina = Mathf.Min(currentStamina, maxStamina);
    }
    
    // Getters
    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina() => maxStamina;
    public float GetStaminaPercent() => currentStamina / maxStamina;
    public StaminaState GetCurrentState() => currentState;
    public bool IsExhausted() => currentState == StaminaState.Exhausted;
    public bool IsRecovering() => currentState == StaminaState.Recovering;
    public float GetSpeedMultiplier()
    {
        return currentState switch
        {
            StaminaState.Exhausted => exhaustedMultiplier,
            _ => 1f
        };
    }
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestisce stamina per sprint, jetpack e altre azioni.
/// Include exhaustion system e speed multipliers.
/// </summary>
public class StaminaComponent : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;
    
    [Header("Depletion & Regen")]
    [SerializeField] private float depletionRate = 15f; // Stamina/sec durante uso
    [SerializeField] private float regenRate = 10f;     // Stamina/sec durante riposo
    [SerializeField] private float regenDelay = 1f;     // Secondi prima che inizi regen
    
    [Header("Speed Modifiers")]
    [SerializeField] private float sprintMultiplier = 1.5f;    // Boost durante sprint
    [SerializeField] private float exhaustionPenalty = 0.75f;  // Penalty quando esausto
    [SerializeField] private float exhaustionDuration = 3f;    // Durata exhaustion
    [SerializeField] private float exhaustionThreshold = 10f;  // Stamina sotto cui scatta exhaustion
    
    [Header("State")]
    [SerializeField] private StaminaState currentState = StaminaState.Normal;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Events
    public UnityEvent<float> OnStaminaChanged;
    public UnityEvent OnExhausted;
    public UnityEvent OnRecovered;
    
    // State tracking
    private bool isUsingStamina = false;
    private float lastUseTime = 0f;
    private float exhaustionEndTime = 0f;
    
    void Awake()
    {
        currentStamina = maxStamina;
    }
    
    void Update()
    {
        UpdateStaminaState();
        HandleRegeneration();
    }
    
    /// <summary>
    /// Update dello state machine stamina.
    /// </summary>
    private void UpdateStaminaState()
    {
        switch (currentState)
        {
            case StaminaState.Normal:
                // Check se scende sotto threshold
                if (currentStamina < exhaustionThreshold)
                {
                    EnterExhaustion();
                }
                break;
                
            case StaminaState.Exhausted:
                // Check se è finito exhaustion timer
                if (Time.time >= exhaustionEndTime)
                {
                    currentState = StaminaState.Recovering;
                    if (showDebugLogs)
                        Debug.Log($"{gameObject.name}: Exhaustion ended, now recovering");
                }
                break;
                
            case StaminaState.Recovering:
                // Check se stamina >= 30% per tornare normale
                if (currentStamina >= maxStamina * 0.3f)
                {
                    currentState = StaminaState.Normal;
                    OnRecovered?.Invoke();
                    if (showDebugLogs)
                        Debug.Log($"{gameObject.name}: Recovered! Back to normal");
                }
                break;
        }
    }
    
    /// <summary>
    /// Gestisce rigenerazione automatica.
    /// </summary>
    private void HandleRegeneration()
    {
        // Non rigenera se sta usando stamina O è in exhaustion
        if (isUsingStamina || currentState == StaminaState.Exhausted)
            return;
        
        // Check delay dopo ultimo uso
        if (Time.time - lastUseTime < regenDelay)
            return;
        
        // Rigenera se non full
        if (currentStamina < maxStamina)
        {
            Regenerate(regenRate * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Usa stamina (chiamato ogni frame durante sprint/jetpack).
    /// Ritorna true se aveva stamina disponibile.
    /// </summary>
    public bool UseStamina(float amount)
    {
        // Se è esausto, non può usare stamina
        if (currentState == StaminaState.Exhausted)
            return false;
        
        // Se non ha stamina, non può usare
        if (currentStamina <= 0)
        {
            if (currentState != StaminaState.Exhausted)
                EnterExhaustion();
            return false;
        }
        
        isUsingStamina = true;
        lastUseTime = Time.time;
        
        currentStamina -= amount;
        currentStamina = Mathf.Max(currentStamina, 0);
        
        OnStaminaChanged?.Invoke(currentStamina);
        
        // Check se va in exhaustion
        if (currentStamina < exhaustionThreshold && currentState == StaminaState.Normal)
        {
            EnterExhaustion();
        }
        
        return true;
    }
    
    /// <summary>
    /// Shortcut per sprint (usa depletionRate configurato).
    /// </summary>
    public bool UseSprint(float deltaTime)
    {
        return UseStamina(depletionRate * deltaTime);
    }
    
    /// <summary>
    /// Ferma uso stamina (chiamato quando smetti di sprintare).
    /// </summary>
    public void StopUsingStamina()
    {
        isUsingStamina = false;
        lastUseTime = Time.time;
    }
    
    /// <summary>
    /// Rigenera stamina.
    /// </summary>
    private void Regenerate(float amount)
    {
        currentStamina += amount;
        currentStamina = Mathf.Min(currentStamina, maxStamina);
        
        OnStaminaChanged?.Invoke(currentStamina);
    }
    
    /// <summary>
    /// Entra in stato exhaustion.
    /// </summary>
    private void EnterExhaustion()
    {
        currentState = StaminaState.Exhausted;
        exhaustionEndTime = Time.time + exhaustionDuration;
        isUsingStamina = false;
        
        OnExhausted?.Invoke();
        
        if (showDebugLogs)
            Debug.Log($"💨 {gameObject.name} EXHAUSTED! Penalty per {exhaustionDuration}s");
    }
    
    /// <summary>
    /// Ritorna speed multiplier corrente basato su stato.
    /// </summary>
    public float GetSpeedMultiplier()
    {
        switch (currentState)
        {
            case StaminaState.Normal:
                return isUsingStamina ? sprintMultiplier : 1f;
                
            case StaminaState.Exhausted:
            case StaminaState.Recovering:
                return exhaustionPenalty;
                
            default:
                return 1f;
        }
    }
    
    /// <summary>
    /// Check se può sprintare (ha stamina + non è esausto).
    /// </summary>
    public bool CanUseSprint()
    {
        return currentStamina > 0 && currentState != StaminaState.Exhausted;
    }
    
    /// <summary>
    /// Ripristina stamina istantaneamente (per debug/powerup).
    /// </summary>
    public void RestoreStamina(float amount)
    {
        currentStamina += amount;
        currentStamina = Mathf.Min(currentStamina, maxStamina);
        
        OnStaminaChanged?.Invoke(currentStamina);
        
        if (showDebugLogs)
            Debug.Log($"{gameObject.name} +{amount} stamina → {currentStamina}/{maxStamina}");
    }
    
    /// <summary>
    /// Resetta a full stamina.
    /// </summary>
    public void ResetStamina()
    {
        currentStamina = maxStamina;
        currentState = StaminaState.Normal;
        isUsingStamina = false;
        
        OnStaminaChanged?.Invoke(currentStamina);
    }
    
    #region Getters
    
    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina() => maxStamina;
    public float GetStaminaPercentage() => currentStamina / maxStamina;
    public StaminaState GetCurrentState() => currentState;
    public bool IsExhausted() => currentState == StaminaState.Exhausted;
    public bool IsRecovering() => currentState == StaminaState.Recovering;
    public bool IsUsingStamina() => isUsingStamina;
    
    #endregion
    
    #region Debug Visualization
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Barra stamina sotto HP bar
        Vector3 pos = transform.position + Vector3.up * 1.5f;
        float barWidth = 1f;
        float barHeight = 0.08f;
        
        // Background (grigio)
        Gizmos.color = Color.gray;
        Gizmos.DrawCube(pos, new Vector3(barWidth, barHeight, 0.01f));
        
        // Foreground (colore basato su stato)
        Color staminaColor = currentState switch
        {
            StaminaState.Normal => Color.cyan,
            StaminaState.Exhausted => Color.red,
            StaminaState.Recovering => Color.yellow,
            _ => Color.white
        };
        
        Gizmos.color = staminaColor;
        float staminaPercent = currentStamina / maxStamina;
        Vector3 staminaBarPos = pos - new Vector3(barWidth * (1 - staminaPercent) * 0.5f, 0, 0);
        Gizmos.DrawCube(staminaBarPos, new Vector3(barWidth * staminaPercent, barHeight, 0.02f));
    }
    
    #endregion
}

/// <summary>
/// Stati possibili per stamina system.
/// </summary>
public enum StaminaState
{
    Normal,      // Può usare stamina normalmente
    Exhausted,   // Esausto, penalty attiva
    Recovering   // In recupero dopo exhaustion
}

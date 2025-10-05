using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestisce HP, damage, morte, regen.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool canRegenerate = false;
    [SerializeField] private float regenRate = 5f;
    [SerializeField] private float regenDelay = 3f;
    
    [Header("Invulnerability")]
    [SerializeField] private bool useInvulnerabilityFrames = false;
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Events
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent<float> OnDamageTaken;
    public UnityEvent<float> OnHealed;
    
    // State
    private bool isDead = false;
    private bool isInvulnerable = false;
    private float lastDamageTime = 0f;
    private float invulnerabilityEndTime = 0f;
    
    // Component cache
    private Entity entity;
    
    void Awake()
    {
        entity = GetComponent<Entity>();
        currentHealth = maxHealth;
    }
    
    void Update()
    {
        if (isDead) return;
        
        UpdateInvulnerability();
        
        if (canRegenerate)
        {
            UpdateRegeneration();
        }
    }
    
    private void UpdateInvulnerability()
    {
        if (isInvulnerable && Time.time >= invulnerabilityEndTime)
        {
            isInvulnerable = false;
        }
    }
    
    private void UpdateRegeneration()
    {
        if (currentHealth >= maxHealth) return;
        if (Time.time - lastDamageTime < regenDelay) return;
        
        Heal(regenRate * Time.deltaTime);
    }
    
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        if (isInvulnerable) return;
        if (damage <= 0f) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
        
        lastDamageTime = Time.time;
        
        if (showDebugLogs)
            Debug.Log($"[Health] {gameObject.name} took {damage} damage! HP: {currentHealth}/{maxHealth}");
        
        OnDamageTaken?.Invoke(damage);
        OnHealthChanged?.Invoke(currentHealth);
        
        if (useInvulnerabilityFrames)
        {
            isInvulnerable = true;
            invulnerabilityEndTime = Time.time + invulnerabilityDuration;
        }
        
        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        if (isDead) return;
        if (amount <= 0f) return;
        
        float oldHealth = currentHealth;
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        
        float actualHealed = currentHealth - oldHealth;
        
        if (actualHealed > 0f)
        {
            if (showDebugLogs)
                Debug.Log($"[Health] {gameObject.name} healed {actualHealed}! HP: {currentHealth}/{maxHealth}");
            
            OnHealed?.Invoke(actualHealed);
            OnHealthChanged?.Invoke(currentHealth);
        }
    }
    
    private void Die()
    {
        isDead = true;
        
        if (showDebugLogs)
            Debug.Log($"💀 [Health] {gameObject.name} died!");
        
        OnDeath?.Invoke();
        
        // ========== DESTROY ENTITY (FIXED) ==========
        // Delay per permettere death events/animation
        Destroy(gameObject, 0.5f);
        // ============================================
    }
    
    public void FullHeal()
    {
        Heal(maxHealth);
    }
    
    public void Revive()
    {
        isDead = false;
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    // Setters per EntityConfig
    public void SetMaxHealth(float max)
    {
        maxHealth = max;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }
    
    public void SetCurrentHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    // Getters
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercent() => currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsAlive() => !isDead;
    public bool IsInvulnerable() => isInvulnerable;
    public bool IsFullHealth() => currentHealth >= maxHealth;
}

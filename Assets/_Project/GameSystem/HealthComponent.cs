using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestisce la salute di un'entità.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool canRegenerate = false;
    [SerializeField] private float regenRate = 5f;
    [SerializeField] private float invulnerabilityTime = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent<Entity> OnDeath;
    public UnityEvent<float, Entity> OnDamageTaken;
    
    private float lastDamageTime = -999f;
    private bool isDead = false;
    
    void Awake()
    {
        currentHealth = maxHealth;
    }
    
    void Update()
    {
        if (canRegenerate && currentHealth < maxHealth && !isDead)
        {
            Regenerate(regenRate * Time.deltaTime);
        }
    }
    
    public void TakeDamage(float damage, Entity attacker = null)
    {
        if (isDead) return;
        
        if (Time.time - lastDamageTime < invulnerabilityTime)
        {
            if (showDebugLogs)
                Debug.Log($"{gameObject.name} è invulnerabile");
            return;
        }
        
        lastDamageTime = Time.time;
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        
        if (showDebugLogs)
            Debug.Log($"{gameObject.name} -{damage} HP → {currentHealth}/{maxHealth}");
        
        OnHealthChanged?.Invoke(currentHealth);
        OnDamageTaken?.Invoke(damage, attacker);
        
        if (currentHealth <= 0)
        {
            Die(attacker);
        }
    }
    
    public void Heal(float amount)
    {
        if (isDead) return;
        
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        
        if (showDebugLogs)
            Debug.Log($"{gameObject.name} +{amount} HP → {currentHealth}/{maxHealth}");
        
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    private void Regenerate(float amount)
    {
        Heal(amount);
    }
    
    private void Die(Entity killer)
    {
        if (isDead) return;
        
        isDead = true;
        
        if (showDebugLogs)
            Debug.Log($"💀 {gameObject.name} è morto! Killer: {(killer != null ? killer.GetEntityName() : "Unknown")}");
        
        OnDeath?.Invoke(killer);
        
        // Notifica la propria Entity
        Entity selfEntity = GetComponent<Entity>();
        if (selfEntity != null)
        {
            selfEntity.OnEntityDeath(killer);
        }
        
        // Notifica killer per Hunger
        if (killer != null && killer.HasHunger())
        {
            FoodValue foodValue = GetComponent<FoodValue>();
            float nutritionValue = foodValue != null ? foodValue.GetValue() : 20f;
            killer.GetHunger().Feed(nutritionValue);
        }
        
        Destroy(gameObject, 0.1f);
    }
    
    // Getters
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsFullHealth() => currentHealth >= maxHealth;
    
    // Debug Gizmo
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        Vector3 pos = transform.position + Vector3.up * 2f;
        float barWidth = 1f;
        float barHeight = 0.1f;
        
        Gizmos.color = Color.red;
        Gizmos.DrawCube(pos, new Vector3(barWidth, barHeight, 0.01f));
        
        Gizmos.color = Color.green;
        float healthPercent = currentHealth / maxHealth;
        Vector3 healthBarPos = pos - new Vector3(barWidth * (1 - healthPercent) * 0.5f, 0, 0);
        Gizmos.DrawCube(healthBarPos, new Vector3(barWidth * healthPercent, barHeight, 0.02f));
    }
}

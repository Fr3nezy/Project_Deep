using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Manages entity health points with damage and death handling.
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Regeneration")]
        [SerializeField] private bool canRegenerate = false;
        [SerializeField] private float regenRate = 5f; // HP per second
        [SerializeField] private float regenDelay = 3f; // Delay after taking damage

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        // Cached components
        private Entity entity;

        // State
        private float lastDamageTime = -999f;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            entity = GetComponent<Entity>();

            if (entity == null)
            {
                Debug.LogError($"[Health] {gameObject.name} missing Entity component!", this);
                enabled = false;
            }
        }

        private void Start()
        {
            currentHealth = maxHealth;
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE LOOP
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            if (entity == null || entity.IsDead())
                return;

            // Regenerate health if enabled
            if (canRegenerate && currentHealth < maxHealth)
            {
                float timeSinceDamage = Time.time - lastDamageTime;
                if (timeSinceDamage >= regenDelay)
                {
                    RegenerateHealth(regenRate * Time.deltaTime);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DAMAGE & HEALING
        // ═══════════════════════════════════════════════════════════════

        public void TakeDamage(float damageAmount, Entity attacker)
        {
            if (entity == null || entity.IsDead())
                return;

            if (damageAmount <= 0f)
                return;

            float oldHealth = currentHealth;
            currentHealth -= damageAmount;
            lastDamageTime = Time.time;

            if (showDebugLogs)
            {
                string attackerName = attacker != null ? attacker.GetEntityName() : "Starvation/Environment";
                Debug.Log($"[Health] {gameObject.name} took {damageAmount:F1} damage from {attackerName} ({oldHealth:F1} → {currentHealth:F1})");
            }

            // Check if dead
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        public void Heal(float healAmount)
        {
            if (entity == null || entity.IsDead())
                return;

            if (healAmount <= 0f)
                return;

            float oldHealth = currentHealth;
            currentHealth += healAmount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);

            if (showDebugLogs)
            {
                Debug.Log($"[Health] {gameObject.name} healed {healAmount:F1} ({oldHealth:F1} → {currentHealth:F1})");
            }
        }

        private void RegenerateHealth(float amount)
        {
            currentHealth += amount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }

        // ═══════════════════════════════════════════════════════════════
        // DEATH
        // ═══════════════════════════════════════════════════════════════

        private void Die()
        {
            if (entity == null)
                return;

            if (showDebugLogs)
            {
                Debug.Log($"[Health] {gameObject.name} DIED");
            }

            entity.Die();
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - Configuration
        // ═══════════════════════════════════════════════════════════════

        public void SetMaxHealth(float max)
        {
            maxHealth = Mathf.Max(10f, max);
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - State Queries
        // ═══════════════════════════════════════════════════════════════

        public float GetCurrentHealth() => currentHealth;
        public float GetMaxHealth() => maxHealth;
        public float GetHealthPercentage() => maxHealth > 0 ? (currentHealth / maxHealth) : 0f;
        public bool IsDead() => entity != null && entity.IsDead();
    }
}

using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Manages entity hunger level with starvation mechanics.
    /// </summary>
    public class HungerComponent : MonoBehaviour
    {
        [Header("Hunger Settings")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float currentHunger = 100f;
        [SerializeField] private float hungerDecayRate = 5f; // Hunger lost per second

        [Header("Starvation")]
        [SerializeField] private float starvationDamage = 2f; // Damage per second when starving
        [SerializeField] private bool canStarve = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        // Cached components
        private HealthComponent health;
        private Entity entity;

        // State
        private bool isStarving = false;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            entity = GetComponent<Entity>();
        }

        private void Start()
        {
            currentHunger = maxHunger;
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE LOOP
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            if (entity != null && entity.IsDead())
                return;

            // Decrease hunger over time
            DecreaseHunger(hungerDecayRate * Time.deltaTime);

            // Check starvation
            if (canStarve && currentHunger <= 0f)
            {
                if (!isStarving)
                {
                    EnterStarvation();
                }
                ApplyStarvationDamage();
            }
            else if (isStarving)
            {
                ExitStarvation();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HUNGER MANAGEMENT
        // ═══════════════════════════════════════════════════════════════

        private void DecreaseHunger(float amount)
        {
            currentHunger -= amount;
            currentHunger = Mathf.Max(0f, currentHunger);
        }

        public void Feed(float nutritionValue)
        {
            float oldHunger = currentHunger;
            currentHunger += nutritionValue;
            currentHunger = Mathf.Min(currentHunger, maxHunger);

            if (showDebugLogs)
            {
                Debug.Log($"[Hunger] {gameObject.name} fed: +{nutritionValue:F1} nutrition ({oldHunger:F1} → {currentHunger:F1})");
            }

            // Exit starvation if fed
            if (isStarving && currentHunger > 0f)
            {
                ExitStarvation();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STARVATION
        // ═══════════════════════════════════════════════════════════════

        private void EnterStarvation()
        {
            isStarving = true;

            if (showDebugLogs)
            {
                Debug.LogWarning($"[Hunger] {gameObject.name} is STARVING!");
            }
        }

        private void ExitStarvation()
        {
            isStarving = false;

            if (showDebugLogs)
            {
                Debug.Log($"[Hunger] {gameObject.name} no longer starving");
            }
        }

        private void ApplyStarvationDamage()
        {
            if (health != null)
            {
                // Fixed: TakeDamage(damage, attacker) - starvation has no attacker (null)
                health.TakeDamage(starvationDamage * Time.deltaTime, null);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - Configuration
        // ═══════════════════════════════════════════════════════════════

        public void SetMaxHunger(float max)
        {
            maxHunger = Mathf.Max(10f, max);
            currentHunger = Mathf.Min(currentHunger, maxHunger);
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - State Queries
        // ═══════════════════════════════════════════════════════════════

        public float GetCurrentHunger() => currentHunger;
        public float GetMaxHunger() => maxHunger;
        public float GetHungerPercentage() => maxHunger > 0 ? (currentHunger / maxHunger) : 0f;
        public bool IsStarving() => isStarving;
        public bool IsHungry(float threshold = 50f) => currentHunger < threshold;
    }
}

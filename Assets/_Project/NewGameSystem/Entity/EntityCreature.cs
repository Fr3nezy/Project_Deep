using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Universal concrete Entity implementation for all creatures.
    /// Replaces: TestFishEntity
    /// Configures all attached components from CreatureProfile on Awake.
    /// </summary>
    [RequireComponent(typeof(Entity))]
    public class EntityCreature : MonoBehaviour
    {
        [Header("Creature Configuration")]
        [Tooltip("ScriptableObject profile defining this creature's identity and stats")]
        [SerializeField] private CreatureProfile profile;

        // Cached references (auto-configured)
        private Entity entity;
        private HealthComponent health;
        private StaminaComponent stamina;
        private HungerComponent hunger;
        private AttackComponent attack;
        private MovementController movement;
        private NutritionComponent nutrition;
        private HuntComponent hunt;
        private FearComponent fear;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Cache entity reference
            entity = GetComponent<Entity>();

            // Validate profile
            if (profile == null)
            {
                Debug.LogError($"[EntityCreature] {gameObject.name} has no CreatureProfile assigned!", this);
                return;
            }

            // Cache component references
            CacheComponents();

            // Auto-configure all components from profile
            ConfigureFromProfile();
        }

        private void CacheComponents()
        {
            health = GetComponent<HealthComponent>();
            stamina = GetComponent<StaminaComponent>();
            hunger = GetComponent<HungerComponent>();
            attack = GetComponent<AttackComponent>();
            movement = GetComponent<MovementController>();
            nutrition = GetComponent<NutritionComponent>();
            hunt = GetComponent<HuntComponent>();
            fear = GetComponent<FearComponent>();
        }

        private void ConfigureFromProfile()
        {
            // Configure HealthComponent
            if (health != null)
            {
                health.SetMaxHealth(profile.maxHealth);
            }

            // Configure StaminaComponent
            if (stamina != null)
            {
                stamina.SetMaxStamina(profile.maxStamina);
            }

            // Configure HungerComponent
            if (hunger != null)
            {
                hunger.SetMaxHunger(profile.maxHunger);
            }

            // Configure AttackComponent
            if (attack != null)
            {
                attack.SetDamage(profile.attackDamage);
                attack.SetAttackRange(profile.attackRange);
                attack.SetAttackCooldown(profile.attackCooldown);
            }

            // Configure MovementController
            if (movement != null)
            {
                movement.SetMaxSpeed(profile.baseSpeed);
                movement.SetTurnSpeed(profile.turnSpeed);
            }

            // ══════════════════════════════════════════════════════════
            // NEW: Configure NutritionComponent from Profile
            // ══════════════════════════════════════════════════════════
            if (nutrition != null)
            {
                // Set nutrition value
                nutrition.SetNutritionValue(profile.nutritionValue);

                // Clear existing lists
                nutrition.preyTypes.Clear();
                nutrition.preyPriorities.Clear();
                nutrition.predatorTypes.Clear();

                // Configure prey types and priorities
                for (int i = 0; i < profile.preyTypes.Length; i++)
                {
                    EntityType preyType = profile.preyTypes[i];
                    nutrition.preyTypes.Add(preyType);

                    // Set priority if available
                    if (i < profile.preyPriorities.Length)
                    {
                        nutrition.SetPreyPriority(preyType, profile.preyPriorities[i]);
                    }
                    else
                    {
                        nutrition.SetPreyPriority(preyType, 5); // Default priority
                    }
                }

                // Configure predator types
                foreach (EntityType predatorType in profile.predatorTypes)
                {
                    nutrition.predatorTypes.Add(predatorType);
                }

                if (Application.isPlaying)
                {
                    Debug.Log($"[EntityCreature] {profile.creatureName} Nutrition configured: " +
                              $"Preys={nutrition.preyTypes.Count}, Predators={nutrition.predatorTypes.Count}, Value={profile.nutritionValue}");
                }
            }

            // ══════════════════════════════════════════════════════════
            // NEW: Configure HuntComponent from Profile
            // ══════════════════════════════════════════════════════════
            if (hunt != null && profile.isAggressive)
            {
                // Hunt component will use NutritionComponent for prey validation
                // No direct configuration needed, but we can enable/disable based on profile
                hunt.enabled = profile.isAggressive;
            }

            // ══════════════════════════════════════════════════════════
            // NEW: Configure FearComponent from Profile
            // ══════════════════════════════════════════════════════════
            if (fear != null)
            {
                // Fear component will use NutritionComponent for predator validation
                // Enable fear if creature has predators
                fear.enabled = profile.predatorTypes.Length > 0;
            }

            if (Application.isPlaying)
            {
                Debug.Log($"[EntityCreature] {profile.creatureName} configured: " +
                          $"HP={profile.maxHealth}, Speed={profile.baseSpeed}, " +
                          $"Preys on {profile.preyTypes.Length} types, Fears {profile.predatorTypes.Length} types");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - Identity & Profile Access
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Get this creature's profile (ScriptableObject).
        /// </summary>
        public CreatureProfile GetProfile()
        {
            return profile;
        }

        /// <summary>
        /// Get this creature's EntityType.
        /// </summary>
        public EntityType GetEntityType()
        {
            return profile != null ? profile.entityType : EntityType.Fish;
        }

        /// <summary>
        /// Get this creature's display name.
        /// </summary>
        public string GetCreatureName()
        {
            return profile != null ? profile.creatureName : "Unknown";
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - Food Chain Queries (Delegate to Profile)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Check if this creature preys on given entity type.
        /// </summary>
        public bool IsPrey(EntityType type)
        {
            return profile != null && profile.IsPrey(type);
        }

        /// <summary>
        /// Check if given entity type is a predator to this creature.
        /// </summary>
        public bool IsPredator(EntityType type)
        {
            return profile != null && profile.IsPredator(type);
        }

        /// <summary>
        /// Get hunting priority for given prey type.
        /// </summary>
        public int GetPreyPriority(EntityType type)
        {
            return profile != null ? profile.GetPreyPriority(type) : 0;
        }

        /// <summary>
        /// Get nutrition value if this creature is eaten.
        /// </summary>
        public float GetNutritionValue()
        {
            return profile != null ? profile.GetNutritionValue() : 0f;
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - Component Access (For Other Systems)
        // ═══════════════════════════════════════════════════════════════

        public Entity GetEntity() => entity;
        public HealthComponent GetHealth() => health;
        public StaminaComponent GetStamina() => stamina;
        public HungerComponent GetHunger() => hunger;
        public AttackComponent GetAttack() => attack;
        public MovementController GetMovement() => movement;
        public NutritionComponent GetNutrition() => nutrition;
        public HuntComponent GetHunt() => hunt;
        public FearComponent GetFear() => fear;

        // ═══════════════════════════════════════════════════════════════
        // EDITOR UTILITIES
        // ═══════════════════════════════════════════════════════════════

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-reconfigure when profile changes in Inspector
            if (Application.isPlaying && profile != null)
            {
                CacheComponents();
                ConfigureFromProfile();
            }
        }

        // Editor menu: Right-click EntityCreature → "Reconfigure From Profile"
        [ContextMenu("Reconfigure From Profile")]
        private void ReconfigureFromProfile()
        {
            CacheComponents();
            ConfigureFromProfile();
            Debug.Log($"[EntityCreature] {gameObject.name} reconfigured from profile!");
        }
        #endif
    }
}

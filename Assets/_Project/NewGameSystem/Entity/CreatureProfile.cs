using UnityEngine;
using System;

namespace GameSystem
{
    /// <summary>
    /// ScriptableObject profile defining a creature's identity, stats, and behavior.
    /// Replaces: EntityConfig, DietComponent data, FoodValue data
    /// Single source of truth for all creature configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "CreatureProfile", menuName = "Game/Creature Profile", order = 0)]
    public class CreatureProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique name for this creature type")]
        public string creatureName = "Unknown Creature";
        
        [Tooltip("Entity type enum - used for prey/predator identification")]
        public EntityType entityType = EntityType.Fish;
        
        [Tooltip("Optional description for designer reference")]
        [TextArea(3, 5)]
        public string description = "";

        // ═══════════════════════════════════════════════════════════════
        // FOOD CHAIN (merged from DietComponent + FoodValue)
        // ═══════════════════════════════════════════════════════════════
        
        [Header("Food Chain - What I Hunt")]
        [Tooltip("List of entity types this creature hunts/eats")]
        public EntityType[] preyTypes = new EntityType[0];
        
        [Tooltip("Priority values for each prey type (higher = preferred)")]
        public int[] preyPriorities = new int[0];

        [Header("Food Chain - What Hunts Me")]
        [Tooltip("List of entity types this creature fears/flees from")]
        public EntityType[] predatorTypes = new EntityType[0];

        [Header("Food Chain - My Nutrition Value")]
        [Tooltip("Nutrition value if this creature is eaten (food points)")]
        [Range(0, 500)]
        public float nutritionValue = 50f;

        // ═══════════════════════════════════════════════════════════════
        // BASE STATS
        // ═══════════════════════════════════════════════════════════════
        
        [Header("Base Stats")]
        [Tooltip("Maximum health points")]
        [Range(10, 1000)]
        public float maxHealth = 100f;
        
        [Tooltip("Maximum stamina points")]
        [Range(10, 500)]
        public float maxStamina = 100f;
        
        [Tooltip("Maximum hunger points (full = not hungry)")]
        [Range(10, 500)]
        public float maxHunger = 100f;

        [Header("Movement")]
        [Tooltip("Base swimming speed (units/second)")]
        [Range(0.5f, 20f)]
        public float baseSpeed = 2f;
        
        [Tooltip("Sprint speed multiplier (e.g., 1.5 = 150% speed)")]
        [Range(1f, 3f)]
        public float sprintMultiplier = 1.5f;
        
        [Tooltip("Turn speed (degrees/second)")]
        [Range(30f, 360f)]
        public float turnSpeed = 180f;

        [Header("Combat")]
        [Tooltip("Base attack damage")]
        [Range(0, 200)]
        public float attackDamage = 20f;
        
        [Tooltip("Attack range (distance)")]
        [Range(0.5f, 10f)]
        public float attackRange = 2f;
        
        [Tooltip("Cooldown between attacks (seconds)")]
        [Range(0.1f, 10f)]
        public float attackCooldown = 1.5f;

        // ═══════════════════════════════════════════════════════════════
        // BEHAVIOR FLAGS
        // ═══════════════════════════════════════════════════════════════
        
        [Header("Behavior Traits")]
        [Tooltip("Will actively hunt prey (requires HuntComponent)")]
        public bool isAggressive = false;
        
        [Tooltip("Defends territory around nest (requires TerritorialBehavior)")]
        public bool isTerritorial = false;
        
        [Tooltip("Can form schools/groups (requires SchoolComponent)")]
        public bool canSchool = false;
        
        [Tooltip("Prefers to stay near hiding spots")]
        public bool isTimid = true;

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - Food Chain Queries
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Check if given entity type is valid prey for this creature.
        /// </summary>
        public bool IsPrey(EntityType type)
        {
            return Array.Exists(preyTypes, prey => prey == type);
        }

        /// <summary>
        /// Check if given entity type is a predator/threat to this creature.
        /// </summary>
        public bool IsPredator(EntityType type)
        {
            return Array.Exists(predatorTypes, predator => predator == type);
        }

        /// <summary>
        /// Get hunting priority for given prey type (higher = more preferred).
        /// Returns 0 if not valid prey.
        /// </summary>
        public int GetPreyPriority(EntityType type)
        {
            int index = Array.IndexOf(preyTypes, type);
            if (index < 0 || index >= preyPriorities.Length)
                return 0;
            
            return preyPriorities[index];
        }

        /// <summary>
        /// Get nutrition value this creature provides when eaten.
        /// </summary>
        public float GetNutritionValue()
        {
            return nutritionValue;
        }

        // ═══════════════════════════════════════════════════════════════
        // EDITOR VALIDATION
        // ═══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-resize priority array to match prey types
            if (preyPriorities.Length != preyTypes.Length)
            {
                Array.Resize(ref preyPriorities, preyTypes.Length);
                
                // Default priority = 5 for new entries
                for (int i = 0; i < preyPriorities.Length; i++)
                {
                    if (preyPriorities[i] == 0)
                        preyPriorities[i] = 5;
                }
            }

            // Clamp values
            maxHealth = Mathf.Max(10f, maxHealth);
            maxStamina = Mathf.Max(10f, maxStamina);
            maxHunger = Mathf.Max(10f, maxHunger);
            baseSpeed = Mathf.Max(0.1f, baseSpeed);
            sprintMultiplier = Mathf.Max(1f, sprintMultiplier);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0.1f, attackCooldown);
        }
#endif
    }
}

using UnityEngine;
using System.Collections.Generic;

namespace GameSystem
{
    /// <summary>
    /// Defines nutrition value and food chain relationships (what hunts me, what I hunt).
    /// Replaces old DietComponent.
    /// </summary>
    public class NutritionComponent : MonoBehaviour
    {
        [Header("Food Chain - What I Hunt")]
        [Tooltip("Entity types this creature can hunt")]
        public List<EntityType> preyTypes = new List<EntityType>();
        
        [Tooltip("Priority for each prey type (0-10, higher = preferred)")]
        public Dictionary<EntityType, int> preyPriorities = new Dictionary<EntityType, int>();

        [Header("Food Chain - What Hunts Me")]
        [Tooltip("Entity types that hunt this creature")]
        public List<EntityType> predatorTypes = new List<EntityType>();

        [Header("My Nutrition Value")]
        [Tooltip("How much nutrition I provide when eaten")]
        [SerializeField] private float nutritionValue = 10f;

        private Entity selfEntity;

        private void Awake()
        {
            selfEntity = GetComponent<Entity>();
        }

        /// <summary>
        /// Check if target is valid prey for this entity.
        /// </summary>
        public bool IsValidPrey(Entity target)
        {
            if (target == null) return false;
            if (target == selfEntity) return false;
            if (target.IsDead()) return false;

            EntityType targetType = target.GetEntityType();
            return preyTypes.Contains(targetType);
        }

        /// <summary>
        /// Check if entity is a predator (hunts me).
        /// </summary>
        public bool IsPredator(Entity entity)
        {
            if (entity == null) return false;
            EntityType entityType = entity.GetEntityType();
            return predatorTypes.Contains(entityType);
        }

        /// <summary>
        /// Get hunting priority for specific prey (by Entity).
        /// </summary>
        public int GetPreyPriority(Entity prey)
        {
            if (prey == null) return 0;
            return GetPreyPriority(prey.GetEntityType());
        }

        /// <summary>
        /// Get hunting priority for specific prey type (by EntityType).
        /// </summary>
        public int GetPreyPriority(EntityType entityType)
        {
            if (preyPriorities.ContainsKey(entityType))
                return preyPriorities[entityType];
            return 5; // Default priority
        }

        /// <summary>
        /// Set hunting priority for prey type.
        /// </summary>
        public void SetPreyPriority(EntityType entityType, int priority)
        {
            preyPriorities[entityType] = Mathf.Clamp(priority, 0, 10);
        }

        /// <summary>
        /// Get nutrition value when eaten.
        /// </summary>
        public float GetNutritionValue()
        {
            return nutritionValue;
        }

        /// <summary>
        /// Set nutrition value.
        /// </summary>
        public void SetNutritionValue(float value)
        {
            nutritionValue = Mathf.Max(0f, value);
        }
    }
}

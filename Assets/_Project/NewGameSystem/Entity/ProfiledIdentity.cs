using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Universal concrete Entity implementation that works for all creatures.
    /// No need to create custom scripts per entity type - just assign a CreatureProfile!
    /// </summary>
    [RequireComponent(typeof(EntityCreature))]
    public class ProfiledEntity : Entity
    {
        private EntityCreature creature;

        protected override void Awake()
        {
            base.Awake();
            creature = GetComponent<EntityCreature>();

            if (creature == null)
            {
                Debug.LogError($"[ProfiledEntity] {gameObject.name} missing EntityCreature component!", this);
            }
        }

        public override EntityType GetEntityType()
        {
            if (creature != null && creature.GetProfile() != null)
            {
                return creature.GetProfile().entityType;
            }

            Debug.LogWarning($"[ProfiledEntity] {gameObject.name} has no profile assigned! Returning Unknown.");
            return EntityType.Unknown;
        }

        public override string GetEntityName()
        {
            if (creature != null && creature.GetProfile() != null)
            {
                return creature.GetProfile().creatureName;
            }

            return gameObject.name; // Fallback to GameObject name
        }
    }
}

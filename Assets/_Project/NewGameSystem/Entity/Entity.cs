using UnityEngine;

namespace GameSystem
{
    public enum EntityType
    {
        Unknown,
        Plankton,
        Fish,
        SmallFish,
        MediumFish,
        LargeFish,
        Leviathan,
        Shark,
        Player,
        NPC
    }

    public abstract class Entity : MonoBehaviour
    {
        [Header("Entity Identity")]
        [SerializeField] protected string entityName = "Entity";

        // Static event for global entity tracking
        public static event System.Action<Entity, Entity> OnAnyEntityDied; // (victim, killer)

        // State
        protected bool isAlive = true;

        // Component cache
        protected HealthComponent health;
        protected Transform cachedTransform;

        protected virtual void Awake()
        {
            cachedTransform = transform;
            health = GetComponent<HealthComponent>();
            
            // Register with EntityManager
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.RegisterEntity(this);
            }
        }

        protected virtual void OnDestroy()
        {
            // Unregister from EntityManager
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.UnregisterEntity(this);
            }
        }

        // Abstract methods (must be implemented by subclasses)
        public abstract EntityType GetEntityType();
        public abstract string GetEntityName();

        // Common methods
        public virtual Vector3 GetPosition() => cachedTransform.position;
        public virtual Transform GetTransform() => cachedTransform;
        public virtual bool IsAlive() => isAlive;
        public virtual bool IsDead() => !isAlive;

        public virtual void Die(Entity killer = null)
        {
            if (!isAlive)
                return;

            isAlive = false;
            
            // Trigger global event
            OnAnyEntityDied?.Invoke(this, killer);
            
            OnDeath(killer);
        }

        protected virtual void OnDeath(Entity killer)
        {
            // Override in subclasses for custom death behavior
        }
    }
}

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace GameSystem
{
    /// <summary>
    /// Handles attack and damage dealing for entities.
    /// </summary>
    public class AttackComponent : MonoBehaviour
    {
        [Header("Attack Settings")]
        [SerializeField] private float damage = 10f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackCooldown = 2f;

        [Header("Target Settings")]
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private bool requireLineOfSight = false;
        [SerializeField] private LayerMask obstacleLayers;

        [Header("Auto Attack")]
        [SerializeField] private bool autoAttackNearestTarget = false;
        [SerializeField] private float autoAttackCheckInterval = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        [SerializeField] private bool showDebugGizmos = true;

        // Events
        public UnityEvent<Entity> OnAttackExecuted;
        public UnityEvent<Entity> OnTargetKilled;
        public UnityEvent OnAttackOnCooldown;

        // State
        private float lastAttackTime = -999f;
        private Entity currentTarget = null;
        private float nextAutoAttackCheck = 0f;

        // Component cache
        private Entity selfEntity;
        private Transform cachedTransform;

        void Awake()
        {
            selfEntity = GetComponent<Entity>();
            cachedTransform = transform;

            if (selfEntity == null)
            {
                Debug.LogError($"[Attack] {gameObject.name} missing Entity component!");
            }
        }

        void Update()
        {
            if (autoAttackNearestTarget)
            {
                UpdateAutoAttack();
            }
        }

        private void UpdateAutoAttack()
        {
            if (Time.time < nextAutoAttackCheck) return;
            nextAutoAttackCheck = Time.time + autoAttackCheckInterval;

            Entity nearestTarget = FindNearestTarget();
            if (nearestTarget != null)
            {
                TryAttack(nearestTarget);
            }
        }

        /// <summary>
        /// Try to attack target entity.
        /// </summary>
        public void TryAttack(Entity target)
        {
            if (!IsAttackReady())
            {
                OnAttackOnCooldown?.Invoke();
                return;
            }

            if (target == null || target.IsDead())
                return;

            float distance = Vector3.Distance(cachedTransform.position, target.GetPosition());
            if (distance <= attackRange)
            {
                if (requireLineOfSight && !HasLineOfSight(target))
                    return;

                PerformAttack(target);
                lastAttackTime = Time.time;
            }
        }

        /// <summary>
        /// Try attack current target (no parameter).
        /// </summary>
        public bool TryAttack()
        {
            if (currentTarget == null)
            {
                currentTarget = FindNearestTarget();
            }

            if (currentTarget == null)
                return false;

            TryAttack(currentTarget);
            return true;
        }

        private void PerformAttack(Entity target)
        {
            currentTarget = target;

            if (showDebugLogs)
                Debug.Log($"⚔️ {selfEntity.GetEntityName()} attacks {target.GetEntityName()} for {damage} damage!");

            // Deal damage
            bool wasAlive = target.IsAlive();

            HealthComponent targetHealth = target.GetComponent<HealthComponent>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damage, selfEntity);
            }

            OnAttackExecuted?.Invoke(target);

            // Check if killed target
            if (wasAlive && target.IsDead())
            {
                OnTargetKilled?.Invoke(target);

                if (showDebugLogs)
                    Debug.Log($"💀 {selfEntity.GetEntityName()} killed {target.GetEntityName()}!");

                currentTarget = null;
            }
        }

        private Entity FindNearestTarget()
        {
            List<Entity> nearbyEntities = EntityManager.Instance.GetEntitiesInRadius(
                cachedTransform.position,
                attackRange,
                selfEntity
            );

            Entity nearest = null;
            float minDistance = attackRange;

            foreach (Entity entity in nearbyEntities)
            {
                if (entity == null || entity.IsDead())
                    continue;

                if (!IsValidTarget(entity))
                    continue;

                float distance = Vector3.Distance(cachedTransform.position, entity.GetPosition());
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = entity;
                }
            }

            return nearest;
        }

        protected virtual bool IsValidTarget(Entity target)
        {
            if (target == null || target.IsDead())
                return false;

            if (target == selfEntity)
                return false;

            return true;
        }

        private bool HasLineOfSight(Entity target)
        {
            if (target == null)
                return false;

            Vector3 direction = target.GetPosition() - cachedTransform.position;
            float distance = direction.magnitude;

            RaycastHit hit;
            if (Physics.Raycast(cachedTransform.position, direction.normalized, out hit, distance, obstacleLayers))
            {
                return false;
            }

            return true;
        }

        // PUBLIC API
        public bool IsAttackReady()
        {
            return Time.time >= lastAttackTime + attackCooldown;
        }

        public float GetCooldownRemaining()
        {
            float remaining = (lastAttackTime + attackCooldown) - Time.time;
            return Mathf.Max(remaining, 0f);
        }

        public void SetTarget(Entity target) => currentTarget = target;
        public void ClearTarget() => currentTarget = null;
        public void ResetCooldown() => lastAttackTime = -999f;

        // Setters (for configuration)
        public void SetDamage(float dmg) => damage = Mathf.Max(0f, dmg);
        public void SetAttackRange(float range) => attackRange = Mathf.Max(0.1f, range);
        public void SetAttackCooldown(float cooldown) => attackCooldown = Mathf.Max(0.1f, cooldown);

        // Getters
        public float GetDamage() => damage;
        public float GetAttackRange() => attackRange;
        public float GetCooldown() => attackCooldown;
        public Entity GetCurrentTarget() => currentTarget;
        public bool HasTarget() => currentTarget != null && currentTarget.IsAlive();

        // DEBUG GIZMOS
        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying)
                return;

            // Attack range
            Gizmos.color = IsAttackReady() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Line to target
            if (currentTarget != null && currentTarget.IsAlive())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, currentTarget.GetPosition());
            }
        }
    }
}

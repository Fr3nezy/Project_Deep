using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace GameSystem
{
    public enum HuntState
    {
        Idle,
        Spotting,
        Stalking,
        Chasing,
        Attacking
    }

    public class HuntComponent : MonoBehaviour
    {
        [Header("Detection FOV")]
        [SerializeField] protected float detectionRange = 10f;
        [SerializeField] protected float detectionAngle = 120f;

        [Header("Chase FOV")]
        [SerializeField] private float chaseRange = 6f;
        [SerializeField] private float chaseAngle = 90f;

        [Header("Behavior Timing")]
        [SerializeField] private float spottingDuration = 3f;
        [SerializeField] private float loseTargetTime = 5f;

        [Header("Speed Settings")]
        [SerializeField] private float stalkSpeed = 1.5f;
        [SerializeField] private float chaseSpeed = 3.0f;

        [Header("Hunt Requirements")]
        [SerializeField] private bool requireHungerToHunt = true;
        [SerializeField] private float minHungerToHunt = 40f;

        [Header("Performance Settings")]
        [SerializeField] private float scanInterval = 0.5f;
        [SerializeField] private int movementUpdateFrequency = 1;

        [Header("Debug")]
        [SerializeField] protected bool showDebugLogs = false;
        [SerializeField] protected bool showDebugGizmos = true;

        // Events
        public UnityEvent<Entity> OnTargetSpotted;
        public UnityEvent<Entity> OnTargetLocked;
        public UnityEvent<Entity> OnTargetLost;
        public UnityEvent<Entity> OnTargetKilled;

        // State
        protected HuntState currentState = HuntState.Idle;
        protected Entity currentTarget = null;
        private float spottingProgress = 0f;
        private float lastTargetSeenTime = 0f;
        private float nextScanTime = 0f;
        private int frameCounter = 0;

        // Cache
        private Entity cachedBestPrey = null;
        private float cacheValidUntil = 0f;
        private const float CACHE_DURATION = 0.2f;

        private float detectionRangeSqr;
        private float chaseRangeSqr;
        private float halfDetectionAngle;
        private float halfChaseAngle;

        // Components
        protected Entity selfEntity;
        private HungerComponent hunger;
        protected StaminaComponent stamina;
        protected AttackComponent attack;
        protected NutritionComponent nutrition;
        protected Transform cachedTransform;

        void Awake()
        {
            CacheComponents();
            PreCalculateValues();
        }

        private void CacheComponents()
        {
            selfEntity = GetComponent<Entity>();
            hunger = GetComponent<HungerComponent>();
            stamina = GetComponent<StaminaComponent>();
            attack = GetComponent<AttackComponent>();
            nutrition = GetComponent<NutritionComponent>();
            cachedTransform = transform;

            if (nutrition == null && showDebugLogs)
            {
                Debug.LogWarning($"[Hunt] {gameObject.name} missing NutritionComponent - will hunt ANY entity!");
            }
        }

        private void PreCalculateValues()
        {
            detectionRangeSqr = detectionRange * detectionRange;
            chaseRangeSqr = chaseRange * chaseRange;
            halfDetectionAngle = detectionAngle * 0.5f;
            halfChaseAngle = chaseAngle * 0.5f;
            nextScanTime = Time.time + Random.Range(0f, scanInterval);
        }

        void Update()
        {
            if (selfEntity == null || selfEntity.IsDead())
                return;

            frameCounter++;
            if (frameCounter >= movementUpdateFrequency)
            {
                frameCounter = 0;
                UpdateHuntBehavior();
            }
        }

        private void UpdateHuntBehavior()
        {
            if (!CanHunt())
            {
                if (currentState != HuntState.Idle)
                {
                    StopHunting("Not hungry enough");
                }
                return;
            }

            switch (currentState)
            {
                case HuntState.Idle:
                    UpdateIdle();
                    break;
                case HuntState.Spotting:
                    UpdateSpotting();
                    break;
                case HuntState.Stalking:
                    UpdateStalking();
                    break;
                case HuntState.Chasing:
                    UpdateChasing();
                    break;
                case HuntState.Attacking:
                    UpdateAttacking();
                    break;
            }
        }

        protected virtual bool CanHunt()
        {
            if (!requireHungerToHunt) return true;
            if (hunger == null) return true;
            return hunger.GetCurrentHunger() <= minHungerToHunt;
        }

        private void UpdateIdle()
        {
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + scanInterval + Random.Range(-0.1f, 0.1f);
            ScanForPrey();
        }

        private void UpdateSpotting()
        {
            if (currentTarget == null || currentTarget.IsDead())
            {
                StopHunting("Target died during spotting");
                return;
            }

            if (IsInDetectionFOV(currentTarget))
            {
                lastTargetSeenTime = Time.time;
                spottingProgress += Time.deltaTime / spottingDuration;
                if (spottingProgress >= 1f)
                {
                    EnterStalking();
                }
            }
            else
            {
                if (Time.time - lastTargetSeenTime > loseTargetTime)
                {
                    LoseTarget("Lost sight during spotting");
                }
            }
        }

        private void UpdateStalking()
        {
            if (currentTarget == null || currentTarget.IsDead())
            {
                StopHunting("Target died during stalking");
                return;
            }

            float distanceSqr = (cachedTransform.position - currentTarget.GetPosition()).sqrMagnitude;

            if (distanceSqr <= chaseRangeSqr && IsInChaseFOV(currentTarget))
            {
                EnterChasing();
                return;
            }

            if (IsInDetectionFOV(currentTarget))
            {
                lastTargetSeenTime = Time.time;
            }
            else
            {
                if (Time.time - lastTargetSeenTime > loseTargetTime)
                {
                    LoseTarget("Lost target during stalking");
                }
            }
        }

        private void UpdateChasing()
        {
            if (currentTarget == null || currentTarget.IsDead())
            {
                StopHunting("Target died during chase");
                return;
            }

            float distanceSqr = (cachedTransform.position - currentTarget.GetPosition()).sqrMagnitude;

            if (attack != null && distanceSqr <= attack.GetAttackRange() * attack.GetAttackRange())
            {
                EnterAttacking();
                return;
            }

            if (distanceSqr > chaseRangeSqr || !IsInChaseFOV(currentTarget))
            {
                currentState = HuntState.Stalking;
                if (stamina != null) stamina.StopSprint();
                return;
            }

            lastTargetSeenTime = Time.time;
        }

        private void UpdateAttacking()
        {
            if (currentTarget == null || currentTarget.IsDead())
            {
                StopHunting("Target died before attack");
                return;
            }

            float distanceSqr = (cachedTransform.position - currentTarget.GetPosition()).sqrMagnitude;
            float attackRangeSqr = attack.GetAttackRange() * attack.GetAttackRange();

            if (attack != null && attack.IsAttackReady() && distanceSqr <= attackRangeSqr)
            {
                attack.TryAttack(currentTarget);
            }

            if (distanceSqr > attackRangeSqr * 2.25f)
            {
                EnterChasing();
            }
        }

        private void ScanForPrey()
        {
            if (Time.time < cacheValidUntil && cachedBestPrey != null && cachedBestPrey.IsAlive())
            {
                if (IsInDetectionFOV(cachedBestPrey))
                {
                    EnterSpotting(cachedBestPrey);
                    return;
                }
            }

            Entity bestPrey = FindBestPrey();
            cachedBestPrey = bestPrey;
            cacheValidUntil = Time.time + CACHE_DURATION;

            if (bestPrey != null)
            {
                EnterSpotting(bestPrey);
            }
        }

        protected virtual Entity FindBestPrey()
        {
            List<Entity> nearbyEntities = EntityManager.Instance.GetEntitiesInRadius(
                cachedTransform.position,
                detectionRange,
                selfEntity
            );

            Entity bestPrey = null;
            float minDistance = detectionRangeSqr;
            int highestPriority = -1;

            foreach (Entity potentialPrey in nearbyEntities)
            {
                if (nutrition != null && !nutrition.IsValidPrey(potentialPrey))
                    continue;

                if (potentialPrey == selfEntity)
                    continue;

                if (!IsInDetectionFOV(potentialPrey))
                    continue;

                float distanceSqr = (cachedTransform.position - potentialPrey.GetPosition()).sqrMagnitude;

                int priority = 5;
                if (nutrition != null)
                {
                    priority = nutrition.GetPreyPriority(potentialPrey);
                }

                if (priority > highestPriority || (priority == highestPriority && distanceSqr < minDistance))
                {
                    minDistance = distanceSqr;
                    highestPriority = priority;
                    bestPrey = potentialPrey;
                }
            }

            return bestPrey;
        }

        protected bool IsInDetectionFOV(Entity target)
        {
            Vector3 dirToTarget = target.GetPosition() - cachedTransform.position;
            float distanceSqr = dirToTarget.sqrMagnitude;

            if (distanceSqr > detectionRangeSqr) return false;

            dirToTarget.Normalize();
            float angle = Vector3.Angle(cachedTransform.forward, dirToTarget);
            return angle <= halfDetectionAngle;
        }

        private bool IsInChaseFOV(Entity target)
        {
            Vector3 dirToTarget = target.GetPosition() - cachedTransform.position;
            float distanceSqr = dirToTarget.sqrMagnitude;

            if (distanceSqr > chaseRangeSqr) return false;

            dirToTarget.Normalize();
            float angle = Vector3.Angle(cachedTransform.forward, dirToTarget);
            return angle <= halfChaseAngle;
        }

        protected void EnterSpotting(Entity target)
        {
            currentState = HuntState.Spotting;
            currentTarget = target;
            spottingProgress = 0f;
            lastTargetSeenTime = Time.time;

            if (showDebugLogs)
                Debug.Log($"👁️ {gameObject.name} SPOTTING {target.GetEntityName()}");

            OnTargetSpotted?.Invoke(target);
        }

        private void EnterStalking()
        {
            currentState = HuntState.Stalking;
            if (showDebugLogs)
                Debug.Log($"🚶 {gameObject.name} STALKING {currentTarget.GetEntityName()}");
            OnTargetLocked?.Invoke(currentTarget);
        }

        protected void EnterChasing()
        {
            currentState = HuntState.Chasing;
            if (showDebugLogs)
                Debug.Log($"🏃 {gameObject.name} CHASING {currentTarget.GetEntityName()}!");
            if (stamina != null) stamina.StartSprint();
        }

        private void EnterAttacking()
        {
            currentState = HuntState.Attacking;
            if (showDebugLogs)
                Debug.Log($"⚔️ {gameObject.name} ATTACKING {currentTarget.GetEntityName()}!");
            if (stamina != null) stamina.StopSprint();
        }

        private void LoseTarget(string reason = "")
        {
            if (showDebugLogs)
                Debug.Log($"❌ {gameObject.name} lost target {currentTarget?.GetEntityName()} ({reason})");
            OnTargetLost?.Invoke(currentTarget);
            StopHunting(reason);
        }

        protected void StopHunting(string reason = "")
        {
            currentState = HuntState.Idle;
            currentTarget = null;
            spottingProgress = 0f;
            cachedBestPrey = null;
            cacheValidUntil = 0f;
            if (stamina != null) stamina.StopSprint();
        }

        // PUBLIC API
        public HuntState GetHuntState() => currentState;
        public Entity GetCurrentTarget() => currentTarget;
        public bool IsHunting() => currentState != HuntState.Idle;
        public bool IsChasing() => currentState == HuntState.Chasing;
        public bool IsStalking() => currentState == HuntState.Stalking;
        public float GetChaseSpeed() => chaseSpeed;
        public float GetStalkSpeed() => stalkSpeed;

        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            DrawFOVCone(detectionRange, detectionAngle);

            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            DrawFOVCone(chaseRange, chaseAngle);

            if (currentTarget != null && currentTarget.IsAlive())
            {
                Gizmos.color = currentState switch
                {
                    HuntState.Spotting => Color.yellow,
                    HuntState.Stalking => Color.green,
                    HuntState.Chasing => Color.red,
                    HuntState.Attacking => Color.magenta,
                    _ => Color.white
                };
                Gizmos.DrawLine(transform.position, currentTarget.GetPosition());
            }
        }

        private void DrawFOVCone(float range, float angle)
        {
            int segments = 20;
            float halfAngle = angle / 2f;
            Vector3 forward = transform.forward * range;

            for (int i = 0; i <= segments; i++)
            {
                float currentAngle = -halfAngle + (angle * i / segments);
                Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * forward;
                Gizmos.DrawLine(transform.position, transform.position + dir);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace GameSystem
{
    public class FearComponent : MonoBehaviour
    {
        [Header("Fear Settings")]
        [SerializeField] private float maxFearLevel = 100f;
        [SerializeField] private float fearFleeThreshold = 50f;
        [SerializeField] private float fearDecayRate = 10f;
        [SerializeField] private float fearBuildupRate = 20f;

        [Header("Threat Detection")]
        [SerializeField] private float predatorDetectionRadius = 10f;
        [SerializeField] private float threatScanInterval = 0.3f;

        [Header("Flee Behavior")]
        [SerializeField] private float fleeSpeedMultiplier = 1.5f;
        [SerializeField] private float fleeMinDistance = 15f;
        [SerializeField] private float fleeStaminaCost = 20f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        [SerializeField] private bool showDebugGizmos = true;

        // State
        private float currentFearLevel = 0f;
        private bool isFleeing = false;
        private Entity primaryThreat = null;
        private List<Entity> detectedThreats = new List<Entity>(16);

        // Performance
        private float nextThreatScan = 0f;
        private float detectionRadiusSqr;
        private float fleeMinDistanceSqr;

        // Components
        private Entity selfEntity;
        private StaminaComponent stamina;
        private MovementController movement;
        private NutritionComponent nutrition;
        private Transform cachedTransform;

        // Events
        public UnityEvent OnFleeStarted;
        public UnityEvent OnFleeStopped;
        public UnityEvent<float> OnFearLevelChanged;

        void Awake()
        {
            CacheComponents();
            PreCalculateValues();
        }

        private void CacheComponents()
        {
            selfEntity = GetComponent<Entity>();
            stamina = GetComponent<StaminaComponent>();
            movement = GetComponent<MovementController>();
            nutrition = GetComponent<NutritionComponent>();
            cachedTransform = transform;

            if (nutrition == null && showDebugLogs)
            {
                Debug.LogWarning($"[Fear] {gameObject.name} missing NutritionComponent - can't detect predators!");
            }
        }

        private void PreCalculateValues()
        {
            detectionRadiusSqr = predatorDetectionRadius * predatorDetectionRadius;
            fleeMinDistanceSqr = fleeMinDistance * fleeMinDistance;
            nextThreatScan = Time.time + Random.Range(0f, threatScanInterval);
        }

        void Update()
        {
            if (selfEntity == null || selfEntity.IsDead())
                return;

            if (nutrition == null)
                return;

            if (Time.time >= nextThreatScan)
            {
                DetectThreats();
                nextThreatScan = Time.time + threatScanInterval + Random.Range(-0.05f, 0.05f);
            }

            if (detectedThreats.Count > 0)
            {
                IncreaseFear(fearBuildupRate * Time.deltaTime * detectedThreats.Count);
            }
            else
            {
                DecreaseFear(fearDecayRate * Time.deltaTime);
            }

            if (!isFleeing && currentFearLevel >= fearFleeThreshold)
            {
                StartFleeing();
            }
            else if (isFleeing && currentFearLevel < fearFleeThreshold * 0.5f)
            {
                StopFleeing();
            }

            if (isFleeing)
            {
                UpdateFleeMovement();
            }
        }

        private void DetectThreats()
        {
            detectedThreats.Clear();
            primaryThreat = null;

            if (nutrition == null) return;

            List<Entity> nearbyEntities = EntityManager.Instance.GetEntitiesInRadius(
                cachedTransform.position,
                predatorDetectionRadius,
                selfEntity
            );

            float closestDistance = float.MaxValue;

            foreach (Entity potentialThreat in nearbyEntities)
            {
                if (!nutrition.IsPredator(potentialThreat))
                    continue;

                float distanceSqr = (potentialThreat.GetPosition() - cachedTransform.position).sqrMagnitude;

                if (distanceSqr <= detectionRadiusSqr)
                {
                    detectedThreats.Add(potentialThreat);

                    if (distanceSqr < closestDistance)
                    {
                        closestDistance = distanceSqr;
                        primaryThreat = potentialThreat;
                    }
                }
            }
        }

        private void StartFleeing()
        {
            isFleeing = true;
            if (showDebugLogs)
                Debug.Log($"😱 {selfEntity.GetEntityName()} is FLEEING from {primaryThreat?.GetEntityName()}!");

            OnFleeStarted?.Invoke();

            if (stamina != null)
            {
                stamina.StartSprint();
            }
        }

        private void StopFleeing()
        {
            isFleeing = false;
            if (showDebugLogs)
                Debug.Log($"😌 {selfEntity.GetEntityName()} stopped fleeing");

            OnFleeStopped?.Invoke();

            if (stamina != null)
            {
                stamina.StopSprint();
            }
        }

        private void UpdateFleeMovement()
        {
            if (primaryThreat == null) return;

            float distanceSqr = (cachedTransform.position - primaryThreat.GetPosition()).sqrMagnitude;

            if (distanceSqr >= fleeMinDistanceSqr && currentFearLevel < fearFleeThreshold)
            {
                StopFleeing();
            }

            if (stamina != null && !stamina.IsExhausted())
            {
                stamina.ConsumeStamina(fleeStaminaCost * Time.deltaTime);
            }
        }

        private void IncreaseFear(float amount)
        {
            float oldFear = currentFearLevel;
            currentFearLevel = Mathf.Min(currentFearLevel + amount, maxFearLevel);

            if (currentFearLevel != oldFear)
            {
                OnFearLevelChanged?.Invoke(currentFearLevel);
            }
        }

        private void DecreaseFear(float amount)
        {
            float oldFear = currentFearLevel;
            currentFearLevel = Mathf.Max(currentFearLevel - amount, 0f);

            if (currentFearLevel != oldFear)
            {
                OnFearLevelChanged?.Invoke(currentFearLevel);
            }
        }

        // PUBLIC API
        public float GetCurrentFearLevel() => currentFearLevel;
        public bool IsFleeing() => isFleeing;
        public Entity GetCurrentThreat() => primaryThreat;
        public float GetFleeSpeedMultiplier() => fleeSpeedMultiplier;

        public Vector3 GetFleeDirection()
        {
            if (primaryThreat != null)
                return (cachedTransform.position - primaryThreat.GetPosition()).normalized;
            return cachedTransform.forward;
        }

        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;

            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawSphere(transform.position, predatorDetectionRadius);

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, predatorDetectionRadius);

            if (isFleeing && primaryThreat != null)
            {
                Gizmos.color = Color.magenta;
                Vector3 fleeTarget = transform.position + GetFleeDirection() * fleeMinDistance;
                Gizmos.DrawLine(transform.position, fleeTarget);
                Gizmos.DrawWireSphere(fleeTarget, 1f);
            }

            foreach (Entity threat in detectedThreats)
            {
                if (threat == null) continue;
                Gizmos.color = threat == primaryThreat ? Color.red : new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(transform.position, threat.GetPosition());
            }
        }
    }
}

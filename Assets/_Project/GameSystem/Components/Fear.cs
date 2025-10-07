using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// High-performance Fear behavior per entity AI with complete API.
/// </summary>
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

    // Performance optimization
    private float nextThreatScan = 0f;
    private float detectionRadiusSqr;
    private float fleeMinDistanceSqr;

    // Component cache
    private Entity selfEntity;
    private StaminaComponent stamina;
    private MovementController movement;
    private DietComponent diet;
    private Transform cachedTransform;

    // Events
    public UnityEvent OnFleeStarted;
    public UnityEvent OnFleeStopped;
    public UnityEvent<float> OnFearLevelChanged;

    #region Initialization

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
        diet = GetComponent<DietComponent>();
        cachedTransform = transform;
    }

    private void PreCalculateValues()
    {
        detectionRadiusSqr = predatorDetectionRadius * predatorDetectionRadius;
        fleeMinDistanceSqr = fleeMinDistance * fleeMinDistance;
        
        // Randomize initial scan time
        nextThreatScan = Time.time + Random.Range(0f, threatScanInterval);
    }

    #endregion

    #region Update Loop

    void Update()
    {
        if (diet == null || selfEntity == null || selfEntity.IsDead()) return;

        // Staggered threat scanning
        if (Time.time >= nextThreatScan)
        {
            DetectThreats();
            nextThreatScan = Time.time + threatScanInterval + Random.Range(-0.05f, 0.05f);
        }

        // Update fear level
        if (detectedThreats.Count > 0)
        {
            IncreaseFear(fearBuildupRate * Time.deltaTime * detectedThreats.Count);
        }
        else
        {
            DecreaseFear(fearDecayRate * Time.deltaTime);
        }

        // Flee state transitions
        if (!isFleeing && currentFearLevel >= fearFleeThreshold)
        {
            StartFleeing();
        }
        else if (isFleeing && currentFearLevel < fearFleeThreshold * 0.5f)
        {
            StopFleeing();
        }

        // Update flee behavior
        if (isFleeing)
        {
            UpdateFleeMovement();
        }
    }

    #endregion

    #region Threat Detection (Optimized)

    /// <summary>
    /// Ultra-fast threat detection using EntityManager spatial queries
    /// </summary>
    private void DetectThreats()
    {
        detectedThreats.Clear();
        primaryThreat = null;

        if (diet == null) return;

        // OPTIMIZATION: Spatial query instead of FindObjectsByType
        List<Entity> nearbyEntities = EntityManager.Instance.GetEntitiesInRadius(
            cachedTransform.position, 
            predatorDetectionRadius, 
            selfEntity
        );

        float closestDistance = float.MaxValue;

        foreach (Entity potentialThreat in nearbyEntities)
        {
            // Check if it's actually a predator
            if (!diet.IsPredator(potentialThreat)) continue;

            float distanceSqr = (potentialThreat.transform.position - cachedTransform.position).sqrMagnitude;

            if (distanceSqr <= detectionRadiusSqr)
            {
                detectedThreats.Add(potentialThreat);

                // Track closest as primary threat
                if (distanceSqr < closestDistance)
                {
                    closestDistance = distanceSqr;
                    primaryThreat = potentialThreat;
                }
            }
        }
    }

    #endregion

    #region Fear Behavior

    private void StartFleeing()
    {
        isFleeing = true;

        if (showDebugLogs)
            Debug.Log($"😱 {selfEntity.GetEntityName()} is FLEEING from {primaryThreat?.GetEntityName()}!");

        OnFleeStarted?.Invoke();

        // Start consuming stamina
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

        // Calculate flee direction (away from threat)
        Vector3 fleeDirection = (cachedTransform.position - primaryThreat.transform.position).normalized;

        // Check if far enough away
        float distanceSqr = (cachedTransform.position - primaryThreat.transform.position).sqrMagnitude;

        if (distanceSqr >= fleeMinDistanceSqr && currentFearLevel < fearFleeThreshold)
        {
            StopFleeing();
        }

        // Consume stamina during flee
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

    #endregion

    #region Public API (For Other Components)

    // Manual fear control
    public void ForceFear(float amount)
    {
        IncreaseFear(amount);
    }

    public void ResetFear()
    {
        currentFearLevel = 0f;
        StopFleeing();
        OnFearLevelChanged?.Invoke(currentFearLevel);
    }

    // Setters per EntityConfig
    public void SetFearThreshold(float threshold) => fearFleeThreshold = threshold;
    public void SetPredatorDetectionRadius(float radius) 
    { 
        predatorDetectionRadius = radius;
        detectionRadiusSqr = radius * radius;
    }
    public void SetFleeSpeedMultiplier(float multiplier) => fleeSpeedMultiplier = multiplier;
    public void SetFleeMinDistance(float distance) 
    { 
        fleeMinDistance = distance;
        fleeMinDistanceSqr = distance * distance;
    }

    // Getters (QUESTI ERANO MANCANTI!)
    public float GetCurrentFearLevel() => currentFearLevel;
    public float GetMaxFearLevel() => maxFearLevel;
    public bool IsFleeing() => isFleeing;
    public Entity GetPrimaryThreat() => primaryThreat;
    public float GetFleeSpeedMultiplier() => fleeSpeedMultiplier;

    public Vector3 GetFleeDirection()
    {
        if (primaryThreat != null)
            return (cachedTransform.position - primaryThreat.transform.position).normalized;
        return cachedTransform.forward;
    }

    public Vector3 GetFleeTarget()
    {
        return cachedTransform.position + GetFleeDirection() * fleeMinDistance;
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmos()
    {
        #if UNITY_EDITOR
        if (!showDebugGizmos) return;

        // Predator detection radius (red transparent sphere)
        if (Application.isPlaying)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.05f);
            Gizmos.DrawSphere(transform.position, predatorDetectionRadius);

            // Wire outline
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, predatorDetectionRadius);
        }

        if (!Application.isPlaying) return;

        // Flee direction arrow
        if (isFleeing && primaryThreat != null)
        {
            Gizmos.color = new Color(0.5f, 0f, 1f);
            Vector3 fleeTarget = GetFleeTarget();
            Gizmos.DrawLine(transform.position, fleeTarget);
            Gizmos.DrawWireSphere(fleeTarget, 1f);

            // Arrow direction
            Vector3 arrowDir = GetFleeDirection();
            Gizmos.DrawRay(transform.position, arrowDir * 3f);
        }

        // Lines to detected threats
        foreach (Entity threat in detectedThreats)
        {
            if (threat == null) continue;

            Gizmos.color = threat == primaryThreat ? new Color(0.5f, 0f, 1f) : new Color(0.7f, 0.3f, 1f);
            Gizmos.DrawLine(transform.position, threat.transform.position);
        }
        #endif
    }

    #endregion
}

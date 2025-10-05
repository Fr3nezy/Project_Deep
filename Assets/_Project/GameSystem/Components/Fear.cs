using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Fear behavior per entity AI. Diet-based predator detection.
/// </summary>
public class FearComponent : MonoBehaviour
{
    [Header("Fear Settings")]
    [SerializeField] private float maxFearLevel = 100f;
    [SerializeField] private float fearFleeThreshold = 50f;
    [SerializeField] private float fearDecayRate = 10f;
    [SerializeField] private float fearBuildupRate = 20f;
    
    [Header("Threat Detection (DEPRECATED - Usa DietComponent!)")]
    [SerializeField] private float playerFearRadius = 2.5f;
    [SerializeField] private float leviathanFearRadius = 10f;
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
    private List<Entity> detectedThreats = new List<Entity>();
    private float nextThreatScan = 0f;
    
    // Events
    public UnityEvent OnFleeStarted;
    public UnityEvent OnFleeStopped;
    public UnityEvent<float> OnFearLevelChanged;
    
    // Component cache
    private Entity selfEntity;
    private StaminaComponent stamina;
    private MovementController movement;
    private DietComponent diet; // ← NUOVO!
    
    void Awake()
    {
        selfEntity = GetComponent<Entity>();
        stamina = GetComponent<StaminaComponent>();
        movement = GetComponent<MovementController>();
        diet = GetComponent<DietComponent>(); // ← NUOVO!
    }
    
    void Update()
    {
        UpdateFearBehavior();
    }
    
    private void UpdateFearBehavior()
    {
        // Check se DietComponent presente
        if (diet == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[Fear] {gameObject.name}: No DietComponent! Cannot detect predators.");
            return;
        }
        
        // Scan per threats
        if (Time.time >= nextThreatScan)
        {
            DetectThreats();
            nextThreatScan = Time.time + threatScanInterval;
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
        
        // Check flee threshold
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
    
    private void DetectThreats()
    {
        detectedThreats.Clear();
        primaryThreat = null;
        
        if (diet == null) return;
        
        // Trova tutte entity
        Entity[] allEntities = FindObjectsOfType<Entity>();
        
        float closestDistance = float.MaxValue;
        
        foreach (Entity potentialThreat in allEntities)
        {
            if (potentialThreat == selfEntity) continue;
            if (potentialThreat.IsDead()) continue;
            
            // USA DIET COMPONENT per verificare se è predatore!
            if (!diet.IsPredator(potentialThreat)) continue;
            
            float distance = Vector3.Distance(transform.position, potentialThreat.transform.position);
            
            // Radius dinamico basato su tipo (TODO: migliorabile)
            float fearRadius = GetFearRadiusForPredator(potentialThreat.GetEntityType());
            
            if (distance <= fearRadius)
            {
                detectedThreats.Add(potentialThreat);
                
                // Track closest come primary threat
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    primaryThreat = potentialThreat;
                }
            }
        }
    }
    
    private float GetFearRadiusForPredator(EntityType predatorType)
    {
        // TODO: Questo potrebbe essere configurabile in DietComponent!
        return predatorType switch
        {
            EntityType.Leviathan => leviathanFearRadius,
            EntityType.Player => playerFearRadius,
            EntityType.LargeFish => 8f,
            EntityType.MediumFish => 5f,
            _ => 5f
        };
    }
    
    private void StartFleeing()
    {
        isFleeing = true;
        
        if (showDebugLogs)
            Debug.Log($"😱 {gameObject.name} is FLEEING from {primaryThreat?.GetEntityName()}!");
        
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
            Debug.Log($"😌 {gameObject.name} stopped fleeing");
        
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
        Vector3 fleeDirection = (transform.position - primaryThreat.transform.position).normalized;
        
        // Check se abbastanza lontano
        float distance = Vector3.Distance(transform.position, primaryThreat.transform.position);
        
        if (distance >= fleeMinDistance && currentFearLevel < fearFleeThreshold)
        {
            StopFleeing();
        }
        
        // Consume stamina
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
    
    public void ForceFear(float amount)
    {
        IncreaseFear(amount);
    }
    
    public void ResetFear()
    {
        currentFearLevel = 0f;
        StopFleeing();
    }
    
    // Setters per EntityConfig
    public void SetFearThreshold(float threshold) => fearFleeThreshold = threshold;
    
    // Getters
    public float GetCurrentFearLevel() => currentFearLevel;
    public float GetMaxFearLevel() => maxFearLevel;
    public bool IsFleeing() => isFleeing;
    public Entity GetPrimaryThreat() => primaryThreat;
    public Vector3 GetFleeDirection()
    {
        if (primaryThreat != null)
            return (transform.position - primaryThreat.transform.position).normalized;
        return transform.forward;
    }
    public Vector3 GetFleeTarget()
    {
        return transform.position + GetFleeDirection() * fleeMinDistance;
    }
    
    #region Debug Visualization
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Detection radii (solo in editor, per reference)
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, playerFearRadius);
        
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, leviathanFearRadius);
        
        if (!Application.isPlaying) return;
        
        // Fear bar (viola)
        Vector3 barPos = transform.position + Vector3.up * 1.6f;
        float barWidth = 1f;
        float barHeight = 0.1f;
        
        Gizmos.color = new Color(0.3f, 0.3f, 0.3f);
        Gizmos.DrawCube(barPos, new Vector3(barWidth, barHeight, 0.01f));
        
        Color fearBarColor;
        if (currentFearLevel >= fearFleeThreshold)
            fearBarColor = new Color(0.5f, 0f, 1f);
        else if (currentFearLevel >= fearFleeThreshold * 0.5f)
            fearBarColor = new Color(0.7f, 0.3f, 1f);
        else
            fearBarColor = new Color(0.9f, 0.7f, 1f);
        
        Gizmos.color = fearBarColor;
        float fearPercent = currentFearLevel / maxFearLevel;
        Vector3 fearBarFillPos = barPos - new Vector3(barWidth * (1 - fearPercent) * 0.5f, 0, 0);
        Gizmos.DrawCube(fearBarFillPos, new Vector3(barWidth * fearPercent, barHeight, 0.02f));
        
        // Fear status label
        #if UNITY_EDITOR
        Vector3 labelPos = transform.position + Vector3.up * 3.5f;
        
        GUIStyle style = new GUIStyle();
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 11;
        style.fontStyle = FontStyle.Bold;
        
        string statusText = "";
        
        if (isFleeing)
        {
            statusText = "😱 FLEEING";
            style.normal.textColor = new Color(0.5f, 0f, 1f);
            
            if (stamina != null && stamina.IsExhausted())
                statusText += " (EXHAUSTED)";
        }
        else if (currentFearLevel >= fearFleeThreshold * 0.5f)
        {
            statusText = "😰 NERVOUS";
            style.normal.textColor = new Color(0.7f, 0.3f, 1f);
        }
        else
        {
            statusText = "😌 CALM";
            style.normal.textColor = new Color(0.9f, 0.7f, 1f);
        }
        
        statusText += $" | Fear: {currentFearLevel:F0}";
        
        UnityEditor.Handles.Label(labelPos, statusText, style);
        #endif
        
        // Flee direction arrow
        if (isFleeing && primaryThreat != null)
        {
            Gizmos.color = new Color(0.5f, 0f, 1f);
            Vector3 fleeTarget = GetFleeTarget();
            Gizmos.DrawLine(transform.position, fleeTarget);
            Gizmos.DrawWireSphere(fleeTarget, 1f);
        }
        
        // Lines to threats
        foreach (Entity threat in detectedThreats)
        {
            if (threat == null) continue;
            
            Gizmos.color = threat == primaryThreat ? new Color(0.5f, 0f, 1f) : new Color(0.7f, 0.3f, 1f);
            Gizmos.DrawLine(transform.position, threat.transform.position);
        }
    }
    
    #endregion
}

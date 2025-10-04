using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Gestisce paura, detection minacce e comportamento fuga.
/// Include fear level system, stamina integration e visual feedback.
/// </summary>
public class FearComponent : MonoBehaviour
{
    [Header("Fear Settings")]
    [SerializeField] private LayerMask threatLayers;
    [SerializeField] private string[] threatTags = { "Player", "Leviathan" };
    
    [Header("Detection Radii")]
    [SerializeField] private float playerFearRadius = 2.5f;
    [SerializeField] private float leviathanFearRadius = 4f;
    [SerializeField] private float genericThreatRadius = 3f;
    
    [Header("Flee Behavior")]
    [SerializeField] private float fleeDistance = 15f;
    [SerializeField] private float fleeDuration = 10f;
    [SerializeField] private float calmDownDelay = 3f;
    
    [Header("Threat Priority Weights")]
    [SerializeField] private float playerThreatWeight = 2f;
    [SerializeField] private float leviathanThreatWeight = 1.5f;
    [SerializeField] private float genericThreatWeight = 1f;
    
    [Header("Fear Level System")]
    [SerializeField] private float maxFearLevel = 100f;
    [SerializeField] private float currentFearLevel = 0f;
    [SerializeField] private float fearIncreaseRate = 50f;
    [SerializeField] private float fearDecreaseRate = 20f;
    [SerializeField] private float fearFleeThreshold = 30f;
    
    [Header("Stamina Integration")]
    [SerializeField] private bool useStaminaWhenFleeing = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;
    
    // Events
    public UnityEvent<Entity> OnThreatDetected;
    public UnityEvent OnStartFleeing;
    public UnityEvent OnStopFleeing;
    public UnityEvent OnCalmDown;
    
    // State
    private bool isFleeing = false;
    private float fleeStartTime = 0f;
    private float lastThreatSeenTime = 0f;
    private Vector3 fleeDirection = Vector3.zero;
    private List<Entity> detectedThreats = new List<Entity>();
    private Entity primaryThreat = null;
    
    // Component references
    private Entity entity;
    private StaminaComponent stamina;
    
    void Awake()
    {
        entity = GetComponent<Entity>();
        stamina = GetComponent<StaminaComponent>();
    }
    
    void Update()
    {
        DetectThreats();
        UpdateFearLevel();
        UpdateFleeState();
        UpdateStaminaUsage();
    }
    
    /// <summary>
    /// Rileva minacce in range.
    /// </summary>
    private void DetectThreats()
    {
        detectedThreats.Clear();
        Vector3 totalFleeDirection = Vector3.zero;
        bool foundThreat = false;
        
        foreach (string tag in threatTags)
        {
            GameObject[] threats = GameObject.FindGameObjectsWithTag(tag);
            
            foreach (GameObject threatObj in threats)
            {
                if (threatObj == gameObject) continue;
                
                Entity threatEntity = threatObj.GetComponent<Entity>();
                if (threatEntity == null || threatEntity.IsDead()) continue;
                
                float distance = Vector3.Distance(transform.position, threatObj.transform.position);
                float detectionRadius = GetDetectionRadiusForTag(tag);
                
                if (distance <= detectionRadius)
                {
                    detectedThreats.Add(threatEntity);
                    
                    Vector3 directionAway = (transform.position - threatObj.transform.position).normalized;
                    float weight = GetThreatWeightForTag(tag);
                    float distanceWeight = 1f - (distance / detectionRadius);
                    totalFleeDirection += directionAway * weight * distanceWeight;
                    
                    foundThreat = true;
                    lastThreatSeenTime = Time.time;
                    
                    if (showDebugLogs && !isFleeing)
                        Debug.Log($"⚠️ {gameObject.name} detected threat: {threatObj.name} at {distance:F1}m");
                    
                    OnThreatDetected?.Invoke(threatEntity);
                }
            }
        }
        
        if (foundThreat)
        {
            fleeDirection = totalFleeDirection.normalized;
            primaryThreat = GetPrimaryThreat();
        }
    }
    
    /// <summary>
    /// Aggiorna fear level nel tempo.
    /// </summary>
    private void UpdateFearLevel()
    {
        bool hasThreatsNearby = detectedThreats.Count > 0;
        
        if (hasThreatsNearby)
        {
            currentFearLevel += fearIncreaseRate * Time.deltaTime;
            currentFearLevel = Mathf.Min(currentFearLevel, maxFearLevel);
        }
        else
        {
            currentFearLevel -= fearDecreaseRate * Time.deltaTime;
            currentFearLevel = Mathf.Max(currentFearLevel, 0f);
        }
        
        // Triggera fleeing se supera threshold
        if (currentFearLevel >= fearFleeThreshold && !isFleeing)
        {
            StartFleeing();
        }
        else if (currentFearLevel < fearFleeThreshold && isFleeing && detectedThreats.Count == 0)
        {
            StopFleeing();
        }
    }
    
    /// <summary>
    /// Update stato fuga.
    /// </summary>
    private void UpdateFleeState()
    {
        if (!isFleeing) return;
        
        float timeSinceLastThreat = Time.time - lastThreatSeenTime;
        float timeFleeing = Time.time - fleeStartTime;
        
        bool shouldStopFleeing = timeFleeing >= fleeDuration || 
                                 timeSinceLastThreat >= calmDownDelay;
        
        if (shouldStopFleeing)
        {
            StopFleeing();
        }
    }
    
    /// <summary>
    /// Consuma stamina durante fuga (integration con StaminaComponent).
    /// </summary>
    private void UpdateStaminaUsage()
    {
        if (!isFleeing || !useStaminaWhenFleeing || stamina == null) return;
        
        // Usa stamina durante fuga (simula sprint)
        bool hasStamina = stamina.UseSprint(Time.deltaTime);
        
        // Se esausto, riduce fear più velocemente (troppo stanco per restare spaventato)
        if (stamina.IsExhausted())
        {
            currentFearLevel -= fearDecreaseRate * 0.5f * Time.deltaTime;
            
            if (showDebugLogs)
                Debug.Log($"💨 {gameObject.name} too exhausted to flee effectively");
        }
    }
    
    /// <summary>
    /// Inizia comportamento fuga.
    /// </summary>
    private void StartFleeing()
    {
        isFleeing = true;
        fleeStartTime = Time.time;
        
        if (showDebugLogs)
            Debug.Log($"🏃 {gameObject.name} START FLEEING from {detectedThreats.Count} threat(s)");
        
        OnStartFleeing?.Invoke();
        
        // Notifica stamina che sta iniziando sprint
        if (stamina != null && useStaminaWhenFleeing)
        {
            // Stamina inizia automaticamente via UseSprint in UpdateStaminaUsage
        }
    }
    
    /// <summary>
    /// Ferma comportamento fuga.
    /// </summary>
    private void StopFleeing()
    {
        isFleeing = false;
        detectedThreats.Clear();
        primaryThreat = null;
        
        if (showDebugLogs)
            Debug.Log($"😌 {gameObject.name} STOP FLEEING - calming down");
        
        OnStopFleeing?.Invoke();
        OnCalmDown?.Invoke();
        
        // Notifica stamina di fermare sprint
        if (stamina != null)
        {
            stamina.StopUsingStamina();
        }
    }
    
    /// <summary>
    /// Ritorna minaccia primaria (più vicina e pericolosa).
    /// </summary>
    public Entity GetPrimaryThreat()
    {
        if (detectedThreats.Count == 0) return null;
        
        Entity primary = null;
        float highestDanger = 0f;
        
        foreach (Entity threat in detectedThreats)
        {
            if (threat == null) continue;
            
            float distance = Vector3.Distance(transform.position, threat.transform.position);
            float weight = GetThreatWeightForEntity(threat);
            float danger = weight / Mathf.Max(distance, 0.1f);
            
            if (danger > highestDanger)
            {
                highestDanger = danger;
                primary = threat;
            }
        }
        
        return primary;
    }
    
    private float GetDetectionRadiusForTag(string tag)
    {
        return tag switch
        {
            "Player" => playerFearRadius,
            "Leviathan" => leviathanFearRadius,
            _ => genericThreatRadius
        };
    }
    
    private float GetThreatWeightForTag(string tag)
    {
        return tag switch
        {
            "Player" => playerThreatWeight,
            "Leviathan" => leviathanThreatWeight,
            _ => genericThreatWeight
        };
    }
    
    private float GetThreatWeightForEntity(Entity entity)
    {
        if (entity == null) return genericThreatWeight;
        
        return entity.GetEntityType() switch
        {
            EntityType.Player => playerThreatWeight,
            EntityType.Leviathan => leviathanThreatWeight,
            _ => genericThreatWeight
        };
    }
    
    #region Public API
    
    public bool IsFleeing() => isFleeing;
    public Vector3 GetFleeDirection() => fleeDirection;
    public Vector3 GetFleeTarget() => transform.position + fleeDirection * fleeDistance;
    public List<Entity> GetDetectedThreats() => new List<Entity>(detectedThreats);
    public int GetThreatCount() => detectedThreats.Count;
    public float GetFearLevel() => currentFearLevel;
    public float GetFearPercentage() => currentFearLevel / maxFearLevel;
    public float GetFearThreshold() => fearFleeThreshold;
    
    public void ForceStopFleeing()
    {
        if (isFleeing)
            StopFleeing();
    }
    
    #endregion
    
    #region Debug Visualization
    
 void OnDrawGizmos()
{
    if (!showDebugGizmos) return;
    
    // Detection radii
    Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
    Gizmos.DrawSphere(transform.position, playerFearRadius);
    
    Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
    Gizmos.DrawSphere(transform.position, leviathanFearRadius);
    
    if (!Application.isPlaying) return;
    
    // === FEAR BAR (VIOLA) ===
    Vector3 barPos = transform.position + Vector3.up * 1.6f; // ← CAMBIATO da 0.9f
    float barWidth = 1f;
    float barHeight = 0.1f;
    
    // Background (grigio scuro)
    Gizmos.color = new Color(0.3f, 0.3f, 0.3f);
    Gizmos.DrawCube(barPos, new Vector3(barWidth, barHeight, 0.01f));
    
    // Foreground (VIOLA gradiente) ← NUOVO
    Color fearBarColor;
    if (currentFearLevel >= fearFleeThreshold)
        fearBarColor = new Color(0.5f, 0f, 1f); // Viola intenso
    else if (currentFearLevel >= fearFleeThreshold * 0.5f)
        fearBarColor = new Color(0.7f, 0.3f, 1f); // Viola medio
    else
        fearBarColor = new Color(0.9f, 0.7f, 1f); // Viola pallido
    
    Gizmos.color = fearBarColor;
    float fearPercent = currentFearLevel / maxFearLevel;
    Vector3 fearBarFillPos = barPos - new Vector3(barWidth * (1 - fearPercent) * 0.5f, 0, 0);
    Gizmos.DrawCube(fearBarFillPos, new Vector3(barWidth * fearPercent, barHeight, 0.02f));
    
    // Threshold marker (linea bianca)
    float thresholdX = (fearFleeThreshold / maxFearLevel - 0.5f) * barWidth;
    Vector3 thresholdPos = barPos + new Vector3(thresholdX, 0, 0);
    Gizmos.color = Color.white;
    Gizmos.DrawLine(thresholdPos + Vector3.up * barHeight * 0.5f, 
                    thresholdPos - Vector3.up * barHeight * 0.5f);
    
    // === FEAR STATUS LABEL ===
    #if UNITY_EDITOR
    Vector3 labelPos = transform.position + Vector3.up * 3.5f; // ← CAMBIATO da 2.2f
    
    GUIStyle style = new GUIStyle();
    style.alignment = TextAnchor.MiddleCenter;
    style.fontSize = 11;
    style.fontStyle = FontStyle.Bold;
    
    string statusText = "";
    
    if (isFleeing)
    {
        statusText = "😱 FLEEING";
        style.normal.textColor = new Color(0.5f, 0f, 1f); // Viola
        
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
    if (isFleeing)
    {
        Gizmos.color = new Color(0.5f, 0f, 1f); // Viola per freccia
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

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Gestisce comportamento caccia con FOV detection, spotting buildup e chase.
/// </summary>
public class HuntComponent : MonoBehaviour
{
    [Header("Prey Settings")]
    [SerializeField] private string[] preyTags = { "Fish", "Plankton" };
    [SerializeField] private LayerMask preyLayers = -1;
    
    [Header("Detection FOV (Outer)")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float detectionAngle = 120f; // Gradi
    [SerializeField] private float spottingDuration = 3f; // Tempo per "lock" preda
    
    [Header("Chase FOV (Inner)")]
    [SerializeField] private float chaseRange = 6f;
    [SerializeField] private float chaseAngle = 90f;
    
    [Header("Hunt Behavior")]
    [SerializeField] private float stalkSpeed = 1.5f;      // Velocità durante stalk
    [SerializeField] private float chaseSpeed = 3f;        // Velocità durante chase
    [SerializeField] private float loseTargetTime = 5f;    // Tempo prima di perdere target
    [SerializeField] private bool useStaminaWhenChasing = true;
    
    [Header("Hunger Integration")]
    [SerializeField] private bool requireHungerToHunt = true;
    [SerializeField] private float minHungerToHunt = 40f; // Hunger threshold per iniziare caccia
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;
    
    // Events
    public UnityEvent<Entity> OnPreySpotted;      // Preda vista (inizio spotting)
    public UnityEvent<Entity> OnPreyLocked;       // Preda locked (fine spotting)
    public UnityEvent<Entity> OnStartStalking;    // Inizia avvicinamento cauto
    public UnityEvent<Entity> OnStartChasing;     // Inizia inseguimento sprint
    public UnityEvent<Entity> OnLostTarget;       // Perso target
    public UnityEvent<Entity> OnTargetInRange;    // Target in attack range
    
    // State
    private HuntState currentState = HuntState.Idle;
    private Entity currentTarget = null;
    private float spottingStartTime = 0f;
    private float lastTargetSeenTime = 0f;
    private float spottingProgress = 0f; // 0-1 per barra buildup
    
    // Component references
    private Entity selfEntity;
    private HungerComponent hunger;
    private StaminaComponent stamina;
    private AttackComponent attack;
    
    void Awake()
    {
        selfEntity = GetComponent<Entity>();
        hunger = GetComponent<HungerComponent>();
        stamina = GetComponent<StaminaComponent>();
        attack = GetComponent<AttackComponent>();
    }
    
    void Update()
    {
        // Check se può cacciare (hunger requirement)
        if (requireHungerToHunt && hunger != null && !hunger.IsHungry())
        {
            if (currentState != HuntState.Idle)
            {
                ResetHunt();
            }
            return;
        }
        
        UpdateHuntStateMachine();
        UpdateStaminaUsage();
    }
    
    /// <summary>
    /// State machine principale caccia.
    /// </summary>
    private void UpdateHuntStateMachine()
    {
        switch (currentState)
        {
            case HuntState.Idle:
                ScanForPrey();
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
    
    /// <summary>
    /// IDLE: Scansiona detection FOV per prede.
    /// </summary>
    private void ScanForPrey()
    {
        Entity nearestPrey = FindNearestPreyInDetectionFOV();
        
        if (nearestPrey != null)
        {
            EnterSpotting(nearestPrey);
        }
    }
    
    /// <summary>
    /// SPOTTING: Buildup 3 secondi per "lock" preda.
    /// </summary>
    private void UpdateSpotting()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            ResetHunt();
            return;
        }
        
        // Check se ancora in FOV
        if (!IsInDetectionFOV(currentTarget))
        {
            if (showDebugLogs)
                Debug.Log($"🔍 {gameObject.name}: Lost sight of {currentTarget.GetEntityName()} during spotting");
            
            ResetHunt();
            return;
        }
        
        // Buildup progress
        float elapsed = Time.time - spottingStartTime;
        spottingProgress = Mathf.Clamp01(elapsed / spottingDuration);
        
        // Spotting completo → Stalk
        if (spottingProgress >= 1f)
        {
            EnterStalking();
        }
    }
    
    /// <summary>
    /// STALKING: Avvicinamento cauto verso preda.
    /// </summary>
    private void UpdateStalking()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            ResetHunt();
            return;
        }
        
        // Check se entra in chase FOV → Chase
        if (IsInChaseFOV(currentTarget))
        {
            EnterChasing();
            return;
        }
        
        // Check se esce da detection FOV → perde target
        if (!IsInDetectionFOV(currentTarget))
        {
            float timeSinceLastSeen = Time.time - lastTargetSeenTime;
            if (timeSinceLastSeen > loseTargetTime)
            {
                LoseTarget();
            }
            return;
        }
        
        lastTargetSeenTime = Time.time;
        
        // Movimento verso target gestito da movimento system (prossimo step)
    }
    
    /// <summary>
    /// CHASING: Inseguimento sprint.
    /// </summary>
    private void UpdateChasing()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            ResetHunt();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        // Check se in attack range → Attack
        if (attack != null && distance <= attack.GetAttackRange())
        {
            EnterAttacking();
            return;
        }
        
        // Check se esce da chase FOV → torna a Stalk
        if (!IsInChaseFOV(currentTarget))
        {
            if (IsInDetectionFOV(currentTarget))
            {
                // Ancora visibile ma fuori chase range → torna a stalk
                EnterStalking();
            }
            else
            {
                // Completamente fuori vista
                float timeSinceLastSeen = Time.time - lastTargetSeenTime;
                if (timeSinceLastSeen > loseTargetTime)
                {
                    LoseTarget();
                }
            }
            return;
        }
        
        lastTargetSeenTime = Time.time;
    }
    
    /// <summary>
    /// ATTACKING: In range, attacca.
    /// </summary>
    private void UpdateAttacking()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            ResetHunt();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        // Troppo lontano → torna a chase
        if (attack != null && distance > attack.GetAttackRange() * 1.2f)
        {
            EnterChasing();
            return;
        }
        
        // Esegui attacco tramite AttackComponent
        if (attack != null && attack.IsAttackReady())
        {
            bool killed = attack.TryAttack(currentTarget);
            
            if (currentTarget.IsDead())
            {
                if (showDebugLogs)
                    Debug.Log($"🍖 {gameObject.name} killed and will eat {currentTarget.GetEntityName()}");
                
                ResetHunt();
            }
        }
    }
    
    /// <summary>
    /// Consuma stamina durante chase.
    /// </summary>
    private void UpdateStaminaUsage()
    {
        if (currentState != HuntState.Chasing || !useStaminaWhenChasing || stamina == null)
        {
            // Stop usando stamina se non in chase
            if (stamina != null && currentState != HuntState.Chasing)
                stamina.StopUsingStamina();
            return;
        }
        
        stamina.UseSprint(Time.deltaTime);
    }
    
    #region State Transitions
    
    private void EnterSpotting(Entity prey)
    {
        currentState = HuntState.Spotting;
        currentTarget = prey;
        spottingStartTime = Time.time;
        spottingProgress = 0f;
        lastTargetSeenTime = Time.time;
        
        OnPreySpotted?.Invoke(prey);
        
        if (showDebugLogs)
            Debug.Log($"👁️ {gameObject.name} SPOTTING {prey.GetEntityName()}");
    }
    
    private void EnterStalking()
    {
        currentState = HuntState.Stalking;
        spottingProgress = 1f;
        
        OnPreyLocked?.Invoke(currentTarget);
        OnStartStalking?.Invoke(currentTarget);
        
        if (showDebugLogs)
            Debug.Log($"🚶 {gameObject.name} STALKING {currentTarget.GetEntityName()}");
    }
    
    private void EnterChasing()
    {
        currentState = HuntState.Chasing;
        
        OnStartChasing?.Invoke(currentTarget);
        
        if (showDebugLogs)
            Debug.Log($"🏃 {gameObject.name} CHASING {currentTarget.GetEntityName()}!");
    }
    
    private void EnterAttacking()
    {
        currentState = HuntState.Attacking;
        
        OnTargetInRange?.Invoke(currentTarget);
        
        if (showDebugLogs)
            Debug.Log($"⚔️ {gameObject.name} ATTACKING {currentTarget.GetEntityName()}!");
    }
    
    private void LoseTarget()
    {
        if (showDebugLogs)
            Debug.Log($"❌ {gameObject.name} lost target {currentTarget?.GetEntityName()}");
        
        OnLostTarget?.Invoke(currentTarget);
        ResetHunt();
    }
    
    private void ResetHunt()
    {
        currentState = HuntState.Idle;
        currentTarget = null;
        spottingProgress = 0f;
        
        if (stamina != null)
            stamina.StopUsingStamina();
    }
    
    #endregion
    
    #region FOV Detection
    
    /// <summary>
    /// Trova preda più vicina nel detection FOV.
    /// </summary>
    private Entity FindNearestPreyInDetectionFOV()
    {
        Entity nearest = null;
        float minDistance = detectionRange;
        
        foreach (string tag in preyTags)
        {
            GameObject[] preys = GameObject.FindGameObjectsWithTag(tag);
            
            foreach (GameObject preyObj in preys)
            {
                if (preyObj == gameObject) continue;
                
                Entity preyEntity = preyObj.GetComponent<Entity>();
                if (preyEntity == null || preyEntity.IsDead()) continue;
                
                if (IsInDetectionFOV(preyEntity))
                {
                    float distance = Vector3.Distance(transform.position, preyObj.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = preyEntity;
                    }
                }
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Check se target è nel detection FOV.
    /// </summary>
    private bool IsInDetectionFOV(Entity target)
    {
        if (target == null) return false;
        
        return IsInFOV(target.transform.position, detectionRange, detectionAngle);
    }
    
    /// <summary>
    /// Check se target è nel chase FOV.
    /// </summary>
    private bool IsInChaseFOV(Entity target)
    {
        if (target == null) return false;
        
        return IsInFOV(target.transform.position, chaseRange, chaseAngle);
    }
    
    /// <summary>
    /// Generic FOV check (range + angle).
    /// </summary>
    private bool IsInFOV(Vector3 targetPos, float range, float angle)
    {
        Vector3 directionToTarget = targetPos - transform.position;
        float distance = directionToTarget.magnitude;
        
        // Check range
        if (distance > range) return false;
        
        // Check angle (FOV cono)
        Vector3 forward = transform.forward;
        float angleToTarget = Vector3.Angle(forward, directionToTarget);
        
        return angleToTarget <= angle / 2f;
    }
    
    #endregion
    
    #region Public API
    
    public HuntState GetHuntState() => currentState;
    public Entity GetCurrentTarget() => currentTarget;
    public bool IsHunting() => currentState != HuntState.Idle;
    public bool IsChasing() => currentState == HuntState.Chasing;
    public float GetSpottingProgress() => spottingProgress;
    public float GetStalkSpeed() => stalkSpeed;
    public float GetChaseSpeed() => chaseSpeed;
    
    public void ForceStopHunt()
    {
        ResetHunt();
    }
    
    #endregion
    
    #region Debug Visualization
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        if (!Application.isPlaying) return;
        
        Vector3 forward = transform.forward;
        Vector3 origin = transform.position;
        
        // === DETECTION FOV (Outer) ===
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f); // Giallo trasparente
        DrawFOVCone(origin, forward, detectionRange, detectionAngle);
        
        // === CHASE FOV (Inner) ===
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // Rosso trasparente
        DrawFOVCone(origin, forward, chaseRange, chaseAngle);
        
        // === TARGET LINE ===
        if (currentTarget != null && currentTarget.IsAlive())
        {
            Color lineColor = currentState switch
            {
                HuntState.Spotting => Color.yellow,
                HuntState.Stalking => Color.green,
                HuntState.Chasing => Color.red,
                HuntState.Attacking => Color.magenta,
                _ => Color.white
            };
            
            Gizmos.color = lineColor;
            Gizmos.DrawLine(origin, currentTarget.transform.position);
        }
        
        // === STATE LABEL ===
        #if UNITY_EDITOR
        Vector3 labelPos = transform.position + Vector3.up * 2.8f;
        
        GUIStyle style = new GUIStyle();
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 11;
        style.fontStyle = FontStyle.Bold;
        
        string statusText = currentState switch
        {
            HuntState.Idle => "🔍 IDLE",
            HuntState.Spotting => $"👁️ SPOTTING {spottingProgress * 100:F0}%",
            HuntState.Stalking => "🚶 STALKING",
            HuntState.Chasing => "🏃 CHASING",
            HuntState.Attacking => "⚔️ ATTACKING",
            _ => ""
        };
        
        style.normal.textColor = currentState switch
        {
            HuntState.Spotting => Color.yellow,
            HuntState.Stalking => Color.green,
            HuntState.Chasing => Color.red,
            HuntState.Attacking => Color.magenta,
            _ => Color.gray
        };
        
        if (currentTarget != null)
            statusText += $"\n→ {currentTarget.GetEntityName()}";
        
        UnityEditor.Handles.Label(labelPos, statusText, style);
        #endif
        
        // === SPOTTING PROGRESS BAR ===
        if (currentState == HuntState.Spotting)
        {
            Vector3 barPos = transform.position + Vector3.up * 0.6f;
            DrawProgressBar(barPos, spottingProgress, Color.yellow, Color.gray);
        }
    }
    
    private void DrawFOVCone(Vector3 origin, Vector3 forward, float range, float angle)
    {
        int segments = 20;
        float halfAngle = angle / 2f;
        
        Vector3 leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * forward * range;
        Vector3 rightBoundary = Quaternion.Euler(0, halfAngle, 0) * forward * range;
        
        // Arc
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (angle * i / segments);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * forward * range;
            Vector3 nextDirection = Quaternion.Euler(0, currentAngle + (angle / segments), 0) * forward * range;
            
            Gizmos.DrawLine(origin + direction, origin + nextDirection);
        }
        
        // Boundaries
        Gizmos.DrawLine(origin, origin + leftBoundary);
        Gizmos.DrawLine(origin, origin + rightBoundary);
    }
    
    private void DrawProgressBar(Vector3 position, float progress, Color fillColor, Color bgColor)
    {
        float barWidth = 1f;
        float barHeight = 0.08f;
        
        // Background
        Gizmos.color = bgColor;
        Gizmos.DrawCube(position, new Vector3(barWidth, barHeight, 0.01f));
        
        // Fill
        Gizmos.color = fillColor;
        Vector3 fillPos = position - new Vector3(barWidth * (1 - progress) * 0.5f, 0, 0);
        Gizmos.DrawCube(fillPos, new Vector3(barWidth * progress, barHeight, 0.02f));
    }
    
    #endregion
}

/// <summary>
/// Stati hunt.
/// </summary>
public enum HuntState
{
    Idle,       // Cerca prede
    Spotting,   // Preda vista, buildup 3 sec
    Stalking,   // Avvicinamento cauto
    Chasing,    // Inseguimento sprint
    Attacking   // In attack range
}

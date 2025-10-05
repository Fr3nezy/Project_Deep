using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public enum HuntState
{
    Idle,
    Spotting,
    Stalking,
    Chasing,
    Attacking
}

/// <summary>
/// Hunt behavior per entity AI. Diet-based invece di tag-based.
/// </summary>
public class HuntComponent : MonoBehaviour
{
    [Header("Detection FOV")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float detectionAngle = 120f;
    
    [Header("Chase FOV")]
    [SerializeField] private float chaseRange = 6f;
    [SerializeField] private float chaseAngle = 90f;
    
    [Header("Spotting")]
    [SerializeField] private float spottingDuration = 3f;
    [SerializeField] private float loseTargetTime = 5f;
    
    [Header("Speed Settings")]
    [SerializeField] private float stalkSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3.0f;
    
    [Header("Hunt Requirements")]
    [SerializeField] private bool requireHungerToHunt = true;
    [SerializeField] private float minHungerToHunt = 40f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;
    
    // State
    private HuntState currentState = HuntState.Idle;
    private Entity currentTarget = null;
    private float spottingProgress = 0f;
    private float lastTargetSeenTime = 0f;
    private float scanInterval = 0.5f;
    private float nextScanTime = 0f;
    
    // Events
    public UnityEvent<Entity> OnTargetSpotted;
    public UnityEvent<Entity> OnTargetLocked;
    public UnityEvent<Entity> OnTargetLost;
    public UnityEvent<Entity> OnTargetKilled;
    
    // Component cache
    private Entity selfEntity;
    private HungerComponent hunger;
    private StaminaComponent stamina;
    private AttackComponent attack;
    private DietComponent diet;
    
    void Awake()
    {
        selfEntity = GetComponent<Entity>();
        hunger = GetComponent<HungerComponent>();
        stamina = GetComponent<StaminaComponent>();
        attack = GetComponent<AttackComponent>();
        diet = GetComponent<DietComponent>();
    }
    
    void Start()
    {
        Entity.OnAnyEntityDied += OnEntityDied;
    }
    
    void OnDestroy()
    {
        Entity.OnAnyEntityDied -= OnEntityDied;
    }
    
    void Update()
    {
        UpdateHuntBehavior();
    }
    
    private void UpdateHuntBehavior()
    {
        // ========== HUNGER CHECK (FIXED) ==========
        if (requireHungerToHunt && hunger != null)
        {
            float currentHunger = hunger.GetCurrentHunger();
            
            // Se hunger è TROPPO ALTA (ben nutrito), NON cacciare!
            if (currentHunger > minHungerToHunt)
            {
                if (showDebugLogs && currentState != HuntState.Idle)
                    Debug.Log($"[Hunt] {gameObject.name} not hungry! Hunger: {currentHunger}/{minHungerToHunt}");
                
                if (currentState != HuntState.Idle)
                {
                    StopHunting();
                }
                return;
            }
        }
        // ==========================================
        
        // Check se DietComponent presente
        if (diet == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[Hunt] {gameObject.name}: No DietComponent! Cannot hunt.");
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
    
    private void UpdateIdle()
    {
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;
        
        ScanForPrey();
    }
    
    private void ScanForPrey()
    {
        Entity nearestPrey = FindNearestPreyInDetectionFOV();
        
        if (nearestPrey != null)
        {
            EnterSpotting(nearestPrey);
        }
    }
    
    private Entity FindNearestPreyInDetectionFOV()
    {
        if (diet == null) return null;
        
        Entity nearestPrey = null;
        float minDistance = detectionRange;
        int highestPriority = -1;
        
        // Trova TUTTE le entity nella scena
        Entity[] allEntities = FindObjectsByType<Entity>(FindObjectsSortMode.None);
        
        foreach (Entity potentialPrey in allEntities)
        {
            if (potentialPrey == selfEntity) continue;
            if (potentialPrey.IsDead()) continue;
            
            // USA DIET COMPONENT per validare prey
            if (!diet.IsValidPrey(potentialPrey)) continue;
            
            // Check FOV
            if (!IsInDetectionFOV(potentialPrey)) continue;
            
            float distance = Vector3.Distance(transform.position, potentialPrey.transform.position);
            
            // Get priority da diet
            int priority = diet.GetPreyPriority(potentialPrey.GetEntityType());
            
            if (priority > highestPriority || (priority == highestPriority && distance < minDistance))
            {
                minDistance = distance;
                highestPriority = priority;
                nearestPrey = potentialPrey;
            }
        }
        
        return nearestPrey;
    }
    
    private void EnterSpotting(Entity target)
    {
        currentState = HuntState.Spotting;
        currentTarget = target;
        spottingProgress = 0f;
        lastTargetSeenTime = Time.time;
        
        if (showDebugLogs)
            Debug.Log($"👁️ {gameObject.name} SPOTTING {target.GetEntityName()}");
        
        OnTargetSpotted?.Invoke(target);
    }
    
    private void UpdateSpotting()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            StopHunting();
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
                LoseTarget();
            }
        }
    }
    
    private void EnterStalking()
    {
        currentState = HuntState.Stalking;
        
        if (showDebugLogs)
            Debug.Log($"🚶 {gameObject.name} STALKING {currentTarget.GetEntityName()}");
        
        OnTargetLocked?.Invoke(currentTarget);
    }
    
    private void UpdateStalking()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            StopHunting();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (distance <= chaseRange && IsInChaseFOV(currentTarget))
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
                LoseTarget();
            }
        }
    }
    
    private void EnterChasing()
    {
        currentState = HuntState.Chasing;
        
        if (showDebugLogs)
            Debug.Log($"🏃 {gameObject.name} CHASING {currentTarget.GetEntityName()}!");
        
        if (stamina != null)
        {
            stamina.StartSprint();
        }
    }
    
    private void UpdateChasing()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            StopHunting();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (attack != null && distance <= attack.GetAttackRange())
        {
            EnterAttacking();
            return;
        }
        
        if (distance > chaseRange || !IsInChaseFOV(currentTarget))
        {
            currentState = HuntState.Stalking;
            if (stamina != null)
            {
                stamina.StopSprint();
            }
        }
        
        lastTargetSeenTime = Time.time;
    }
    
    private void EnterAttacking()
    {
        currentState = HuntState.Attacking;
        
        if (showDebugLogs)
            Debug.Log($"⚔️ {gameObject.name} ATTACKING {currentTarget.GetEntityName()}!");
        
        if (stamina != null)
        {
            stamina.StopSprint();
        }
    }
    
    private void UpdateAttacking()
    {
        if (currentTarget == null || currentTarget.IsDead())
        {
            StopHunting();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (attack != null && attack.IsAttackReady())
        {
            attack.TryAttack(currentTarget);
        }
        
        if (distance > attack.GetAttackRange() * 1.5f)
        {
            EnterChasing();
        }
    }
    
    private void LoseTarget()
    {
        if (showDebugLogs)
            Debug.Log($"❌ {gameObject.name} lost target {currentTarget?.GetEntityName()}");
        
        OnTargetLost?.Invoke(currentTarget);
        StopHunting();
    }
    
    private void StopHunting()
    {
        currentState = HuntState.Idle;
        currentTarget = null;
        spottingProgress = 0f;
        
        if (stamina != null)
        {
            stamina.StopSprint();
        }
    }
    
    private void OnEntityDied(Entity entity, Entity killer)
    {
        if (entity == currentTarget)
        {
            if (killer == selfEntity)
            {
                OnTargetKilled?.Invoke(entity);
                
                if (showDebugLogs)
                    Debug.Log($"🍖 {gameObject.name} killed {entity.GetEntityName()}");
                
                // ========== FEED ON KILL (FIXED) ==========
                if (hunger != null && diet != null)
                {
                    DietComponent preyDiet = entity.GetDietComponent();
                    if (preyDiet != null)
                    {
                        float nutrition = preyDiet.GetNutritionValue();
                        hunger.Feed(nutrition);
                        
                        if (showDebugLogs)
                            Debug.Log($"🍖 {gameObject.name} fed +{nutrition} nutrition!");
                    }
                }
                // ==========================================
            }
            
            StopHunting();
        }
    }
    
    private bool IsInDetectionFOV(Entity target)
    {
        Vector3 dirToTarget = (target.transform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToTarget);
        float distance = Vector3.Distance(transform.position, target.transform.position);
        
        return angle <= detectionAngle / 2f && distance <= detectionRange;
    }
    
    private bool IsInChaseFOV(Entity target)
    {
        Vector3 dirToTarget = (target.transform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToTarget);
        float distance = Vector3.Distance(transform.position, target.transform.position);
        
        return angle <= chaseAngle / 2f && distance <= chaseRange;
    }
    
    // Setters per EntityConfig
    public void SetDetectionRange(float range) => detectionRange = range;
    public void SetDetectionAngle(float angle) => detectionAngle = angle;
    public void SetChaseRange(float range) => chaseRange = range;
    public void SetChaseSpeed(float speed) => chaseSpeed = speed;
    public void SetStalkSpeed(float speed) => stalkSpeed = speed;
    
    // Getters
    public HuntState GetHuntState() => currentState;
    public Entity GetCurrentTarget() => currentTarget;
    public bool IsHunting() => currentState != HuntState.Idle;
    public float GetChaseSpeed() => chaseSpeed;
    public float GetStalkSpeed() => stalkSpeed;
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        if (!Application.isPlaying) return;
        
        // Detection FOV (yellow)
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        DrawFOVCone(detectionRange, detectionAngle);
        
        // Chase FOV (red)
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        DrawFOVCone(chaseRange, chaseAngle);
        
        // Target line
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
            
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
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

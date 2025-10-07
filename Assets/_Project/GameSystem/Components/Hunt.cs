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

public class HuntComponent : MonoBehaviour
{
    #region Inspector Settings
    
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
    [Tooltip("How often to scan for prey (seconds)")]
    [SerializeField] private float scanInterval = 0.5f;
    [Tooltip("Update movement every N frames")]
    [SerializeField] private int movementUpdateFrequency = 1;
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugLogs = true; // Attivato per debug
    [SerializeField] protected bool showDebugGizmos = true;
    
    #endregion
    
    #region Events
    
    public UnityEvent<Entity> OnTargetSpotted;
    public UnityEvent<Entity> OnTargetLocked;
    public UnityEvent<Entity> OnTargetLost;
    public UnityEvent<Entity> OnTargetKilled;
    
    #endregion
    
    #region State & Performance Variables
    
    protected HuntState currentState = HuntState.Idle;
    protected Entity currentTarget = null;
    private float spottingProgress = 0f;
    private float lastTargetSeenTime = 0f;
    
    private float nextScanTime = 0f;
    private int frameCounter = 0;
    private Entity cachedBestPrey = null;
    private float cacheValidUntil = 0f;
    private const float CACHE_DURATION = 0.2f;
    
    private float detectionRangeSqr;
    private float chaseRangeSqr;
    private float halfDetectionAngle;
    private float halfChaseAngle;
    
    protected Entity selfEntity;
    private HungerComponent hunger;
    protected StaminaComponent stamina;
    protected AttackComponent attack;
    protected DietComponent diet;
    protected Transform cachedTransform;
    
    #endregion
    
    #region Initialization
    
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
        diet = GetComponent<DietComponent>();
        cachedTransform = transform;
        
        #if UNITY_EDITOR
        if (diet == null)
            Debug.LogError($"[Hunt] {gameObject.name}: DietComponent required!");
        #endif
    }
    
    private void PreCalculateValues()
    {
        detectionRangeSqr = detectionRange * detectionRange;
        chaseRangeSqr = chaseRange * chaseRange;
        halfDetectionAngle = detectionAngle * 0.5f;
        halfChaseAngle = chaseAngle * 0.5f;
        nextScanTime = Time.time + Random.Range(0f, scanInterval);
    }
    
    void Start()
    {
        Entity.OnAnyEntityDied += OnEntityDied;
    }
    
    void OnDestroy()
    {
        Entity.OnAnyEntityDied -= OnEntityDied;
    }
    
    #endregion
    
    #region Update Loop
    
void Update()
{
    // ========== DEBUG: Press P to force scan ==========
    if (UnityEngine.InputSystem.Keyboard.current != null && 
        UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
    {
        Debug.Log("=== FORCE SCAN PRESSED ===");
        Debug.Log($"Entity: {gameObject.name}");
        Debug.Log($"EntityType: {selfEntity?.GetEntityType()}");
        Debug.Log($"Diet present: {diet != null}");
        Debug.Log($"Can hunt: {CanHunt()}");
        
        if (diet != null)
        {
            Debug.Log($"Diet component: OK");
        }
        
        ScanForPrey();
    }
    
    // ========== AGGIUNGI QUESTO: Press E to test EntityManager ==========
    if (UnityEngine.InputSystem.Keyboard.current != null && 
        UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
    {
        Debug.Log("=== ENTITYMANAGER TEST ===");
        
        var allEntities = EntityManager.Instance.GetAllEntities();
        Debug.Log($"Total entities registered: {allEntities.Count}");
        
        foreach (var e in allEntities)
        {
            Debug.Log($"  - {e.GetEntityName()}: Type={e.GetEntityType()}");
        }
        
        // Check specifically for Player
        bool playerFound = false;
        foreach (var e in allEntities)
        {
            if (e.GetEntityType() == EntityType.Player)
            {
                playerFound = true;
                Debug.Log($"✓✓✓ PLAYER FOUND IN ENTITYMANAGER: {e.GetEntityName()}");
                
                // Check distance to Player
                float dist = Vector3.Distance(cachedTransform.position, e.transform.position);
                Debug.Log($"Distance to Player: {dist:F2}m (Detection Range: {detectionRange}m)");
                
                break;
            }
        }
        
        if (!playerFound)
        {
            Debug.LogError("❌❌❌ PLAYER NOT REGISTERED IN ENTITYMANAGER!");
        }
    }
    // =====================================================================
    
    if (selfEntity == null || selfEntity.IsDead() || diet == null) return;
    
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
        
        bool canHunt = hunger.GetCurrentHunger() <= minHungerToHunt;
        
  if (UnityEngine.InputSystem.Keyboard.current != null && 
        UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
            Debug.Log($"[CanHunt] Hunger: {hunger.GetCurrentHunger()}/{minHungerToHunt}, Can hunt: {canHunt}");
        
        return canHunt;
    }
    
    #endregion
    
    #region State Behaviors
    
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
        
        float distanceSqr = (cachedTransform.position - currentTarget.transform.position).sqrMagnitude;
        
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
        
        float distanceSqr = (cachedTransform.position - currentTarget.transform.position).sqrMagnitude;
        
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
        
        float distanceSqr = (cachedTransform.position - currentTarget.transform.position).sqrMagnitude;
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
    
    #endregion
    
    #region Prey Detection
    
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
        Debug.Log($"[FindBestPrey] {gameObject.name} scanning...");
        Debug.Log($"[FindBestPrey] Detection range: {detectionRange}");
        Debug.Log($"[FindBestPrey] Position: {cachedTransform.position}");
        
        List<Entity> nearbyEntities = EntityManager.Instance.GetEntitiesInRadius(
            cachedTransform.position, 
            detectionRange, 
            selfEntity
        );
        
        Debug.Log($"[FindBestPrey] Found {nearbyEntities.Count} entities nearby");
        
        foreach (var e in nearbyEntities)
        {
            Debug.Log($"  - Entity: {e.GetEntityName()}, Type: {e.GetEntityType()}, Alive: {e.IsAlive()}");
            
            bool isValidPrey = diet.IsValidPrey(e);
            Debug.Log($"    IsValidPrey: {isValidPrey}");
            
            if (isValidPrey)
            {
                bool inFOV = IsInDetectionFOV(e);
                float distance = Vector3.Distance(cachedTransform.position, e.transform.position);
                Debug.Log($"    InFOV: {inFOV}, Distance: {distance:F2}m");
            }
        }
        
        Entity bestPrey = null;
        float minDistance = detectionRangeSqr;
        int highestPriority = -1;
        
        foreach (Entity potentialPrey in nearbyEntities)
        {
            if (!diet.IsValidPrey(potentialPrey)) continue;
            if (!IsInDetectionFOV(potentialPrey)) continue;
            
            float distanceSqr = (cachedTransform.position - potentialPrey.transform.position).sqrMagnitude;
            int priority = diet.GetPreyPriority(potentialPrey.GetEntityType());
            
            Debug.Log($"  ✓ VALID PREY: {potentialPrey.GetEntityName()} (priority={priority})");
            
            if (priority > highestPriority || (priority == highestPriority && distanceSqr < minDistance))
            {
                minDistance = distanceSqr;
                highestPriority = priority;
                bestPrey = potentialPrey;
            }
        }
        
        if (bestPrey != null)
            Debug.Log($"[FindBestPrey] BEST PREY SELECTED: {bestPrey.GetEntityName()}");
        else
            Debug.Log($"[FindBestPrey] NO VALID PREY FOUND");
        
        return bestPrey;
    }
    
    protected bool IsInDetectionFOV(Entity target)
    {
        Vector3 dirToTarget = target.transform.position - cachedTransform.position;
        float distanceSqr = dirToTarget.sqrMagnitude;
        
        if (distanceSqr > detectionRangeSqr) return false;
        
        dirToTarget = dirToTarget.normalized;
        float angle = Vector3.Angle(cachedTransform.forward, dirToTarget);
        
        return angle <= halfDetectionAngle;
    }
    
    private bool IsInChaseFOV(Entity target)
    {
        Vector3 dirToTarget = target.transform.position - cachedTransform.position;
        float distanceSqr = dirToTarget.sqrMagnitude;
        
        if (distanceSqr > chaseRangeSqr) return false;
        
        dirToTarget = dirToTarget.normalized;
        float angle = Vector3.Angle(cachedTransform.forward, dirToTarget);
        
        return angle <= halfChaseAngle;
    }
    
    #endregion
    
    #region State Transitions
    
    protected void EnterSpotting(Entity target)
    {
        currentState = HuntState.Spotting;
        currentTarget = target;
        spottingProgress = 0f;
        lastTargetSeenTime = Time.time;
        
        Debug.Log($"👁️ {gameObject.name} SPOTTING {target.GetEntityName()}");
        
        OnTargetSpotted?.Invoke(target);
    }
    
    private void EnterStalking()
    {
        currentState = HuntState.Stalking;
        
        Debug.Log($"🚶 {gameObject.name} STALKING {currentTarget.GetEntityName()}");
        
        OnTargetLocked?.Invoke(currentTarget);
    }
    
    protected void EnterChasing()
    {
        currentState = HuntState.Chasing;
        
        Debug.Log($"🏃 {gameObject.name} CHASING {currentTarget.GetEntityName()}!");
        
        if (stamina != null) stamina.StartSprint();
    }
    
    private void EnterAttacking()
    {
        currentState = HuntState.Attacking;
        
        Debug.Log($"⚔️ {gameObject.name} ATTACKING {currentTarget.GetEntityName()}!");
        
        if (stamina != null) stamina.StopSprint();
    }
    
    private void LoseTarget(string reason = "")
    {
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
    
    #endregion
    
    #region Event Handlers
    
    private void OnEntityDied(Entity entity, Entity killer)
    {
        if (entity != currentTarget || killer != selfEntity) return;
        
        OnTargetKilled?.Invoke(entity);
        
        Debug.Log($"🍖 {gameObject.name} killed {entity.GetEntityName()}");
        
        if (hunger != null && diet != null)
        {
            DietComponent preyDiet = entity.GetDietComponent();
            if (preyDiet != null)
            {
                float nutrition = preyDiet.GetNutritionValue();
                hunger.Feed(nutrition);
                
                Debug.Log($"🍖 {gameObject.name} fed +{nutrition} nutrition!");
            }
        }
        
        StopHunting("Target killed");
    }
    
    #endregion
    
    #region Public API
    
    public void SetDetectionRange(float range) 
    { 
        detectionRange = range;
        detectionRangeSqr = range * range;
    }
    
    public void SetDetectionAngle(float angle) 
    { 
        detectionAngle = angle;
        halfDetectionAngle = angle * 0.5f;
    }
    
    public void SetChaseRange(float range) 
    { 
        chaseRange = range;
        chaseRangeSqr = range * range;
    }
    
    public void SetChaseSpeed(float speed) => chaseSpeed = speed;
    public void SetStalkSpeed(float speed) => stalkSpeed = speed;
    public void SetScanInterval(float interval) => scanInterval = interval;
    
    public HuntState GetHuntState() => currentState;
    public Entity GetCurrentTarget() => currentTarget;
    public bool IsHunting() => currentState != HuntState.Idle;
    public float GetChaseSpeed() => chaseSpeed;
    public float GetStalkSpeed() => stalkSpeed;
    protected Entity GetSelfEntity() => selfEntity;
    
    #endregion
    
    #region Debug Gizmos
    
    void OnDrawGizmos()
    {
        #if UNITY_EDITOR
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
            
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
        #endif
    }
    
    #if UNITY_EDITOR
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
    #endif
    
    #endregion
}

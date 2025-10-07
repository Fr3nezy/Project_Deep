using UnityEngine;

/// <summary>
/// Controlla movimento entità con steering behaviors e priority system.
/// Integration con Fear, Hunt, Stamina, Hunger components.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MovementController : MonoBehaviour
{
    [Header("Speed Settings")]
    [SerializeField] private float baseSpeed = 2f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float acceleration = 2f;
    [SerializeField] private float deceleration = 3f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 60f;
    [SerializeField] private bool smoothRotation = true;
    
    [Header("Wander Settings")]
    [SerializeField] private float wanderRadius = 10f;
    [SerializeField] private float wanderChangeInterval = 4f;
    [SerializeField] private float wanderSpeed = 0.8f;
    
    [Header("Obstacle Avoidance")]
    [SerializeField] private bool enableObstacleAvoidance = true;
    [SerializeField] private float obstacleDetectionRange = 3f;
    [SerializeField] private float avoidanceForce = 5f;
    [SerializeField] private LayerMask obstacleLayers = -1;
    [SerializeField] private int raycastCount = 5;
    
    [Header("Boundaries (Optional)")]
    [SerializeField] private bool useBoundaries = false;
    [SerializeField] private Vector3 boundaryCenter = Vector3.zero;
    [SerializeField] private Vector3 boundarySize = new Vector3(50, 20, 50);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDebugLogs = false;

    [Header("Animation Integration")]
    [SerializeField] private Animator fishAnimator;
    [SerializeField] private float animSpeedMultiplier = 0.5f;
    
    // Component references
    private Rigidbody rb;
    private Entity entity;
    private FearComponent fear;
    private HuntComponent hunt;
    private StaminaComponent stamina;
    private HungerComponent hunger;
    
    // State
    private MovementState currentState = MovementState.Idle;
    private Vector3 currentTarget = Vector3.zero;
    private Vector3 wanderTarget = Vector3.zero;
    private float nextWanderChangeTime = 0f;
    private float currentSpeed = 0f;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        entity = GetComponent<Entity>();
        fear = GetComponent<FearComponent>();
        hunt = GetComponent<HuntComponent>();
        stamina = GetComponent<StaminaComponent>();
        hunger = GetComponent<HungerComponent>();
        
        // Rigidbody setup
        rb.useGravity = false;
        rb.linearDamping = 2f;
        rb.angularDamping = 3f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }
    
    void Start()
    {
        PickNewWanderTarget();
    }
    
    void FixedUpdate()
    {
        DetermineBehavior();
        ApplyMovement();
        ApplyRotation();
        EnforceBoundaries();
        UpdateAnimator();
    }
    
    // Setters per EntityConfig
    public void SetBaseSpeed(float speed) => baseSpeed = speed;
    public void SetWanderSpeed(float speed) => wanderSpeed = speed;
    public void SetWanderRadius(float radius) => wanderRadius = radius;
    
    /// <summary>
    /// Priority-based behavior decision.
    /// </summary>
    private void DetermineBehavior()
    {
        Vector3 desiredDirection = Vector3.zero;
        float desiredSpeed = baseSpeed;
        
// PRIORITY 1: FEAR
if (fear != null && fear.IsFleeing())
{
    currentState = MovementState.Fleeing;
    desiredDirection = fear.GetFleeDirection();
    
    // ========== FIX: USA fleeSpeedMultiplier da FearComponent! ==========
    desiredSpeed = baseSpeed * fear.GetFleeSpeedMultiplier(); // ← DYNAMIC!
    // ===================================================================
    
    currentTarget = transform.position + desiredDirection * 10f;
    
    if (showDebugLogs)
        Debug.Log($"[Movement] {gameObject.name}: FLEEING at {desiredSpeed}m/s");
}

        // PRIORITY 2: HUNT
        else if (hunt != null && hunt.IsHunting())
        {
            Entity target = hunt.GetCurrentTarget();
            
            if (target != null && target.IsAlive())
            {
                currentTarget = target.transform.position;
                desiredDirection = (currentTarget - transform.position).normalized;
                
                if (hunt.GetHuntState() == HuntState.Chasing)
                {
                    currentState = MovementState.Chasing;
                    desiredSpeed = hunt.GetChaseSpeed();
                }
                else if (hunt.GetHuntState() == HuntState.Stalking)
                {
                    currentState = MovementState.Stalking;
                    desiredSpeed = hunt.GetStalkSpeed();
                }
                else if (hunt.GetHuntState() == HuntState.Attacking)
                {
                    currentState = MovementState.Attacking;
                    desiredSpeed = baseSpeed * 0.5f;
                }
                else
                {
                    currentState = MovementState.Seeking;
                    desiredSpeed = baseSpeed;
                }
            }
            else
            {
                currentState = MovementState.Wandering;
                desiredDirection = GetWanderDirection();
                desiredSpeed = wanderSpeed;
            }
        }
        // PRIORITY 3: WANDER
        else
        {
            currentState = MovementState.Wandering;
            desiredDirection = GetWanderDirection();
            desiredSpeed = wanderSpeed;
        }
        
        // Apply speed modifiers
        float finalSpeed = CalculateFinalSpeed(desiredSpeed);
        
        // Apply steering force
        ApplySteering(desiredDirection, finalSpeed);
        
        // Obstacle avoidance
        if (enableObstacleAvoidance)
        {
            ApplyObstacleAvoidance();
        }
    }
    
    /// <summary>
    /// Applica steering force verso direzione desiderata.
    /// </summary>
    private void ApplySteering(Vector3 direction, float speed)
    {
        if (direction == Vector3.zero) return;
        
        Vector3 desiredVelocity = direction.normalized * speed;
        Vector3 steering = desiredVelocity - rb.linearVelocity;
        
        steering = Vector3.ClampMagnitude(steering, acceleration);
        
        rb.AddForce(steering, ForceMode.Acceleration);
    }
    
    /// <summary>
    /// Calcola velocità finale con tutti i modifier.
    /// </summary>
private float CalculateFinalSpeed(float baseSpeed)
{
    float speed = baseSpeed;
    
    // Stamina multiplier
    if (stamina != null)
    {
        speed *= stamina.GetSpeedMultiplier();
    }
    
    // Hunger penalty
    if (hunger != null && hunger.IsStarving())
    {
        speed *= 0.7f;
    }
    
    // ========== FIX: Clamp solo se NON sta fuggendo! ==========
    // Se sta fuggendo, allow speed > maxSpeed (panic mode!)
    if (currentState != MovementState.Fleeing)
    {
        speed = Mathf.Min(speed, maxSpeed);
    }
    else
    {
        // Durante flee, clamp a maxSpeed * 2 (safety cap)
        speed = Mathf.Min(speed, maxSpeed * 2f);
    }
    // ===========================================================
    
    return speed;
}

    
    /// <summary>
    /// Wander behavior: movimento casuale drift.
    /// </summary>
    private Vector3 GetWanderDirection()
    {
        if (Time.time >= nextWanderChangeTime || Vector3.Distance(transform.position, wanderTarget) < 2f)
        {
            PickNewWanderTarget();
        }
        
        Vector3 direction = (wanderTarget - transform.position).normalized;
        return direction;
    }
    
    /// <summary>
    /// Sceglie nuovo target wander casuale.
    /// </summary>
    private void PickNewWanderTarget()
    {
        Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
        wanderTarget = transform.position + randomOffset;
        
        if (useBoundaries)
        {
            wanderTarget = ClampToBoundaries(wanderTarget);
        }
        
        nextWanderChangeTime = Time.time + wanderChangeInterval;
        
        if (showDebugLogs)
            Debug.Log($"[Movement] {gameObject.name}: New wander target at {wanderTarget}");
    }
    
    /// <summary>
    /// Obstacle avoidance con multiple raycast.
    /// </summary>
    private void ApplyObstacleAvoidance()
    {
        Vector3 avoidanceDirection = Vector3.zero;
        int hitCount = 0;
        
        for (int i = 0; i < raycastCount; i++)
        {
            float angle = -30f + (60f * i / (raycastCount - 1));
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, obstacleDetectionRange, obstacleLayers))
            {
                Vector3 avoidDir = Vector3.Reflect(direction, hit.normal);
                avoidanceDirection += avoidDir;
                hitCount++;
                
                if (showDebugGizmos)
                    Debug.DrawLine(transform.position, hit.point, Color.red);
            }
            else if (showDebugGizmos)
            {
                Debug.DrawRay(transform.position, direction * obstacleDetectionRange, Color.green);
            }
        }
        
        if (hitCount > 0)
        {
            avoidanceDirection = avoidanceDirection.normalized;
            rb.AddForce(avoidanceDirection * avoidanceForce, ForceMode.Acceleration);
            
            if (showDebugLogs)
                Debug.Log($"[Movement] {gameObject.name}: Avoiding {hitCount} obstacles");
        }
    }
    
    /// <summary>
    /// Applica movimento al rigidbody.
    /// </summary>
private void ApplyMovement()
{
    // ========== FIX: Clamp differenziato per flee ==========
    float speedCap = currentState == MovementState.Fleeing ? maxSpeed * 2f : maxSpeed;
    
    if (rb.linearVelocity.magnitude > speedCap)
    {
        rb.linearVelocity = rb.linearVelocity.normalized * speedCap;
    }
    // =======================================================
    
    currentSpeed = rb.linearVelocity.magnitude;
    
    // Stamina consumption
    if (stamina != null && currentSpeed > baseSpeed * 1.1f)
    {
        float staminaCost = 15f * Time.fixedDeltaTime;
        stamina.ConsumeStamina(staminaCost);
    }
}

    
    /// <summary>
    /// Ruota entità verso direzione movimento.
    /// </summary>
    private void ApplyRotation()
    {
        if (rb.linearVelocity.magnitude < 0.1f) return;
        
        Vector3 lookDirection = rb.linearVelocity.normalized;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
        
        if (smoothRotation)
        {
            float step = rotationSpeed * Time.fixedDeltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, step);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }
    
    /// <summary>
    /// Forza entità dentro boundaries.
    /// </summary>
    private void EnforceBoundaries()
    {
        if (!useBoundaries) return;
        
        Vector3 pos = transform.position;
        Vector3 min = boundaryCenter - boundarySize / 2f;
        Vector3 max = boundaryCenter + boundarySize / 2f;
        
        float pushDistance = 5f;
        float pushStrength = 3f;
        
        if (pos.x < min.x + pushDistance)
            rb.AddForce(Vector3.right * pushStrength, ForceMode.Acceleration);
        else if (pos.x > max.x - pushDistance)
            rb.AddForce(Vector3.left * pushStrength, ForceMode.Acceleration);
        
        if (pos.y < min.y + pushDistance)
            rb.AddForce(Vector3.up * pushStrength, ForceMode.Acceleration);
        else if (pos.y > max.y - pushDistance)
            rb.AddForce(Vector3.down * pushStrength, ForceMode.Acceleration);
        
        if (pos.z < min.z + pushDistance)
            rb.AddForce(Vector3.forward * pushStrength, ForceMode.Acceleration);
        else if (pos.z > max.z - pushDistance)
            rb.AddForce(Vector3.back * pushStrength, ForceMode.Acceleration);
        
        // Hard clamp
        pos.x = Mathf.Clamp(pos.x, min.x, max.x);
        pos.y = Mathf.Clamp(pos.y, min.y, max.y);
        pos.z = Mathf.Clamp(pos.z, min.z, max.z);
        transform.position = pos;
    }
    
    private Vector3 ClampToBoundaries(Vector3 position)
    {
        Vector3 min = boundaryCenter - boundarySize / 2f;
        Vector3 max = boundaryCenter + boundarySize / 2f;
        
        position.x = Mathf.Clamp(position.x, min.x, max.x);
        position.y = Mathf.Clamp(position.y, min.y, max.y);
        position.z = Mathf.Clamp(position.z, min.z, max.z);
        
        return position;
    }
    
    /// <summary>
    /// Update animator speed based on movement.
    /// </summary>
    private void UpdateAnimator()
    {
        if (fishAnimator == null) return;
        
        float animSpeed = currentSpeed * animSpeedMultiplier;
        animSpeed = Mathf.Max(animSpeed, 0.3f);
        
        fishAnimator.SetFloat("Speed", animSpeed);
    }
    
    #region Public API
    
    public MovementState GetCurrentState() => currentState;
    public float GetCurrentSpeed() => currentSpeed;
    public Vector3 GetCurrentTarget() => currentTarget;
    
    public void MoveToPosition(Vector3 position, float speed)
    {
        currentTarget = position;
        Vector3 direction = (position - transform.position).normalized;
        ApplySteering(direction, speed);
    }
    
    public void Stop()
    {
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
    }
    
    #endregion
    
    #region Debug Visualization
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        if (!Application.isPlaying) return;
        
        // Current target
        if (currentTarget != Vector3.zero)
        {
            Gizmos.color = currentState switch
            {
                MovementState.Fleeing => Color.red,
                MovementState.Chasing => Color.yellow,
                MovementState.Stalking => Color.green,
                MovementState.Wandering => Color.cyan,
                _ => Color.white
            };
            
            Gizmos.DrawWireSphere(currentTarget, 0.5f);
            Gizmos.DrawLine(transform.position, currentTarget);
        }
        
        // Wander radius
        if (currentState == MovementState.Wandering)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, wanderRadius);
        }
        
        // Boundaries
        if (useBoundaries)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(boundaryCenter, boundarySize);
        }
        
        // Velocity vector
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, rb.linearVelocity);
    }
    
    #endregion
}

/// <summary>
/// Stati movimento.
/// </summary>
public enum MovementState
{
    Idle,
    Wandering,
    Seeking,
    Stalking,
    Chasing,
    Attacking,
    Fleeing
}

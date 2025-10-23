using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Advanced 3D fluid movement with stamina consumption and modular obstacle avoidance.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MovementController : MonoBehaviour
    {
        [Header("Speed Settings")]
        [SerializeField] private float baseSpeed = 2f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float acceleration = 2f;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 60f;
        [SerializeField] private bool smoothRotation = true;
        [SerializeField] private float verticalRotationSpeed = 30f; // NEW: Separate vertical rotation speed
        [SerializeField] private float maxVerticalAngle = 45f; // NEW: Limit vertical tilt
        [SerializeField] private bool limitVerticalRotation = true; // NEW: Enable/disable vertical limits

        [Header("3D Wander Settings")]
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float wanderChangeInterval = 4f;
        [SerializeField] private float wanderSpeed = 0.8f;

        [Header("Vertical Movement")]
        [SerializeField] private bool enableVerticalMovement = true;
        [SerializeField] private float verticalWanderAmplitude = 3f;
        [SerializeField] private float verticalWanderFrequency = 0.3f;
        [SerializeField] private float verticalSmoothness = 2f;
        [SerializeField] private float minHeightAboveGround = 2f;
        [SerializeField] private float maxSwimDepth = 50f;

        [Header("Perlin Noise Drift")]
        [SerializeField] private bool usePerlinDrift = true;
        [SerializeField] private float perlinScale = 0.5f;
        [SerializeField] private float perlinStrength = 0.3f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private bool enableObstacleAvoidance = true;
        [SerializeField] private float obstacleDetectionRange = 3f;
        [SerializeField] private float avoidanceForce = 5f;
        [SerializeField] private LayerMask obstacleLayers = -1;
        [SerializeField] private int raycastCount = 5;

        [Header("Ground Avoidance")]
        [SerializeField] private bool enableGroundAvoidance = true;
        [SerializeField] private float groundDetectionRange = 3f;
        [SerializeField] private float groundAvoidanceForce = 8f;
        [SerializeField] private LayerMask groundLayer = -1;

        [Header("Stamina Consumption")]
        [SerializeField] private float fleeStaminaCost = 20f;
        [SerializeField] private float chaseStaminaCost = 15f;
        [SerializeField] private float sprintStaminaCost = 10f;

        [Header("Boundaries")]
        [SerializeField] private bool useBoundaries = false;
        [SerializeField] private Vector3 boundaryCenter = Vector3.zero;
        [SerializeField] private Vector3 boundarySize = new Vector3(50, 20, 50);

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool showDebugLogs = false;

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

        // Vertical movement
        private float verticalNoiseOffset;
        private float targetYPosition;
        private float currentGroundHeight = float.MinValue;

        // Perlin noise
        private Vector2 perlinOffset;

        // NEW: Smooth rotation tracking
        private float currentVerticalAngle = 0f;
        private Vector3 lastVelocityDirection = Vector3.forward;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            entity = GetComponent<Entity>();
            fear = GetComponent<FearComponent>();
            hunt = GetComponent<HuntComponent>();
            stamina = GetComponent<StaminaComponent>();
            hunger = GetComponent<HungerComponent>();

            // Rigidbody setup for underwater physics
            rb.useGravity = false;
            rb.linearDamping = 2f;
            rb.angularDamping = 5f; // Increased for more stability
            
            // NEW: Only freeze roll (Z rotation) to allow natural pitch/yaw
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;

            // Initialize random offsets
            verticalNoiseOffset = Random.Range(0f, 100f);
            perlinOffset = new Vector2(Random.Range(0f, 100f), Random.Range(0f, 100f));
            targetYPosition = transform.position.y;
            lastVelocityDirection = transform.forward;
        }

        private void Start()
        {
            PickNewWanderTarget();
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE LOOP
        // ═══════════════════════════════════════════════════════════════

        private void FixedUpdate()
        {
            if (entity != null && entity.IsDead())
                return;

            UpdateGroundHeight();
            DetermineBehavior();
            ApplyMovement();
            ApplyRotation(); // NEW: Improved rotation system
            EnforceBoundaries();
        }

        // ═══════════════════════════════════════════════════════════════
        // GROUND HEIGHT TRACKING
        // ═══════════════════════════════════════════════════════════════

        private void UpdateGroundHeight()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 100f, groundLayer))
            {
                currentGroundHeight = hit.point.y;
            }
            else
            {
                currentGroundHeight = float.MinValue;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // BEHAVIOR DETERMINATION
        // ═══════════════════════════════════════════════════════════════

        private void DetermineBehavior()
        {
            Vector3 desiredDirection = Vector3.zero;
            float desiredSpeed = baseSpeed;

            // PRIORITY 1: FLEE
            if (fear != null && fear.IsFleeing())
            {
                currentState = MovementState.Fleeing;
                desiredDirection = fear.GetFleeDirection();
                desiredSpeed = baseSpeed * 1.8f;
                currentTarget = transform.position + desiredDirection * 10f;
                ConsumeStamina(fleeStaminaCost);
            }
            // PRIORITY 2: HUNT
            else if (hunt != null && hunt.IsHunting())
            {
                Entity target = hunt.GetCurrentTarget();
                if (target != null && target.IsAlive())
                {
                    currentTarget = target.GetPosition();
                    desiredDirection = (currentTarget - transform.position).normalized;

                    if (hunt.IsChasing())
                    {
                        currentState = MovementState.Chasing;
                        desiredSpeed = baseSpeed * 1.5f;
                        ConsumeStamina(chaseStaminaCost);
                    }
                    else if (hunt.IsStalking())
                    {
                        currentState = MovementState.Stalking;
                        desiredSpeed = baseSpeed * 0.7f;
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

            float finalSpeed = ApplyStaminaModifier(desiredSpeed);

            if (hunger != null && hunger.IsStarving())
            {
                finalSpeed *= 0.6f;
            }

            ApplySteering(desiredDirection, finalSpeed);

            if (enableObstacleAvoidance)
            {
                float avoidancePriority = (currentState == MovementState.Fleeing || currentState == MovementState.Chasing) ? 2f : 1f;
                ApplyObstacleAvoidance(avoidancePriority);
            }

            if (enableGroundAvoidance)
            {
                ApplyGroundAvoidance();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STAMINA SYSTEM
        // ═══════════════════════════════════════════════════════════════

        private void ConsumeStamina(float costPerSecond)
        {
            if (stamina == null)
                return;

            float cost = costPerSecond * Time.fixedDeltaTime;
            stamina.ConsumeStamina(cost);
        }

        private float ApplyStaminaModifier(float speed)
        {
            if (stamina == null)
                return speed;

            float staminaPercent = stamina.GetStaminaPercentage();

            if (staminaPercent < 0.1f)
            {
                return speed * 0.5f;
            }
            else if (staminaPercent < 0.3f)
            {
                return speed * 0.75f;
            }

            return speed;
        }

        // ═══════════════════════════════════════════════════════════════
        // STEERING & MOVEMENT
        // ═══════════════════════════════════════════════════════════════

        private void ApplySteering(Vector3 direction, float speed)
        {
            if (direction == Vector3.zero)
                return;

            Vector3 desiredVelocity = direction.normalized * speed;
            Vector3 steering = desiredVelocity - rb.linearVelocity;
            steering = Vector3.ClampMagnitude(steering, acceleration);

            rb.AddForce(steering, ForceMode.Acceleration);
        }

        // ═══════════════════════════════════════════════════════════════
        // 3D WANDERING (IMPROVED)
        // ═══════════════════════════════════════════════════════════════

        private Vector3 GetWanderDirection()
        {
            if (Time.time > nextWanderChangeTime || Vector3.Distance(transform.position, wanderTarget) < 2f)
            {
                PickNewWanderTarget();
            }

            Vector3 direction = (wanderTarget - transform.position).normalized;

            if (usePerlinDrift)
            {
                float perlinX = Mathf.PerlinNoise(Time.time * perlinScale + perlinOffset.x, 0f);
                float perlinZ = Mathf.PerlinNoise(0f, Time.time * perlinScale + perlinOffset.y);

                Vector3 drift = new Vector3(
                    (perlinX - 0.5f) * perlinStrength,
                    0f,
                    (perlinZ - 0.5f) * perlinStrength
                );

                direction += drift;
                direction.Normalize();
            }

            if (enableVerticalMovement)
            {
                float verticalNoise = Mathf.PerlinNoise(Time.time * verticalWanderFrequency + verticalNoiseOffset, 0f);
                
                float safeMinHeight = currentGroundHeight + minHeightAboveGround;
                float safeMaxHeight = Mathf.Min(safeMinHeight + verticalWanderAmplitude * 2f, -maxSwimDepth);

                targetYPosition = Mathf.Lerp(safeMinHeight, safeMaxHeight, verticalNoise);

                float verticalDelta = targetYPosition - transform.position.y;
                direction.y = Mathf.Lerp(direction.y, verticalDelta, verticalSmoothness * Time.deltaTime);
            }

            return direction;
        }

        private void PickNewWanderTarget()
        {
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            wanderTarget = transform.position + randomOffset;

            if (currentGroundHeight != float.MinValue)
            {
                float safeMinY = currentGroundHeight + minHeightAboveGround;
                wanderTarget.y = Mathf.Max(wanderTarget.y, safeMinY);
            }

            if (useBoundaries)
            {
                wanderTarget = ClampToBoundaries(wanderTarget);
            }

            nextWanderChangeTime = Time.time + wanderChangeInterval;
        }

        // ═══════════════════════════════════════════════════════════════
        // OBSTACLE AVOIDANCE
        // ═══════════════════════════════════════════════════════════════

        private void ApplyObstacleAvoidance(float priorityMultiplier = 1f)
        {
            Vector3 avoidanceDirection = Vector3.zero;
            int hitCount = 0;

            for (int i = 0; i < raycastCount; i++)
            {
                float angle = -45f + (90f * i / (raycastCount - 1));
                Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

                if (Physics.Raycast(transform.position, direction, out RaycastHit hit, obstacleDetectionRange, obstacleLayers))
                {
                    Vector3 awayFromObstacle = transform.position - hit.point;
                    awayFromObstacle.Normalize();

                    float proximity = 1f - (hit.distance / obstacleDetectionRange);
                    avoidanceDirection += awayFromObstacle * proximity;
                    hitCount++;

                    if (showDebugGizmos)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.red);
                    }
                }
                else if (showDebugGizmos)
                {
                    Debug.DrawRay(transform.position, direction * obstacleDetectionRange, Color.green);
                }
            }

            if (hitCount > 0)
            {
                avoidanceDirection.Normalize();
                float force = avoidanceForce * priorityMultiplier;
                rb.AddForce(avoidanceDirection * force, ForceMode.Acceleration);

                if (showDebugLogs)
                {
                    Debug.Log($"[Movement] {gameObject.name} avoiding {hitCount} obstacles (priority: {priorityMultiplier}x)");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GROUND AVOIDANCE
        // ═══════════════════════════════════════════════════════════════

        private void ApplyGroundAvoidance()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundDetectionRange, groundLayer))
            {
                float distanceToGround = hit.distance;

                if (distanceToGround < minHeightAboveGround)
                {
                    float proximityFactor = 1f - (distanceToGround / minHeightAboveGround);
                    Vector3 upwardForce = Vector3.up * groundAvoidanceForce * proximityFactor;

                    rb.AddForce(upwardForce, ForceMode.Acceleration);

                    if (showDebugGizmos)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.magenta);
                    }

                    if (showDebugLogs)
                    {
                        Debug.Log($"[Movement] {gameObject.name} avoiding ground (dist: {distanceToGround:F2}m, force: {proximityFactor:F2})");
                    }
                }
                else if (showDebugGizmos)
                {
                    Debug.DrawLine(transform.position, hit.point, Color.cyan);
                }
            }

            Vector3 forwardDownDir = (transform.forward + Vector3.down * 0.5f).normalized;
            if (Physics.Raycast(transform.position, forwardDownDir, out RaycastHit forwardHit, groundDetectionRange, groundLayer))
            {
                Vector3 avoidDir = Vector3.up + (transform.right * Random.Range(-0.5f, 0.5f));
                rb.AddForce(avoidDir * groundAvoidanceForce * 0.5f, ForceMode.Acceleration);

                if (showDebugGizmos)
                {
                    Debug.DrawLine(transform.position, forwardHit.point, Color.yellow);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PHYSICS
        // ═══════════════════════════════════════════════════════════════

        private void ApplyMovement()
        {
            float speedCap = (currentState == MovementState.Fleeing) ? maxSpeed * 2f : maxSpeed;

            if (rb.linearVelocity.magnitude > speedCap)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * speedCap;
            }

            currentSpeed = rb.linearVelocity.magnitude;
        }

        // ═══════════════════════════════════════════════════════════════
        // NEW: IMPROVED ROTATION SYSTEM (NO JITTER)
        // ═══════════════════════════════════════════════════════════════

        private void ApplyRotation()
        {
            // Skip if moving too slowly
            if (rb.linearVelocity.magnitude < 0.1f)
            {
                // Gradually return to horizontal when idle
                if (limitVerticalRotation)
                {
                    currentVerticalAngle = Mathf.Lerp(currentVerticalAngle, 0f, verticalRotationSpeed * 0.5f * Time.fixedDeltaTime);
                    ApplyRotationToTransform(transform.forward, currentVerticalAngle);
                }
                return;
            }

            Vector3 velocity = rb.linearVelocity;
            
            // Smooth velocity changes to prevent jitter
            Vector3 smoothedDirection = Vector3.Lerp(lastVelocityDirection, velocity.normalized, 0.3f);
            lastVelocityDirection = smoothedDirection;

            // Separate horizontal and vertical components
            Vector3 horizontalDirection = new Vector3(smoothedDirection.x, 0f, smoothedDirection.z).normalized;
            
            // Calculate vertical angle (pitch)
            float targetVerticalAngle = 0f;
            if (horizontalDirection.magnitude > 0.01f)
            {
                // Calculate angle based on vertical velocity component
                float verticalComponent = smoothedDirection.y;
                targetVerticalAngle = Mathf.Asin(Mathf.Clamp(verticalComponent, -1f, 1f)) * Mathf.Rad2Deg;
                
                // Clamp to max angle if enabled
                if (limitVerticalRotation)
                {
                    targetVerticalAngle = Mathf.Clamp(targetVerticalAngle, -maxVerticalAngle, maxVerticalAngle);
                }
            }

            // Smoothly interpolate vertical angle
            currentVerticalAngle = Mathf.Lerp(
                currentVerticalAngle, 
                targetVerticalAngle, 
                verticalRotationSpeed * Time.fixedDeltaTime
            );

            // Apply rotation
            if (horizontalDirection.magnitude > 0.01f)
            {
                ApplyRotationToTransform(horizontalDirection, currentVerticalAngle);
            }
        }

        private void ApplyRotationToTransform(Vector3 horizontalDirection, float verticalAngle)
        {
            // Calculate target rotation for horizontal direction (yaw)
            Quaternion horizontalRotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);
            
            // Apply vertical tilt (pitch) around local X axis
            Quaternion verticalRotation = Quaternion.Euler(verticalAngle, 0f, 0f);
            
            // Combine rotations
            Quaternion targetRotation = horizontalRotation * verticalRotation;

            // Smoothly rotate to target
            if (smoothRotation)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.fixedDeltaTime
                );
            }
            else
            {
                transform.rotation = targetRotation;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // BOUNDARIES
        // ═══════════════════════════════════════════════════════════════

        private void EnforceBoundaries()
        {
            if (!useBoundaries)
                return;

            Vector3 pos = transform.position;
            Vector3 min = boundaryCenter - boundarySize / 2f;
            Vector3 max = boundaryCenter + boundarySize / 2f;

            float pushDistance = 5f;
            float pushStrength = 3f;

            if (pos.x < min.x + pushDistance) rb.AddForce(Vector3.right * pushStrength, ForceMode.Acceleration);
            else if (pos.x > max.x - pushDistance) rb.AddForce(Vector3.left * pushStrength, ForceMode.Acceleration);

            if (pos.y < min.y + pushDistance) rb.AddForce(Vector3.up * pushStrength, ForceMode.Acceleration);
            else if (pos.y > max.y - pushDistance) rb.AddForce(Vector3.down * pushStrength, ForceMode.Acceleration);

            if (pos.z < min.z + pushDistance) rb.AddForce(Vector3.forward * pushStrength, ForceMode.Acceleration);
            else if (pos.z > max.z - pushDistance) rb.AddForce(Vector3.back * pushStrength, ForceMode.Acceleration);

            pos.x = Mathf.Clamp(pos.x, min.x, max.x);
            pos.y = Mathf.Clamp(pos.y, min.y, max.y);
            pos.z = Mathf.Clamp(pos.z, min.z, max.z);

            transform.position = pos;
        }

        private Vector3 ClampToBoundaries(Vector3 position)
        {
            Vector3 min = boundaryCenter - boundarySize / 2f;
            Vector3 max = boundaryCenter + boundarySize / 2f;

            return new Vector3(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y),
                Mathf.Clamp(position.z, min.z, max.z)
            );
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - Configuration
        // ═══════════════════════════════════════════════════════════════

        public void SetBaseSpeed(float speed) => baseSpeed = Mathf.Max(0.1f, speed);
        public void SetMaxSpeed(float speed) => maxSpeed = Mathf.Max(0.1f, speed);
        public void SetTurnSpeed(float speed) => rotationSpeed = Mathf.Max(10f, speed);
        public void SetWanderSpeed(float speed) => wanderSpeed = Mathf.Max(0.1f, speed);
        public void SetWanderRadius(float radius) => wanderRadius = Mathf.Max(1f, radius);

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - State
        // ═══════════════════════════════════════════════════════════════

        public MovementState GetCurrentState() => currentState;
        public float GetCurrentSpeed() => currentSpeed;
        public Vector3 GetCurrentTarget() => currentTarget;

        // ═══════════════════════════════════════════════════════════════
        // DEBUG GIZMOS
        // ═══════════════════════════════════════════════════════════════

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying)
                return;

            if (currentTarget != Vector3.zero)
            {
                Gizmos.color = GetStateColor();
                Gizmos.DrawWireSphere(currentTarget, 0.5f);
                Gizmos.DrawLine(transform.position, currentTarget);
            }

            if (currentState == MovementState.Wandering)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, wanderRadius);
            }

            if (useBoundaries)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(boundaryCenter, boundarySize);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, rb ? rb.linearVelocity : Vector3.zero);

            if (currentGroundHeight != float.MinValue)
            {
                Vector3 groundPos = new Vector3(transform.position.x, currentGroundHeight, transform.position.z);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, groundPos);
                Gizmos.DrawWireSphere(groundPos, 0.3f);
            }
        }

        private Color GetStateColor()
        {
            return currentState switch
            {
                MovementState.Fleeing => Color.red,
                MovementState.Chasing => Color.yellow,
                MovementState.Stalking => Color.green,
                MovementState.Wandering => Color.cyan,
                _ => Color.white
            };
        }
    }

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
}

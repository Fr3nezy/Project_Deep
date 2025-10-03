// File: AquaticEntity.cs (VERSIONE CORRETTA - Physics Fix)
using UnityEngine;

public abstract class AquaticEntity : MonoBehaviour
{
    [Header("Parametri Comuni")]
    public float moveSpeed = 2f;
    public float wanderRadius = 10f;
    public float wanderInterval = 3f;
    
    [Header("Navigation")]
    public LayerMask obstacleLayer = -1;
    public float avoidanceRadius = 2f;
    
    protected Vector3 wanderTarget;
    private float nextWanderTime;
    protected Rigidbody rb;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // CONFIGURAZIONE CORRETTA RIGIDBODY
        rb.useGravity = false;        // Simulazione underwater
        rb.isKinematic = false;       // NON kinematic per usare velocity
        rb.linearDamping = 2f;                 // Resistenza acqua
        rb.angularDamping = 5f;          // Resistenza rotazione
        rb.freezeRotation = true;     // Evita rotazioni indesiderate
        
        ScheduleNextWander();
    }

    protected virtual void Update()
    {
        if (Time.time >= nextWanderTime)
            PickWanderTarget();
    }

    protected void ScheduleNextWander()
    {
        nextWanderTime = Time.time + wanderInterval + Random.Range(0f, 1f);
    }

    protected void PickWanderTarget()
    {
        Vector2 circle = Random.insideUnitCircle * wanderRadius;
        Vector3 origin = transform.position;
        wanderTarget = new Vector3(
            origin.x + circle.x,
            origin.y,
            origin.z + circle.y
        );
        ScheduleNextWander();
    }

    // MOVIMENTO CORRETTO CON RIGIDBODY
    protected void MoveTowards(Vector3 target, float speed)
    {
        Vector3 direction = (target - transform.position).normalized;
        
        // Check per ostacoli
        if (Physics.Raycast(transform.position, direction, avoidanceRadius, obstacleLayer))
        {
            Vector3 avoidDir = GetAvoidanceDirection(direction);
            rb.linearVelocity = avoidDir * speed;
        }
        else
        {
            rb.linearVelocity = direction * speed;
        }
        
        // Rotazione graduale verso direzione movimento
        if (rb.linearVelocity != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 3f);
        }
    }

    private Vector3 GetAvoidanceDirection(Vector3 originalDirection)
    {
        Vector3[] alternatives = {
            Quaternion.Euler(0, 45, 0) * originalDirection,
            Quaternion.Euler(0, -45, 0) * originalDirection,
            Quaternion.Euler(0, 90, 0) * originalDirection,
            Quaternion.Euler(0, -90, 0) * originalDirection
        };
        
        foreach (var dir in alternatives)
        {
            if (!Physics.Raycast(transform.position, dir, avoidanceRadius, obstacleLayer))
            {
                return dir;
            }
        }
        
        return -originalDirection;
    }

    // Metodo per fermare il movimento
    protected void StopMovement()
    {
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
    }
}

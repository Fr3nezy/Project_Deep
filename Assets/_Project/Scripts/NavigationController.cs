using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gestisce la navigazione tramite NavMeshAgent e fornisce velocity per animazioni.
/// </summary>
public class NavigationController : MonoBehaviour
{
    [Header("Navigation Settings")]
    public float baseSpeed = 2f;
    public float yOffset = 0f;  // Offset Y per pesci che nuotano a diverse altezze
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 5f;
    public bool smoothRotation = true;
    
    [Header("References")]
    public Animator animator;
    
    private NavMeshAgent agent;
    private Vector3 currentVelocity;
    private Vector3 targetPosition;
    private bool hasDestination = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        // Configurazione NavMeshAgent per movimento underwater
        agent.speed = baseSpeed;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;
        
        // Disabilita rotation automatica (la gestiamo manualmente)
        agent.updateRotation = false;
        agent.updateUpAxis = false;
    }

    void Update()
    {
        // Leggi velocity corrente da NavMeshAgent
        currentVelocity = agent.velocity;
        
        // Gestione rotazione smooth
        if (smoothRotation && currentVelocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentVelocity.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                Time.deltaTime * rotationSpeed
            );
        }
        
        // Applica offset Y per profondità di nuoto
        if (yOffset != 0f && agent.isOnNavMesh)
        {
            Vector3 navPos = agent.nextPosition;
            navPos.y += yOffset;
            transform.position = navPos;
        }
        
        // Aggiorna animator
        UpdateAnimator();
        
        // Check se arrivato a destinazione
        if (hasDestination && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                hasDestination = false;
            }
        }
    }

    /// <summary>
    /// Imposta destinazione per NavMeshAgent.
    /// </summary>
    public void SetDestination(Vector3 target)
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"{name}: Agent not on NavMesh, cannot set destination");
            return;
        }
        
        targetPosition = target;
        agent.SetDestination(target);
        hasDestination = true;
        agent.isStopped = false;
    }

    /// <summary>
    /// Ferma il movimento.
    /// </summary>
    public void Stop()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            hasDestination = false;
        }
    }

    /// <summary>
    /// Riprende il movimento.
    /// </summary>
    public void Resume()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    /// <summary>
    /// Cambia velocità temporaneamente.
    /// </summary>
    public void SetSpeed(float speed)
    {
        agent.speed = speed;
    }

    /// <summary>
    /// Resetta velocità a quella base.
    /// </summary>
    public void ResetSpeed()
    {
        agent.speed = baseSpeed;
    }

    /// <summary>
    /// Ritorna il vettore velocità corrente.
    /// </summary>
    public Vector3 GetVelocity()
    {
        return currentVelocity;
    }

    /// <summary>
    /// Ritorna se l'agente è arrivato a destinazione.
    /// </summary>
    public bool HasReachedDestination()
    {
        return !hasDestination;
    }

    /// <summary>
    /// Ritorna se l'agente è sul NavMesh.
    /// </summary>
    public bool IsOnNavMesh()
    {
        return agent.isOnNavMesh;
    }

    private void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", currentVelocity.magnitude);
            animator.SetFloat("VelocityX", currentVelocity.x);
            animator.SetFloat("VelocityZ", currentVelocity.z);
        }
    }

    // Debug visualization
    void OnDrawGizmos()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        
        // Percorso NavMesh
        Gizmos.color = Color.green;
        var path = agent.path;
        if (path != null && path.corners.Length > 1)
        {
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
            }
        }
        
        // Velocità corrente
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, currentVelocity);
        
        // Destinazione
        if (hasDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
        }
    }
}

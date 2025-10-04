using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Classe base per entità acquatiche con sistema di movimento verticale override-able.
/// </summary>
public abstract class AquaticEntity : MonoBehaviour
{
    [Header("Parametri Comuni")]
    public float wanderRadius = 10f;
    public float wanderInterval = 3f;
    
    [Header("Vertical Movement")]
    public bool enableVerticalMovement = true;
    public float minHeight = 0f;
    public float maxHeight = 5f;
    public float verticalSpeed = 1f;
    public float heightCheckRadius = 0.5f;  // Raggio per check ostacoli verticali
    
    [Header("Detection")]
    public LayerMask obstacleLayer = -1;
    
    protected Vector3 wanderTarget;
    private float nextWanderTime;
    protected NavigationController navigation;
    
    // Vertical movement state
    protected float currentHeight = 0f;
    protected float targetHeight = 0f;
    private float groundLevel = 0f;

    protected virtual void Start()
    {
        navigation = GetComponent<NavigationController>();
        if (navigation == null)
        {
            navigation = gameObject.AddComponent<NavigationController>();
            Debug.LogWarning($"{name}: NavigationController not found, added automatically");
        }
        
        // Inizializza altezza
        InitializeHeight();
        ScheduleNextWander();
    }

    protected virtual void Update()
    {
        // Aggiorna wander target periodicamente
        if (Time.time >= nextWanderTime)
        {
            PickWanderTarget();
        }
        
        // Sistema movimento verticale
        if (enableVerticalMovement)
        {
            UpdateVerticalMovement();
        }
    }

    #region Vertical Movement System

    /// <summary>
    /// Inizializza l'altezza di partenza. Override per comportamenti custom.
    /// </summary>
    protected virtual void InitializeHeight()
    {
        UpdateGroundLevel();
        currentHeight = Random.Range(minHeight, maxHeight * 0.5f);
        targetHeight = currentHeight;
    }

    /// <summary>
    /// Update loop per movimento verticale. Chiamato automaticamente se enableVerticalMovement = true.
    /// </summary>
    private void UpdateVerticalMovement()
    {
        UpdateGroundLevel();
        
        // Chiama metodo override-able per decidere target height
        DetermineTargetHeight();
        
        // Valida target height contro ostacoli
        targetHeight = ValidateHeightAgainstObstacles(targetHeight);
        
        // Smooth movement verso target
        currentHeight = Mathf.MoveTowards(
            currentHeight,
            targetHeight,
            verticalSpeed * Time.deltaTime
        );
        
        // Applica l'altezza al transform
        ApplyVerticalPosition();
    }

    /// <summary>
    /// OVERRIDE QUESTO per comportamenti verticali custom nelle classi figlie.
    /// Default: cambio casuale ogni tot secondi.
    /// </summary>
    protected virtual void DetermineTargetHeight()
    {
        // Comportamento default: cambio casuale ogni wanderInterval
        if (Time.time >= nextWanderTime)
        {
            targetHeight = Random.Range(minHeight, maxHeight);
        }
    }

    /// <summary>
    /// Aggiorna il ground level basandosi sul NavMesh.
    /// </summary>
    private void UpdateGroundLevel()
    {
        NavMeshAgent agent = navigation.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            groundLevel = agent.nextPosition.y;
        }
    }

    /// <summary>
    /// Valida che l'altezza target non collida con ostacoli sopra/sotto.
    /// Tiene conto del NavMesh e dei NavMeshObstacles.
    /// </summary>
    private float ValidateHeightAgainstObstacles(float desiredHeight)
    {
        Vector3 basePosition = new Vector3(
            transform.position.x,
            groundLevel,
            transform.position.z
        );
        
        // Check ceiling (ostacoli sopra)
        float checkHeight = Mathf.Max(desiredHeight, currentHeight) + 1f;
        RaycastHit ceilingHit;
        if (Physics.SphereCast(
            basePosition, 
            heightCheckRadius, 
            Vector3.up, 
            out ceilingHit, 
            checkHeight, 
            obstacleLayer))
        {
            // C'è un ostacolo sopra, limita altezza
            float maxSafeHeight = ceilingHit.distance - heightCheckRadius - 0.2f;
            desiredHeight = Mathf.Min(desiredHeight, maxSafeHeight);
        }
        
        // Check floor (ostacoli sotto - es. rocce che sporgono)
        RaycastHit floorHit;
        if (Physics.SphereCast(
            basePosition + Vector3.up * desiredHeight, 
            heightCheckRadius, 
            Vector3.down, 
            out floorHit, 
            desiredHeight, 
            obstacleLayer))
        {
            // C'è un ostacolo sotto, alza altezza minima
            float minSafeHeight = desiredHeight - floorHit.distance + heightCheckRadius + 0.2f;
            desiredHeight = Mathf.Max(desiredHeight, minSafeHeight);
        }
        
        // Clamp finale nel range permesso
        return Mathf.Clamp(desiredHeight, minHeight, maxHeight);
    }

    /// <summary>
    /// Applica la posizione verticale al transform.
    /// </summary>
    private void ApplyVerticalPosition()
    {
        Vector3 pos = transform.position;
        pos.y = groundLevel + currentHeight;
        transform.position = pos;
    }

    /// <summary>
    /// Imposta manualmente un target height. Utile per comportamenti reattivi.
    /// </summary>
    protected void SetTargetHeight(float height)
    {
        targetHeight = Mathf.Clamp(height, minHeight, maxHeight);
    }

    /// <summary>
    /// Forza un cambio immediato di altezza (senza interpolazione).
    /// </summary>
    protected void SetHeightImmediate(float height)
    {
        currentHeight = Mathf.Clamp(height, minHeight, maxHeight);
        targetHeight = currentHeight;
        ApplyVerticalPosition();
    }

    /// <summary>
    /// Ritorna l'altezza corrente sopra il ground level.
    /// </summary>
    protected float GetCurrentHeight()
    {
        return currentHeight;
    }

    /// <summary>
    /// Check se può muoversi a una certa altezza senza ostacoli.
    /// </summary>
    protected bool CanReachHeight(float height)
    {
        float validatedHeight = ValidateHeightAgainstObstacles(height);
        return Mathf.Abs(validatedHeight - height) < 0.1f;
    }

    #endregion

    #region Navigation Helpers

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
            groundLevel, // Usa ground level per pathfinding
            origin.z + circle.y
        );
        ScheduleNextWander();
    }

    protected void MoveTo(Vector3 target)
    {
        if (navigation != null)
        {
            // NavMesh usa coordinate ground-level
            target.y = groundLevel;
            navigation.SetDestination(target);
        }
    }

    protected void Wander()
    {
        MoveTo(wanderTarget);
    }

    protected void StopMovement()
    {
        if (navigation != null)
        {
            navigation.Stop();
        }
    }

    protected void SetSpeed(float speed)
    {
        if (navigation != null)
        {
            navigation.SetSpeed(speed);
        }
    }

    protected void ResetSpeed()
    {
        if (navigation != null)
        {
            navigation.ResetSpeed();
        }
    }

    #endregion

    #region Debug Visualization

    protected virtual void OnDrawGizmos()
    {
        if (!enableVerticalMovement) return;
        
        Vector3 basePos = transform.position;
        basePos.y = groundLevel;
        
        // Range verticale permesso
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawLine(basePos + Vector3.up * minHeight, basePos + Vector3.up * maxHeight);
        
        // Min/Max markers
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(basePos + Vector3.up * minHeight, 0.2f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(basePos + Vector3.up * maxHeight, 0.2f);
        
        // Current height
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        
        // Target height
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(basePos + Vector3.up * targetHeight, 0.25f);
        Gizmos.DrawLine(transform.position, basePos + Vector3.up * targetHeight);
        
        // Check radius per ostacoli
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, heightCheckRadius);
    }

    #endregion
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestisce attacco e damage dealing.
/// Usato da: Fish (vs Plankton), Leviathan (vs Fish/Player), Player (vs All).
/// </summary>
public class AttackComponent : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 2f;
    
    [Header("Target Settings")]
    [SerializeField] private LayerMask targetLayers = -1;
    [SerializeField] private string[] validTargetTags = { "Fish", "Player", "Plankton" };
    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private LayerMask obstacleLayers;
    
    [Header("Attack Behavior")]
    [SerializeField] private bool autoAttackNearestTarget = false;
    [SerializeField] private float autoAttackCheckInterval = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = true;
    
    // Events
    public UnityEvent<Entity> OnAttackExecuted;
    public UnityEvent<Entity> OnTargetKilled;
    public UnityEvent OnAttackOnCooldown;
    
    // State
    private float lastAttackTime = -999f;
    private float nextAttackTime = 0f; // ← AGGIUNTO per HuntComponent
    private Entity currentTarget = null;
    private float nextAutoAttackCheck = 0f;
    
    // Component references
    private Entity selfEntity;
    
    void Awake()
    {
        selfEntity = GetComponent<Entity>();
    }
    
    void Update()
    {
        if (autoAttackNearestTarget)
        {
            UpdateAutoAttack();
        }
    }
    
    /// <summary>
    /// Auto-attack nearest valid target.
    /// </summary>
    private void UpdateAutoAttack()
    {
        if (Time.time < nextAutoAttackCheck) return;
        nextAutoAttackCheck = Time.time + autoAttackCheckInterval;
        
        Entity nearestTarget = FindNearestTarget();
        if (nearestTarget != null)
        {
            TryAttack(nearestTarget);
        }
    }
    
    /// <summary>
    /// Try attack target (chiamato da HuntComponent con Entity parameter).
    /// </summary>
    public void TryAttack(Entity target)
    {
        if (!IsAttackReady()) 
        {
            OnAttackOnCooldown?.Invoke();
            return;
        }
        
        if (target == null || target.IsDead()) return;
        
        float distance = Vector3.Distance(transform.position, target.transform.position);
        
        if (distance <= attackRange)
        {
            if (requireLineOfSight && !HasLineOfSight(target))
                return;
            
            PerformAttack(target);
            lastAttackTime = Time.time;
            nextAttackTime = Time.time + attackCooldown;
        }
    }
    
    /// <summary>
    /// Try attack current target (overload without parameter).
    /// </summary>
    public bool TryAttack()
    {
        if (currentTarget == null)
        {
            currentTarget = FindNearestTarget();
        }
        
        if (currentTarget == null) return false;
        
        TryAttack(currentTarget);
        return true;
    }
    
    /// <summary>
    /// Perform attack (internal method).
    /// </summary>
    private void PerformAttack(Entity target)
    {
        currentTarget = target;
        
        if (showDebugLogs)
            Debug.Log($"⚔️ {gameObject.name} attacks {target.GetEntityName()} for {damage} damage!");
        
        // Infliggi danno
        bool wasAlive = target.IsAlive();
        target.TakeDamage(damage, selfEntity);
        
        OnAttackExecuted?.Invoke(target);
        
        // Check se ha ucciso il target
        if (wasAlive && target.IsDead())
        {
            OnTargetKilled?.Invoke(target);
            
            if (showDebugLogs)
                Debug.Log($"💀 {gameObject.name} killed {target.GetEntityName()}!");
            
            currentTarget = null;
        }
    }
    
    /// <summary>
    /// Trova target più vicino valido.
    /// </summary>
    private Entity FindNearestTarget()
    {
        Entity nearest = null;
        float minDistance = attackRange;
        
        foreach (string tag in validTargetTags)
        {
            GameObject[] potentialTargets = GameObject.FindGameObjectsWithTag(tag);
            
            foreach (GameObject targetObj in potentialTargets)
            {
                if (targetObj == gameObject) continue;
                
                Entity targetEntity = targetObj.GetComponent<Entity>();
                if (targetEntity == null || targetEntity.IsDead()) continue;
                
                if (!IsValidTarget(targetEntity)) continue;
                
                float distance = Vector3.Distance(transform.position, targetObj.transform.position);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = targetEntity;
                }
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Verifica se target è valido.
    /// </summary>
    protected virtual bool IsValidTarget(Entity target)
    {
        if (target == null || target.IsDead()) return false;
        if (target == selfEntity) return false;
        
        // Non attaccare stessa faction (opzionale, può essere rimosso)
        // if (selfEntity != null && target.GetEntityType() == selfEntity.GetEntityType())
        //     return false;
        
        return true;
    }
    
    /// <summary>
    /// Check line of sight.
    /// </summary>
    private bool HasLineOfSight(Entity target)
    {
        if (target == null) return false;
        
        Vector3 direction = target.transform.position - transform.position;
        float distance = direction.magnitude;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction.normalized, out hit, distance, obstacleLayers))
        {
            return false;
        }
        
        return true;
    }
    
    #region Public Getters/Setters
    
    /// <summary>
    /// Check se attack è pronto (cooldown finished).
    /// </summary>
    public bool IsAttackReady()
    {
        return Time.time >= lastAttackTime + attackCooldown;
    }
    
    /// <summary>
    /// Get cooldown remaining.
    /// </summary>
    public float GetCooldownRemaining()
    {
        float remaining = (lastAttackTime + attackCooldown) - Time.time;
        return Mathf.Max(remaining, 0f);
    }
    
    /// <summary>
    /// Set target manualmente.
    /// </summary>
    public void SetTarget(Entity target)
    {
        currentTarget = target;
    }
    
    /// <summary>
    /// Clear target.
    /// </summary>
    public void ClearTarget()
    {
        currentTarget = null;
    }
    
    /// <summary>
    /// Reset cooldown (instant attack ready).
    /// </summary>
    public void ResetCooldown()
    {
        lastAttackTime = -999f;
        nextAttackTime = 0f;
    }
    
    // Setters per EntityConfig
    public void SetDamage(float dmg) => damage = dmg;
    public void SetAttackRange(float range) => attackRange = range;
    public void SetCooldown(float cooldown) => attackCooldown = cooldown;
    
    // Getters
    public float GetDamage() => damage;
    public float GetAttackRange() => attackRange;
    public float GetCooldown() => attackCooldown;
    public Entity GetCurrentTarget() => currentTarget;
    public bool HasTarget() => currentTarget != null && currentTarget.IsAlive();
    
    #endregion
    
    #region Debug Visualization
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        if (!Application.isPlaying) return;
        
        // Attack range
        Gizmos.color = IsAttackReady() ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Line to target
        if (currentTarget != null && currentTarget.IsAlive())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            
            #if UNITY_EDITOR
            Vector3 midPoint = (transform.position + currentTarget.transform.position) / 2f;
            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = distance <= attackRange ? Color.green : Color.red;
            style.fontSize = 10;
            
            UnityEditor.Handles.Label(midPoint, $"→ {distance:F1}m", style);
            #endif
        }
        
        // Cooldown indicator
        if (!IsAttackReady())
        {
            #if UNITY_EDITOR
            Vector3 labelPos = transform.position + Vector3.up * 2.6f;
            float cooldownRemaining = GetCooldownRemaining();
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.red;
            style.fontSize = 11;
            style.fontStyle = FontStyle.Bold;
            
            UnityEditor.Handles.Label(labelPos, $"⏳ {cooldownRemaining:F1}s", style);
            #endif
        }
    }
    
    #endregion
}

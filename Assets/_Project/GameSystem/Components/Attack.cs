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
    /// Tenta attacco su target specifico.
    /// </summary>
    public bool TryAttack(Entity target)
    {
        if (target == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[Attack] {gameObject.name}: Target is null");
            return false;
        }
        
        if (target.IsDead())
        {
            if (showDebugLogs)
                Debug.Log($"[Attack] {gameObject.name}: Target {target.GetEntityName()} is already dead");
            return false;
        }
        
        // Cooldown check
        if (!IsAttackReady())
        {
            OnAttackOnCooldown?.Invoke();
            return false;
        }
        
        // Range check
        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance > attackRange)
        {
            if (showDebugLogs)
                Debug.Log($"[Attack] {gameObject.name}: Target {target.GetEntityName()} out of range ({distance:F2}m > {attackRange}m)");
            return false;
        }
        
        // Line of sight check
        if (requireLineOfSight && !HasLineOfSight(target))
        {
            if (showDebugLogs)
                Debug.Log($"[Attack] {gameObject.name}: No line of sight to {target.GetEntityName()}");
            return false;
        }
        
        // Execute attack
        ExecuteAttack(target);
        return true;
    }
    
    /// <summary>
    /// Tenta attacco su target corrente.
    /// </summary>
    public bool TryAttack()
    {
        if (currentTarget == null)
        {
            currentTarget = FindNearestTarget();
        }
        
        return TryAttack(currentTarget);
    }
    
    /// <summary>
    /// Esegue attacco su target.
    /// </summary>
    private void ExecuteAttack(Entity target)
    {
        lastAttackTime = Time.time;
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
        
        // Non attaccare stessa faction
        if (selfEntity != null && target.GetEntityType() == selfEntity.GetEntityType())
            return false;
        
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
    
    public bool IsAttackReady()
    {
        return Time.time >= lastAttackTime + attackCooldown;
    }
    
    public float GetCooldownRemaining()
    {
        float remaining = (lastAttackTime + attackCooldown) - Time.time;
        return Mathf.Max(remaining, 0f);
    }
    
    public void SetTarget(Entity target)
    {
        currentTarget = target;
    }
    
    public void ClearTarget()
    {
        currentTarget = null;
    }
    
    public void ResetCooldown()
    {
        lastAttackTime = -999f;
    }
    
    #region Getters
    
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

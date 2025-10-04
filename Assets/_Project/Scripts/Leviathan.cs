using UnityEngine;

public class Leviathan : AquaticEntity
{
    [Header("Parametri Leviatano")]
    public string fishTag = "Fish";
    public string playerTag = "Player";
    public float detectionRadius = 15f;
    public float huntSpeed = 3.5f;
    public float attackRange = 2f;
    public float attackCooldown = 3f;
    public LayerMask gameLightLayer = 1 << 11;
    public LayerMask playerLayer = 1 << 8;
    
    [Header("Vertical Hunting Behavior")]
    public float huntingHeightAdjustment = 0.2f;  // Aggiusta altezza per matchare preda

    private GameObject currentTarget;
    private float lastAttackTime;
    private bool huntingPlayer = false;

    protected override void Update()
    {
        base.Update();
        
        CheckForPlayer();
        
        if (!huntingPlayer && currentTarget == null)
        {
            ScanForFish();
        }
        
        if (currentTarget != null)
        {
            Hunt();
        }
        else
        {
            Wander();
            ResetSpeed();
        }
    }

    /// <summary>
    /// OVERRIDE: Leviatano regola altezza per matchare il target.
    /// </summary>
    protected override void DetermineTargetHeight()
    {
        if (currentTarget != null)
        {
            // Match altezza del target (player o pesce)
            float targetY = currentTarget.transform.position.y;
            float groundY = transform.position.y - GetCurrentHeight();
            float desiredHeight = Mathf.Clamp(
                targetY - groundY + huntingHeightAdjustment,
                minHeight,
                maxHeight
            );
            
            SetTargetHeight(desiredHeight);
        }
        else
        {
            // Wander con movimento verticale ampio
            base.DetermineTargetHeight();
        }
    }

    private void CheckForPlayer()
    {
        Collider[] nearbyPlayers = Physics.OverlapSphere(
            transform.position, 
            detectionRadius, 
            playerLayer
        );
        
        foreach (var playerCollider in nearbyPlayers)
        {
            Light playerLight = playerCollider.GetComponentInChildren<Light>();
            if (playerLight != null && playerLight.enabled && playerLight.intensity > 0.1f)
            {
                currentTarget = playerCollider.gameObject;
                huntingPlayer = true;
                return;
            }
            
            float dist = Vector3.Distance(transform.position, playerCollider.transform.position);
            if (dist <= detectionRadius * 0.5f)
            {
                currentTarget = playerCollider.gameObject;
                huntingPlayer = true;
                return;
            }
        }
        
        Collider[] lights = Physics.OverlapSphere(
            transform.position, 
            detectionRadius, 
            gameLightLayer
        );
        
        foreach (var lightCollider in lights)
        {
            Light lightComp = lightCollider.GetComponent<Light>();
            if (lightComp != null && lightComp.enabled)
            {
                Transform parent = lightCollider.transform.parent;
                if (parent != null && parent.CompareTag(playerTag))
                {
                    currentTarget = parent.gameObject;
                    huntingPlayer = true;
                    return;
                }
            }
        }
        
        huntingPlayer = false;
    }

    private void ScanForFish()
    {
        GameObject[] fishes = GameObject.FindGameObjectsWithTag(fishTag);
        GameObject closestFish = null;
        float minDist = detectionRadius;

        foreach (var fish in fishes)
        {
            if (fish == null) continue;
            
            float dist = Vector3.Distance(transform.position, fish.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closestFish = fish;
            }
        }

        if (closestFish != null)
        {
            currentTarget = closestFish;
            huntingPlayer = false;
        }
    }

    private void Hunt()
    {
        if (currentTarget == null)
            return;

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        float maxDist = huntingPlayer ? detectionRadius * 2f : detectionRadius * 1.2f;
        if (dist > maxDist)
        {
            currentTarget = null;
            huntingPlayer = false;
            ResetSpeed();
            return;
        }

        SetSpeed(huntSpeed);
        MoveTo(currentTarget.transform.position);

        if (dist <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            Attack();
        }
    }

    private void Attack()
    {
        lastAttackTime = Time.time;
        
        if (huntingPlayer)
        {
            Debug.Log("⚡ LEVIATHAN ATTACKS PLAYER! ⚡");
        }
        else if (currentTarget != null)
        {
            Debug.Log($"🐟 Leviathan ate: {currentTarget.name}");
            Destroy(currentTarget);
        }
        
        currentTarget = null;
        huntingPlayer = false;
    }
}

// File: Leviathan.cs (VERSIONE CORRETTA - Player Attraction Fix)
using UnityEngine;

public class Leviathan : AquaticEntity
{
    [Header("Parametri Leviatano")]
    public string fishTag = "Fish";
    public string playerTag = "Player";
    public float detectionRadius = 15f;      // Aumentato
    public float huntSpeed = 3f;
    public float attackRange = 2f;
    public float attackCooldown = 3f;
    public LayerMask gameLightLayer = 1 << 11; // Layer 11 = GameLight
    public LayerMask playerLayer = 1 << 8;     // Layer 8 = Player

    private GameObject currentTarget;
    private float lastAttackTime;
    private bool huntingPlayer = false;

    protected override void Update()
    {
        base.Update();
        
        // 1. PRIORITÀ: Player (sempre, se vicino o con luce)
        CheckForPlayer();
        
        if (!huntingPlayer && currentTarget == null)
        {
            // 2. Se non insegue player, cerca pesci
            ScanForFish();
        }
        
        if (currentTarget != null)
        {
            Hunt();
        }
        else
        {
            // 3. Wander normale
            MoveTowards(wanderTarget, moveSpeed);
        }
    }

    private void CheckForPlayer()
    {
        // Metodo 1: Player nelle vicinanze (con o senza luce)
        Collider[] nearbyPlayers = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);
        
        foreach (var playerCollider in nearbyPlayers)
        {
            Debug.Log($"Leviathan detected PLAYER nearby: {playerCollider.name}");
            
            // Controlla se il player ha luce accesa
            Light playerLight = playerCollider.GetComponentInChildren<Light>();
            if (playerLight != null && playerLight.enabled && playerLight.intensity > 0.1f)
            {
                Debug.Log("Player has LIGHT ON - Priority target!");
                currentTarget = playerCollider.gameObject;
                huntingPlayer = true;
                return;
            }
            
            // Anche senza luce, se molto vicino lo insegue
            float dist = Vector3.Distance(transform.position, playerCollider.transform.position);
            if (dist <= detectionRadius * 0.5f) // Metà del raggio di detection
            {
                Debug.Log("Player very close - attacking anyway!");
                currentTarget = playerCollider.gameObject;
                huntingPlayer = true;
                return;
            }
        }
        
        // Metodo 2: Cerca luci GameLight attive (backup)
        Collider[] lights = Physics.OverlapSphere(transform.position, detectionRadius, gameLightLayer);
        foreach (var lightCollider in lights)
        {
            Light lightComp = lightCollider.GetComponent<Light>();
            if (lightComp != null && lightComp.enabled)
            {
                // Trova il player parent di questa luce
                Transform parent = lightCollider.transform.parent;
                if (parent != null && parent.CompareTag(playerTag))
                {
                    Debug.Log($"Found GameLight from player: {parent.name}");
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
            Debug.Log($"Leviathan targeting fish: {closestFish.name}");
        }
    }

    private void Hunt()
    {
        if (currentTarget == null)
            return;

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        // Perde il target se troppo lontano
        float maxDist = huntingPlayer ? detectionRadius * 2f : detectionRadius * 1.2f;
        if (dist > maxDist)
        {
            Debug.Log($"Target {currentTarget.name} too far, losing target");
            currentTarget = null;
            huntingPlayer = false;
            return;
        }

        // Movimento verso target
        MoveTowards(currentTarget.transform.position, huntSpeed);

        // Attacco
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
            // Qui puoi aggiungere damage al player
        }
        else if (currentTarget != null)
        {
            Debug.Log($"🐟 Leviathan ate fish: {currentTarget.name}");
            Destroy(currentTarget);
        }
        
        currentTarget = null;
        huntingPlayer = false;
    }
    
    // Debug visuale
    void OnDrawGizmos()
    {
        // Raggio detection (verde)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Raggio attacco (rosso)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Target corrente
        if (currentTarget != null)
        {
            Gizmos.color = huntingPlayer ? Color.yellow : Color.cyan;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}

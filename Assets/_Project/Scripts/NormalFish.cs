// File: NormalFish.cs (VERSIONE MIGLIORATA - Light Hopping & Smart Flee)
using UnityEngine;
using System.Collections;

public class NormalFish : AquaticEntity
{
    [Header("Parametri Pesce Normale")]
    public float fleeDistance = 2f;
    public float fleeSpeed = 6f;
    public float fleeTime = 3f;              // Tempo di fuga dopo contatto
    public float lightAttractDistance = 12f;
    public string gameLightTag = "GameLight";
    public LayerMask playerLayer = 1 << 8;
    public LayerMask leviathanLayer = 1 << 10;

    [Header("Light Hopping Behavior")]
    public float lightStayDuration = 5f;     // Tempo vicino a una luce prima di annoiarsi
    public float lightCooldownTime = 8f;     // Tempo prima di poter ritornare alla stessa luce
    public float lightSwitchChance = 0.3f;   // Probabilità di cambiare luce anche se non annoiato

    private bool isFleeing = false;
    private bool isFleeingFromContact = false;
    private float fleeEndTime;
    private GameObject currentTargetLight;
    private float lightStartTime;
    private System.Collections.Generic.Dictionary<GameObject, float> lightCooldowns = 
        new System.Collections.Generic.Dictionary<GameObject, float>();

    protected override void Update()
    {
        base.Update();
        
        // Gestisce fine della fuga da contatto
        if (isFleeingFromContact && Time.time >= fleeEndTime)
        {
            isFleeingFromContact = false;
            Debug.Log($"{name}: Finished fleeing from contact");
        }
        
        isFleeing = false;
        
        // 1. PRIORITÀ: Scappa da minacce immediate
        if (!isFleeingFromContact)
        {
            CheckForThreats();
        }
        else
        {
            // Continua a fuggire velocemente
            ContinueFleeing();
            return;
        }
        
        if (!isFleeing)
        {
            // 2. Se non in fuga, gestisci comportamento luci
            ManageLightBehavior();
        }
        
        // Pulisce cooldown scaduti
        CleanupLightCooldowns();
    }

    private void CheckForThreats()
    {
        Vector3 fleeDirection = Vector3.zero;
        bool foundThreat = false;
        bool playerContact = false;
        
        // Rileva player
        Collider[] playerThreats = Physics.OverlapSphere(transform.position, fleeDistance, playerLayer);
        foreach (var threat in playerThreats)
        {
            Vector3 dirAway = (transform.position - threat.transform.position).normalized;
            fleeDirection += dirAway * 2f; // Priorità alta per player
            foundThreat = true;
            playerContact = true;
            Debug.Log($"{name}: Detected PLAYER contact - initiating extended flee");
        }
        
        // Rileva leviatano
        Collider[] leviathanThreats = Physics.OverlapSphere(transform.position, fleeDistance * 1.2f, leviathanLayer);
        foreach (var threat in leviathanThreats)
        {
            Vector3 dirAway = (transform.position - threat.transform.position).normalized;
            fleeDirection += dirAway * 1.5f;
            foundThreat = true;
        }

        if (foundThreat)
        {
            isFleeing = true;
            Vector3 fleeTarget = transform.position + fleeDirection.normalized * 15f; // Fuga più lontana
            MoveTowards(fleeTarget, fleeSpeed);
            
            // Se è contatto con player, inizia fuga estesa
            if (playerContact)
            {
                StartExtendedFlee(fleeDirection.normalized);
            }
        }
    }

    private void StartExtendedFlee(Vector3 fleeDirection)
    {
        isFleeingFromContact = true;
        fleeEndTime = Time.time + fleeTime;
        currentTargetLight = null; // Dimentica la luce corrente
        
        // Imposta target di fuga lontano
        Vector3 fleeTarget = transform.position + fleeDirection * 20f;
        wanderTarget = fleeTarget; // Override del wander target
        
        Debug.Log($"{name}: Started extended flee until {fleeEndTime}");
    }

    private void ContinueFleeing()
    {
        // Continua a muoverti velocemente lontano
        MoveTowards(wanderTarget, fleeSpeed);
        isFleeing = true;
    }

    private void ManageLightBehavior()
    {
        // Se abbiamo una luce target, controlliamo se è ancora valida
        if (currentTargetLight != null)
        {
            float timeAtCurrentLight = Time.time - lightStartTime;
            float distanceToCurrentLight = Vector3.Distance(transform.position, currentTargetLight.transform.position);
            
            // Condizioni per cambiare luce:
            // 1. Troppo tempo alla stessa luce
            // 2. Probabilità casuale di cambiare
            // 3. Luce troppo lontana
            bool shouldSwitchLight = timeAtCurrentLight > lightStayDuration ||
                                   Random.value < lightSwitchChance * Time.deltaTime ||
                                   distanceToCurrentLight > lightAttractDistance * 1.5f;
                                   
            if (shouldSwitchLight)
            {
                // Aggiungi cooldown per la luce corrente
                lightCooldowns[currentTargetLight] = Time.time + lightCooldownTime;
                Debug.Log($"{name}: Getting bored of light {currentTargetLight.name}, switching...");
                currentTargetLight = null;
            }
        }
        
        // Se non abbiamo una luce target, cercane una nuova
        if (currentTargetLight == null)
        {
            GameObject newLight = FindBestAvailableLight();
            if (newLight != null)
            {
                currentTargetLight = newLight;
                lightStartTime = Time.time;
                Debug.Log($"{name}: Switching to light {newLight.name}");
            }
        }
        
        // Movimento verso luce o wander
        if (currentTargetLight != null)
        {
            float distToLight = Vector3.Distance(transform.position, currentTargetLight.transform.position);
            if (distToLight <= lightAttractDistance)
            {
                MoveTowards(currentTargetLight.transform.position, moveSpeed * 0.9f);
                return;
            }
        }
        
        // Nessuna luce disponibile, wander normale
        MoveTowards(wanderTarget, moveSpeed * 0.7f);
    }

    private GameObject FindBestAvailableLight()
    {
        GameObject[] allLights = GameObject.FindGameObjectsWithTag(gameLightTag);
        GameObject bestLight = null;
        float bestScore = float.MinValue;

        foreach (var light in allLights)
        {
            if (light == null) continue;
            
            // Salta luci in cooldown
            if (lightCooldowns.ContainsKey(light) && lightCooldowns[light] > Time.time)
                continue;
            
            float distance = Vector3.Distance(transform.position, light.transform.position);
            if (distance > lightAttractDistance) continue;
            
            // Calcola score: più vicino è meglio, ma con elementi casuali
            float score = (lightAttractDistance - distance) + Random.Range(-2f, 3f);
            
            // Bonus se è diversa dalla luce precedente
            if (light != currentTargetLight)
                score += 2f;
            
            if (score > bestScore)
            {
                bestScore = score;
                bestLight = light;
            }
        }

        return bestLight;
    }

    private void CleanupLightCooldowns()
    {
        var keysToRemove = new System.Collections.Generic.List<GameObject>();
        foreach (var kvp in lightCooldowns)
        {
            if (kvp.Key == null || kvp.Value < Time.time)
                keysToRemove.Add(kvp.Key);
        }
        
        foreach (var key in keysToRemove)
        {
            lightCooldowns.Remove(key);
        }
    }

    // Debug visuale migliorato
    void OnDrawGizmos()
    {
        // Raggio fuga player (rosso)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeDistance);
        
        // Raggio attrazione luce (verde)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, lightAttractDistance);
        
        // Connessione alla luce target
        if (currentTargetLight != null)
        {
            Gizmos.color = isFleeingFromContact ? Color.gray : Color.cyan;
            Gizmos.DrawLine(transform.position, currentTargetLight.transform.position);
            
            // Mostra quanto tempo rimane alla luce corrente
            float timeRemaining = lightStayDuration - (Time.time - lightStartTime);
            if (timeRemaining > 0 && Application.isPlaying)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"{currentTargetLight.name}\nTime: {timeRemaining:F1}s"
                );
            }
        }
        
        // Stato fuga
        if (isFleeingFromContact)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 3f);
        }
    }
}

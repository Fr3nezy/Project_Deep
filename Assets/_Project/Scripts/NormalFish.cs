using UnityEngine;

public class NormalFish : AquaticEntity
{
    [Header("Parametri Pesce Normale")]
    public float fleeDistance = 2f;
    public float fleeSpeed = 5f;
    public float fleeTime = 3f;
    public float lightAttractDistance = 12f;
    public string gameLightTag = "GameLight";
    public LayerMask playerLayer = 1 << 8;
    public LayerMask leviathanLayer = 1 << 10;

    [Header("Light Hopping Behavior")]
    public float lightStayDuration = 5f;
    public float lightCooldownTime = 8f;
    public float lightSwitchChance = 0.3f;
    
    [Header("Vertical Behavior")]
    public float diveDepthWhenThreatened = 0.5f;  // Scende quando minacciato
    public float surfaceHeightWhenSeekingLight = 0.8f;  // Percentuale maxHeight quando cerca luce

    private bool isFleeingFromContact = false;
    private float fleeEndTime;
    private GameObject currentTargetLight;
    private float lightStartTime;
    private System.Collections.Generic.Dictionary<GameObject, float> lightCooldowns = 
        new System.Collections.Generic.Dictionary<GameObject, float>();

    protected override void Update()
    {
        base.Update(); // Chiama il vertical movement system
        
        if (isFleeingFromContact && Time.time >= fleeEndTime)
        {
            isFleeingFromContact = false;
            ResetSpeed();
        }
        
        if (!isFleeingFromContact)
        {
            if (CheckForThreats())
            {
                return;
            }
            ManageLightBehavior();
        }
        else
        {
            ContinueFleeing();
        }
        
        CleanupLightCooldowns();
    }

    /// <summary>
    /// OVERRIDE: Comportamento verticale custom per pesci normali.
    /// </summary>
    protected override void DetermineTargetHeight()
    {
        // Comportamento 1: Scende verso il fondale quando minacciato
        if (isFleeingFromContact)
        {
            SetTargetHeight(diveDepthWhenThreatened);
            return;
        }
        
        // Comportamento 2: Sale verso l'alto quando cerca luce
        if (currentTargetLight != null)
        {
            float lightHeight = surfaceHeightWhenSeekingLight * maxHeight;
            SetTargetHeight(lightHeight);
            return;
        }
        
        // Comportamento 3: Altezza casuale durante wander (default behavior)
        base.DetermineTargetHeight();
    }

    private bool CheckForThreats()
    {
        Vector3 fleeDirection = Vector3.zero;
        bool foundThreat = false;
        bool playerContact = false;
        
        Collider[] playerThreats = Physics.OverlapSphere(transform.position, fleeDistance, playerLayer);
        foreach (var threat in playerThreats)
        {
            Vector3 dirAway = (transform.position - threat.transform.position).normalized;
            fleeDirection += dirAway * 2f;
            foundThreat = true;
            playerContact = true;
        }
        
        Collider[] leviathanThreats = Physics.OverlapSphere(
            transform.position, 
            fleeDistance * 1.5f, 
            leviathanLayer
        );
        foreach (var threat in leviathanThreats)
        {
            Vector3 dirAway = (transform.position - threat.transform.position).normalized;
            fleeDirection += dirAway * 1.5f;
            foundThreat = true;
        }

        if (foundThreat)
        {
            Vector3 fleeTarget = transform.position + fleeDirection.normalized * 15f;
            SetSpeed(fleeSpeed);
            MoveTo(fleeTarget);
            
            if (playerContact)
            {
                StartExtendedFlee(fleeDirection.normalized);
            }
            
            return true;
        }
        
        return false;
    }

    private void StartExtendedFlee(Vector3 fleeDirection)
    {
        isFleeingFromContact = true;
        fleeEndTime = Time.time + fleeTime;
        currentTargetLight = null;
        
        Vector3 fleeTarget = transform.position + fleeDirection * 20f;
        wanderTarget = fleeTarget;
        SetSpeed(fleeSpeed);
        MoveTo(fleeTarget);
        
        // Triggera comportamento "dive" (gestito in DetermineTargetHeight)
    }

    private void ContinueFleeing()
    {
        MoveTo(wanderTarget);
    }

    private void ManageLightBehavior()
    {
        if (currentTargetLight != null)
        {
            float timeAtCurrentLight = Time.time - lightStartTime;
            float distanceToCurrentLight = Vector3.Distance(
                transform.position, 
                currentTargetLight.transform.position
            );
            
            bool shouldSwitchLight = 
                timeAtCurrentLight > lightStayDuration ||
                Random.value < lightSwitchChance * Time.deltaTime ||
                distanceToCurrentLight > lightAttractDistance * 1.5f;
                                   
            if (shouldSwitchLight)
            {
                lightCooldowns[currentTargetLight] = Time.time + lightCooldownTime;
                currentTargetLight = null;
            }
        }
        
        if (currentTargetLight == null)
        {
            GameObject newLight = FindBestAvailableLight();
            if (newLight != null)
            {
                currentTargetLight = newLight;
                lightStartTime = Time.time;
            }
        }
        
        if (currentTargetLight != null)
        {
            float distToLight = Vector3.Distance(
                transform.position, 
                currentTargetLight.transform.position
            );
            
            if (distToLight <= lightAttractDistance)
            {
                MoveTo(currentTargetLight.transform.position);
                return;
            }
        }
        
        Wander();
    }

    private GameObject FindBestAvailableLight()
    {
        GameObject[] allLights = GameObject.FindGameObjectsWithTag(gameLightTag);
        GameObject bestLight = null;
        float bestScore = float.MinValue;

        foreach (var light in allLights)
        {
            if (light == null) continue;
            
            if (lightCooldowns.ContainsKey(light) && lightCooldowns[light] > Time.time)
                continue;
            
            float distance = Vector3.Distance(transform.position, light.transform.position);
            if (distance > lightAttractDistance) continue;
            
            float score = (lightAttractDistance - distance) + Random.Range(-2f, 3f);
            
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
}

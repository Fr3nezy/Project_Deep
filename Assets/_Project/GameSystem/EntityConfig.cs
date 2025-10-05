using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject che definisce TUTTI gli attributi di una specie.
/// Designer-friendly: crea nuove specie senza coding!
/// </summary>
[CreateAssetMenu(fileName = "EntityConfig", menuName = "Ecosystem/Entity Config")]
public class EntityConfig : ScriptableObject
{
    [Header("Identity")]
    public string speciesName = "Unknown";
    public EntityType entityType = EntityType.Unknown;
    public Sprite icon;
    
    [Header("Visuals")]
    public GameObject modelPrefab;
    public Vector3 modelScale = Vector3.one;
    public Material[] materials;
    
    [Header("Stats")]
    public float maxHealth = 10f;
    public float maxStamina = 100f;
    public float baseSpeed = 2f;
    public float mass = 1f;
    
    [Header("Diet")]
    public List<EntityType> preyTypes = new List<EntityType>();
    public List<EntityType> predatorTypes = new List<EntityType>();
    public float nutritionValue = 10f;
    public bool cannibal = false;
    
    [Header("Hunger")]
    public bool needsFood = true;
    public float hungerDecayRate = 0.5f;
    public float hungerThreshold = 40f;
    
    [Header("Hunt Behavior")]
    public bool canHunt = true;
    public float detectionRange = 10f;
    public float detectionAngle = 120f;
    public float chaseRange = 6f;
    public float chaseSpeed = 3f;
    public float stalkSpeed = 1.5f;
    
    [Header("Attack")]
    public bool canAttack = true;
    public float attackDamage = 10f;
    public float attackRange = 1.5f;
    public float attackCooldown = 2f;
    
    [Header("Fear")]
    public bool canFear = true;
    public float fearRadius = 5f;
    public float fearThreshold = 30f;
    public float fleeSpeedMultiplier = 1.5f;
    
    [Header("Movement")]
    public float wanderRadius = 10f;
    public float wanderSpeed = 0.8f;
    public bool avoidObstacles = true;
    
    /// <summary>
    /// Apply config a entity runtime.
    /// </summary>
    public void ApplyToEntity(GameObject entityObject)
    {
        Entity entity = entityObject.GetComponent<Entity>();
        if (entity == null) return;
        
        // Set entity type
        entity.SetEntityType(entityType);
        
        // Apply health
        HealthComponent health = entityObject.GetComponent<HealthComponent>();
        if (health != null)
        {
            health.SetMaxHealth(maxHealth);
            health.SetCurrentHealth(maxHealth);
        }
        
        // Apply stamina
        StaminaComponent stamina = entityObject.GetComponent<StaminaComponent>();
        if (stamina != null)
        {
            stamina.SetMaxStamina(maxStamina);
        }
        
        // Apply diet
        DietComponent diet = entityObject.GetComponent<DietComponent>();
        if (diet != null)
        {
            diet.preyTypes = new List<EntityType>(preyTypes);
            diet.predatorTypes = new List<EntityType>(predatorTypes);
            diet.nutritionValue = nutritionValue;
            diet.cannibal = cannibal;
        }
        
        // Apply hunger
        HungerComponent hunger = entityObject.GetComponent<HungerComponent>();
        if (hunger != null && needsFood)
        {
            hunger.SetBaseDecayRate(hungerDecayRate);
            hunger.SetHungryThreshold(hungerThreshold);
        }
        else if (hunger != null && !needsFood)
        {
            // Disable hunger se non serve
            hunger.enabled = false;
        }
        
        // Apply hunt
        HuntComponent hunt = entityObject.GetComponent<HuntComponent>();
        if (hunt != null && canHunt)
        {
            hunt.SetDetectionRange(detectionRange);
            hunt.SetDetectionAngle(detectionAngle);
            hunt.SetChaseRange(chaseRange);
            hunt.SetChaseSpeed(chaseSpeed);
            hunt.SetStalkSpeed(stalkSpeed);
        }
        else if (hunt != null && !canHunt)
        {
            hunt.enabled = false;
        }
        
        // Apply attack
        AttackComponent attack = entityObject.GetComponent<AttackComponent>();
        if (attack != null && canAttack)
        {
            attack.SetDamage(attackDamage);
            attack.SetAttackRange(attackRange);
            attack.SetCooldown(attackCooldown);
        }
        else if (attack != null && !canAttack)
        {
            attack.enabled = false;
        }
        
        // Apply fear
        FearComponent fear = entityObject.GetComponent<FearComponent>();
        if (fear != null && canFear)
        {
            fear.SetFearThreshold(fearThreshold);
            // TODO: Set fear radius per predator type
        }
        else if (fear != null && !canFear)
        {
            fear.enabled = false;
        }
        
        // Apply movement
        MovementController movement = entityObject.GetComponent<MovementController>();
        if (movement != null)
        {
            movement.SetBaseSpeed(baseSpeed);
            movement.SetWanderSpeed(wanderSpeed);
            movement.SetWanderRadius(wanderRadius);
        }
        
        // Apply visuals
        if (modelPrefab != null)
        {
            // Instantiate model as child
            Transform existingModel = entityObject.transform.Find("Model");
            if (existingModel != null)
            {
                Destroy(existingModel.gameObject);
            }
            
            GameObject model = Instantiate(modelPrefab, entityObject.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = modelScale;
        }
    }
}

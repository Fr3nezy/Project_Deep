using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Definisce dieta dell'entità: cosa caccia, da cosa fugge.
/// Sostituisce tag-based hunting!
/// </summary>
public class DietComponent : MonoBehaviour
{
    [Header("Prey (Cosa Caccio)")]
    [Tooltip("Entità che questa specie mangia")]
    public List<EntityType> preyTypes = new List<EntityType>();
    
    [Tooltip("Posso cacciare anche entità più grandi di me?")]
    public bool canHuntLarger = false;
    
    [Tooltip("Posso cacciare anche entità stessa specie? (cannibalismo)")]
    public bool cannibal = false;
    
    [Header("Predators (Da Chi Fuggo)")]
    [Tooltip("Entità che mi cacciano (trigger fear)")]
    public List<EntityType> predatorTypes = new List<EntityType>();
    
    [Header("Food Value")]
    [Tooltip("Valore nutritivo quando sono mangiato")]
    public float nutritionValue = 10f;
    
    [Header("Hunting Preferences")]
    [Tooltip("Priorità prey (0=lowest, 10=highest)")]
    public Dictionary<EntityType, int> preyPriority = new Dictionary<EntityType, int>();
    
    // Cache
    private Entity selfEntity;
    
    void Awake()
    {
        selfEntity = GetComponent<Entity>();
    }
    
    /// <summary>
    /// Verifica se target è prey valido.
    /// </summary>
    public bool IsValidPrey(Entity target)
    {
        if (target == null || target == selfEntity) return false;
        if (target.IsDead()) return false;
        
        EntityType targetType = target.GetEntityType();
        
        // Check se è nella lista prey
        if (!preyTypes.Contains(targetType))
            return false;
        
        // Check cannibalismo
        if (!cannibal && targetType == selfEntity.GetEntityType())
            return false;
        
        // Check size (optional)
        if (!canHuntLarger)
        {
            // TODO: Compare size/scale
        }
        
        return true;
    }
    
    /// <summary>
    /// Verifica se source è un predatore (trigger fear).
    /// </summary>
    public bool IsPredator(Entity source)
    {
        if (source == null || source == selfEntity) return false;
        
        EntityType sourceType = source.GetEntityType();
        return predatorTypes.Contains(sourceType);
    }
    
    /// <summary>
    /// Get priorità di target (per multi-prey scenarios).
    /// </summary>
    public int GetPreyPriority(EntityType type)
    {
        if (preyPriority.ContainsKey(type))
            return preyPriority[type];
        
        return 5; // Default medium priority
    }
    
    /// <summary>
    /// Get nutrition value (quanto valgo come cibo).
    /// </summary>
    public float GetNutritionValue()
    {
        return nutritionValue;
    }
}

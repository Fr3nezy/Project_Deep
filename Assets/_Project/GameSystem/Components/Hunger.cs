using UnityEngine;

/// <summary>
/// Gestisce fame per entità.
/// [STUB - Implementazione completa prossimo step]
/// </summary>
public class HungerComponent : MonoBehaviour
{
    [SerializeField] private float currentHunger = 0f;
    
    public void Feed(float amount)
    {
        currentHunger -= amount;
        currentHunger = Mathf.Max(currentHunger, 0);
        Debug.Log($"{gameObject.name} mangiato! Hunger: {currentHunger}");
    }
    
    public float GetCurrentHunger() => currentHunger;
}

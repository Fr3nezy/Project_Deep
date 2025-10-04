using UnityEngine;

/// <summary>
/// Definisce valore nutrizionale quando mangiato.
/// </summary>
public class FoodValue : MonoBehaviour
{
    [SerializeField] private float nutritionValue = 40f;
    
    public float GetValue() => nutritionValue;
    public void SetValue(float value) => nutritionValue = value;
}

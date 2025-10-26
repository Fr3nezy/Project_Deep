using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// ScriptableObject per configurare le statistiche del Player (Fame, Stamina, Salute).
    /// Permette di bilanciare il survival gameplay.
    /// </summary>
    [CreateAssetMenu(fileName = "Player Profile", menuName = "Deeploration/Player Profile")]
    public class PlayerProfile : ScriptableObject
    {
        [Header("Salute Player")]
        [Tooltip("Salute massima del Player")]
        [Range(50f, 500f)]
        public float maxHealth = 100f;

        [Header("Stamina (Sprint, Jump)")]
        [Tooltip("Stamina massima (per sprint e jump boost)")]
        [Range(50f, 200f)]
        public float maxStamina = 100f;

        [Tooltip("Costo stamina per secondo di sprint")]
        [Range(5f, 50f)]
        public float sprintCostPerSecond = 15f;

        [Tooltip("Costo stamina per jump boost")]
        [Range(5f, 40f)]
        public float jumpCost = 10f;

        [Tooltip("Velocità di recupero stamina per secondo")]
        [Range(1f, 20f)]
        public float staminaRecoveryRate = 10f;

        [Tooltip("Soglia sotto cui Player è 'stanco' (disabilita sprint?)")]
        [Range(0f, 50f)]
        public float exhaustThreshold = 20f;

        [Header("Fame (Survival)")]
        [Tooltip("Fame massima (sazio = 100, affamato = 0)")]
        [Range(50f, 200f)]
        public float maxHunger = 100f;

        [Tooltip("Velocità di perdita fame per secondo")]
        [Range(0.1f, 5f)]
        public float hungerRate = 0.5f;

        [Tooltip("Soglia sotto cui Player 'ha fame' (deb buff, UI)")]
        [Range(0f, 50f)]
        public float hungerThreshold = 25f;

        [Tooltip("Permetti morte per fame?")]
        public bool allowDeathByStarvation = true;

        [Tooltip("Se no death, danno per secondo con fame=0")]
        [Range(0f, 10f)]
        public float starvationDamageRate = 2f;

        [Header("UI / Feedback")]
        [Tooltip("Mostra UI per statistiche")]
        public bool showStatusUI = true;

        [Tooltip("Colore per stamina (barra UI)")]
        public Color staminaBarColor = Color.green;

        [Tooltip("Colore per fame (barra UI)")]
        public Color hungerBarColor = new Color(1f, 0.5f, 0f); // Orange

        [Tooltip("Colore per salute (barra UI)")]
        public Color healthBarColor = Color.red;
    }
}

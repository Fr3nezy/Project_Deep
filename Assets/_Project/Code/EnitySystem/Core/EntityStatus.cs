using UnityEngine;
using System;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Gestisce i parametri vitali (Salute, Fame, Stamina) di un'entità.
    /// Fornisce API pubbliche per la modifica sicura dei valori.
    /// Emette eventi per notificare cambiamenti di stato.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EntityStatus : MonoBehaviour
    {
        [Header("Configurazione")]
        [SerializeField, Tooltip("Profilo della creatura (ScriptableObject)")]
        private CreatureProfile profile;

        [Header("Debug Info (Read-Only)")]
        [SerializeField, Tooltip("ID unico dell'entità")]
        private string entityID;

        [SerializeField, Tooltip("Salute corrente")]
        private float currentHealth;

        [SerializeField, Tooltip("Stamina corrente")]
        private float currentStamina;

        [SerializeField, Tooltip("Fame corrente (100=sazio, 0=affamato)")]
        private float currentHunger;

        [SerializeField, Tooltip("Indica se l'entità è viva")]
        private bool isAlive = true;

        // Eventi pubblici
        public event Action<string, EntityType> OnEntityDied;
        public event Action<string, float> OnEntityHealthChanged;
        public event Action<string> OnStaminaDepleted;

        // Proprietà pubbliche di sola lettura
        public string EntityID => entityID;
        public EntityType EntityType => profile != null ? profile.entityType : EntityType.None;
        public CreatureProfile Profile => profile;
        public bool IsAlive => isAlive;

        private void Awake()
        {
            // Validazione profilo
            if (profile == null)
            {
                Debug.LogError($"[EntityStatus] CreatureProfile non assegnato su {gameObject.name}! L'entità non funzionerà correttamente.", this);
                enabled = false;
                return;
            }

            // Genera ID unico
            entityID = $"{profile.entityType}_{gameObject.GetInstanceID()}";
            
            // Inizializza parametri vitali
            InitializeStatus();
        }

        private void Update()
        {
            if (!isAlive) return;

            // Decremento automatico della fame
            DecreaseHunger(profile.hungerRate * Time.deltaTime);

            // Morte per fame
            if (currentHunger <= 0f)
            {
                ApplyDamage(profile.maxHealth); // Uccide l'entità
            }
        }

        /// <summary>
        /// Inizializza i parametri vitali ai valori massimi.
        /// </summary>
        private void InitializeStatus()
        {
            currentHealth = profile.maxHealth;
            currentStamina = profile.maxStamina;
            currentHunger = profile.maxHunger;
            isAlive = true;

            Debug.Log($"[EntityStatus] {profile.creatureName} (ID: {entityID}) inizializzato con successo.");
        }

        /// <summary>
        /// Resetta lo stato dell'entità (usato dal pooling).
        /// </summary>
        public void ResetStatus()
        {
            InitializeStatus();
        }

        #region Public API

        /// <summary>
        /// Applica danno all'entità. Se la salute scende a zero, attiva la morte.
        /// </summary>
        /// <param name="amount">Quantità di danno da applicare</param>
        public void ApplyDamage(float amount)
        {
            if (!isAlive) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnEntityHealthChanged?.Invoke(entityID, currentHealth);

            Debug.Log($"[EntityStatus] {profile.creatureName} ha ricevuto {amount} danni. Salute: {currentHealth}/{profile.maxHealth}");

            // Controlla morte
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Consuma cibo, riducendo la fame (aumentando il valore di hunger).
        /// </summary>
        /// <param name="amount">Quantità di cibo consumato</param>
        public void ConsumeFood(float amount)
        {
            if (!isAlive) return;

            currentHunger = Mathf.Min(profile.maxHunger, currentHunger + amount);
            Debug.Log($"[EntityStatus] {profile.creatureName} ha mangiato. Fame: {currentHunger}/{profile.maxHunger}");
        }

        /// <summary>
        /// Ottiene il valore corrente di un parametro vitale.
        /// </summary>
        /// <param name="type">Tipo di parametro da leggere</param>
        /// <returns>Valore corrente del parametro</returns>
        public float GetStatusValue(StatusType type)
        {
            return type switch
            {
                StatusType.Health => currentHealth,
                StatusType.Stamina => currentStamina,
                StatusType.Hunger => currentHunger,
                _ => 0f
            };
        }

        /// <summary>
        /// Tenta di utilizzare stamina. Ritorna true se la stamina è sufficiente.
        /// </summary>
        /// <param name="amount">Quantità di stamina da usare</param>
        /// <param name="deltaTime">Delta time per il calcolo del costo</param>
        /// <returns>True se la stamina è stata consumata, false se insufficiente</returns>
        public bool TryUseStamina(float amount, float deltaTime)
        {
            if (!isAlive) return false;

            float cost = amount * deltaTime;
            
            if (currentStamina >= cost)
            {
                currentStamina = Mathf.Max(0f, currentStamina - cost);
                return true;
            }
            else
            {
                // Stamina esaurita
                if (currentStamina > 0f)
                {
                    currentStamina = 0f;
                    OnStaminaDepleted?.Invoke(entityID);
                    Debug.Log($"[EntityStatus] {profile.creatureName} ha esaurito la stamina!");
                }
                return false;
            }
        }

        /// <summary>
        /// Recupera stamina nel tempo (usato durante il riposo).
        /// </summary>
        /// <param name="deltaTime">Delta time per il calcolo del recupero</param>
        public void RecoverStamina(float deltaTime)
        {
            if (!isAlive) return;

            currentStamina = Mathf.Min(profile.maxStamina, currentStamina + profile.staminaRecoveryRate * deltaTime);
        }

        /// <summary>
        /// Verifica se questa entità è una preda del tipo specificato.
        /// </summary>
        /// <param name="predatorType">Tipo di predatore da verificare</param>
        /// <returns>True se questa entità è preda del tipo specificato</returns>
        public bool IsPreyOf(EntityType predatorType)
        {
            if (profile == null) return false;
            return profile.predatorTypes.Contains(predatorType);
        }

        /// <summary>
        /// Verifica se questa entità può cacciare il tipo specificato.
        /// </summary>
        /// <param name="preyType">Tipo di preda da verificare</param>
        /// <returns>True se questa entità può cacciare il tipo specificato</returns>
        public bool CanHunt(EntityType preyType)
        {
            if (profile == null) return false;
            return profile.preyTypes.Contains(preyType);
        }

        /// <summary>
        /// Verifica se l'entità è affamata (sotto la soglia hungerThreshold).
        /// </summary>
        public bool IsHungry()
        {
            return currentHunger < profile.hungerThreshold;
        }

        /// <summary>
        /// Verifica se l'entità ha poca stamina (sotto la soglia staminaThreshold).
        /// </summary>
        public bool IsExhausted()
        {
            return currentStamina < profile.staminaThreshold;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Riduce la fame nel tempo.
        /// </summary>
        private void DecreaseHunger(float amount)
        {
            currentHunger = Mathf.Max(0f, currentHunger - amount);
        }

        /// <summary>
        /// Gestisce la morte dell'entità.
        /// </summary>
        private void Die()
        {
            if (!isAlive) return;

            isAlive = false;
            OnEntityDied?.Invoke(entityID, profile.entityType);

            Debug.Log($"[EntityStatus] {profile.creatureName} (ID: {entityID}) è morto!");

            // Qui potrebbe essere chiamato il pooler per disattivare l'entità
        }

        #endregion

        #region Debug Helpers

        /// <summary>
        /// Ritorna una stringa formattata con lo stato corrente per il debug.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"{profile.creatureName}\n" +
                   $"HP: {currentHealth:F0}/{profile.maxHealth:F0}\n" +
                   $"Stamina: {currentStamina:F0}/{profile.maxStamina:F0}\n" +
                   $"Hunger: {currentHunger:F0}/{profile.maxHunger:F0}";
        }

        /// <summary>
        /// Ritorna il valore normalizzato (0-1) di un parametro vitale.
        /// </summary>
        public float GetStatusNormalized(StatusType type)
        {
            return type switch
            {
                StatusType.Health => currentHealth / profile.maxHealth,
                StatusType.Stamina => currentStamina / profile.maxStamina,
                StatusType.Hunger => currentHunger / profile.maxHunger,
                _ => 0f
            };
        }

        #endregion

        private void OnValidate()
        {
            // Clamp dei valori nell'editor per evitare valori invalidi
            if (profile != null)
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, profile.maxHealth);
                currentStamina = Mathf.Clamp(currentStamina, 0f, profile.maxStamina);
                currentHunger = Mathf.Clamp(currentHunger, 0f, profile.maxHunger);
            }
        }
    }
}

using UnityEngine;
using System;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Gestisce le statistiche del Player (Fame, Stamina) per survival gameplay.
    /// Fame: Diminuisce col tempo, affetta vitalità e movimento.
    /// Stamina: Usata per sprint e jump boost, recupera automaticamente.
    /// </summary>
    public class PlayerStatus : MonoBehaviour
    {
        [Header("Profilo Player")]
        [SerializeField, Tooltip("Profilo del player (Statistiche personalizzabili)")]
        private PlayerProfile playerProfile;

        [Header("Debug Info (Read-Only)")]
        [SerializeField, Tooltip("Salute corrente")]
        private float currentHealth;

        [SerializeField, Tooltip("Stamina corrente (0-100)")]
        private float currentStamina;

        [SerializeField, Tooltip("Fame corrente (0-100). A 0 = morte o debuff")]
        private float currentHunger;

        [SerializeField, Tooltip("È vivo?")]
        private bool isAlive = true;

        // Eventi pubblici
        public event Action<string> OnStaminaDepleted;
        public event Action<string> OnHungerDepleted;
        public event Action OnPlayerDied;

        // Proprietà pubbliche
        public float CurrentHealth => currentHealth;
        public float MaxHealth => playerProfile.maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => playerProfile.maxStamina;
        public float CurrentHunger => currentHunger;
        public float MaxHunger => playerProfile.maxHunger;
        public bool IsAlive => isAlive;
        public PlayerProfile Profile => playerProfile;

        private void Awake()
        {
            if (playerProfile == null)
            {
                Debug.LogError("[PlayerStatus] PlayerProfile non assegnato! Disabilitato.", this);
                enabled = false;
                return;
            }

            InitializeStatus();
        }

        private void Update()
        {
            if (!isAlive) return;

            // Diminuisci fame col tempo
            DecreaseHunger(playerProfile.hungerRate * Time.deltaTime);

            // Recupera stamina col tempo
            RecoverStamina(playerProfile.staminaRecoveryRate * Time.deltaTime);

            // Fame = 0 → morte o debuff
            if (currentHunger <= 0f)
            {
                if (playerProfile.allowDeathByStarvation)
                {
                    Die();
                }
                else
                {
                    // Riduci salute se fame =0
                    ApplyDamage(playerProfile.starvationDamageRate * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Inizializza le statistiche ai valori massimi.
        /// </summary>
        private void InitializeStatus()
        {
            currentHealth = playerProfile.maxHealth;
            currentStamina = playerProfile.maxStamina;
            currentHunger = playerProfile.maxHunger;
            isAlive = true;

            Debug.Log($"[PlayerStatus] Player inizializzato con successo.");
        }

        /// <summary>
        /// Resetta le statistiche (usato per respawn o reload).
        /// </summary>
        public void ResetStatus()
        {
            InitializeStatus();
        }

        #region Public API (Simile EntityStatus)

        /// <summary>
        /// Applica danno al Player. Se salute = 0, attiva morte.
        /// </summary>
        public void ApplyDamage(float amount)
        {
            if (!isAlive) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);

            Debug.Log($"[PlayerStatus] Player ricevuto {amount} danno. Salute: {currentHealth}/{MaxHealth}");

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Riduce la fame (mangiare cibo).
        /// </summary>
        public void ConsumeFood(float amount)
        {
            if (!isAlive) return;

            currentHunger = Mathf.Min(MaxHunger, currentHunger + amount);
            Debug.Log($"[PlayerStatus] Mangiatto {amount}, Fame: {currentHunger}/{MaxHunger}");
        }

        /// <summary>
        /// Tenta di usare stamina. Ritorna true se sufficiente.
        /// </summary>
        public bool TryUseStamina(float amount)
        {
            if (!isAlive) return false;

            if (currentStamina >= amount)
            {
                currentStamina = Mathf.Max(0f, currentStamina - amount);
                return true;
            }
            else
            {
                // Stamina insufficiente
                if (OnStaminaDepleted != null)
                {
                    OnStaminaDepleted(gameObject.name);
                }
                return false;
            }
        }

        /// <summary>
        /// Verifica se il Player ha fame (sotto hungerThreshold).
        /// </summary>
        public bool IsHungry()
        {
            return currentHunger < playerProfile.hungerThreshold;
        }

        /// <summary>
        /// Verifica se il Player è stanco (stamina basso).
        /// </summary>
        public bool IsExhausted()
        {
            return currentStamina < playerProfile.exhaustThreshold;
        }

        #endregion

        #region Private Methods

        private void DecreaseHunger(float amount)
        {
            currentHunger = Mathf.Max(0f, currentHunger - amount);
        }

        private void RecoverStamina(float amount)
        {
            currentStamina = Mathf.Min(MaxStamina, currentStamina + amount);
        }

        private void Die()
        {
            if (!isAlive) return;

            isAlive = false;
            Debug.Log($"[PlayerStatus] Player morto per fame/starvation!");

            if (OnPlayerDied != null)
            {
                OnPlayerDied();
            }
        }

        #endregion

        #region Utilità

        /// <summary>
        /// Ottiene il valore normalizzato (0-1) di una statistica.
        /// </summary>
        public float GetStatusNormalized(string type)
        {
            return type switch
            {
                "Health" => currentHealth / MaxHealth,
                "Stamina" => currentStamina / MaxStamina,
                "Hunger" => currentHunger / MaxHunger,
                _ => 0f
            };
        }

        /// <summary>
        /// Info di debug.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Player\n" +
                   $"HP: {currentHealth:F0}/{MaxHealth:F0}\n" +
                   $"Stamina: {currentStamina:F0}/{MaxStamina:F0}\n" +
                   $"Hunger: {currentHunger:F0}/{MaxHunger:F0}";
        }

        #endregion
    }
}

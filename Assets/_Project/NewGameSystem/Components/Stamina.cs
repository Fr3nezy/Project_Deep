using UnityEngine;
using UnityEngine.Events;

namespace GameSystem
{
    public enum StaminaState
    {
        Normal,
        Recovering,
        Exhausted
    }

    public class StaminaComponent : MonoBehaviour
    {
        [Header("Stamina Settings")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina = 100f;
        [SerializeField] private float regenRate = 10f;
        [SerializeField] private float regenDelay = 1f;
        [SerializeField] private float exhaustedDuration = 3f;

        [Header("Speed Multipliers")]
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float exhaustedMultiplier = 0.75f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        // Events
        public UnityEvent OnStaminaExhausted;
        public UnityEvent OnStaminaRecovered;
        public UnityEvent<float> OnStaminaChanged;

        // State
        private StaminaState currentState = StaminaState.Normal;
        private float lastStaminaUseTime = 0f;
        private float lastExhaustionTime = 0f;
        private bool isSprinting = false;

        void Awake()
        {
            currentStamina = maxStamina;
        }

        void Update()
        {
            UpdateStaminaState();
            UpdateRegeneration();
        }

        private void UpdateStaminaState()
        {
            // Check for exhaustion
            if (currentStamina <= 0f && currentState != StaminaState.Exhausted)
            {
                EnterExhausted();
            }
            else if (currentState == StaminaState.Exhausted && Time.time - lastExhaustionTime >= exhaustedDuration)
            {
                ExitExhausted();
            }
        }

        private void UpdateRegeneration()
        {
            if (currentState == StaminaState.Exhausted)
                return;

            // Start regenerating after delay
            if (Time.time - lastStaminaUseTime >= regenDelay && currentStamina < maxStamina)
            {
                float regenAmount = regenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina + regenAmount, maxStamina);
                OnStaminaChanged?.Invoke(currentStamina);
            }
        }

        public bool ConsumeStamina(float amount)
        {
            if (currentState == StaminaState.Exhausted)
                return false;

            if (currentStamina >= amount)
            {
                currentStamina -= amount;
                lastStaminaUseTime = Time.time;
                OnStaminaChanged?.Invoke(currentStamina);
                return true;
            }
            else
            {
                currentStamina = 0f;
                lastStaminaUseTime = Time.time;
                OnStaminaChanged?.Invoke(currentStamina);
                return false;
            }
        }

        private void EnterExhausted()
        {
            currentState = StaminaState.Exhausted;
            lastExhaustionTime = Time.time;
            isSprinting = false;
            OnStaminaExhausted?.Invoke();

            if (showDebugLogs)
                Debug.Log($"[Stamina] {gameObject.name} is EXHAUSTED!");
        }

        private void ExitExhausted()
        {
            currentState = StaminaState.Normal;
            OnStaminaRecovered?.Invoke();

            if (showDebugLogs)
                Debug.Log($"[Stamina] {gameObject.name} recovered from exhaustion");
        }

        public void StartSprint()
        {
            if (currentState != StaminaState.Exhausted)
            {
                isSprinting = true;
            }
        }

        public void StopSprint()
        {
            isSprinting = false;
        }

        // PUBLIC API
        public void SetMaxStamina(float max)
        {
            maxStamina = Mathf.Max(1f, max);
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }

        public float GetCurrentStamina() => currentStamina;
        public float GetMaxStamina() => maxStamina;
        public float GetStaminaPercentage() => currentStamina / maxStamina;
        public bool IsSprinting() => isSprinting;
        public bool IsExhausted() => currentStamina <= 0.1f;
        public StaminaState GetState() => currentState;
    }
}

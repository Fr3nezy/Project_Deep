using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Player
{
    public enum OxygenLevel
    {
        Safe,
        Warning,
        Critical,
        Emergency,
        Depleted
    }

    /// <summary>
    /// Serbatoio di ossigeno del diver. Non conosce nessun sistema di gioco: somma il consumo
    /// dichiarato dalle fonti IOxygenDrainSource presenti sul diver e pubblica il proprio stato
    /// via eventi. HUD, respiro audio e H.E.L.M. si limitano ad ascoltare.
    /// </summary>
    public class OxygenSystem : MonoBehaviour
    {
        [Header("Profilo")]
        [SerializeField] private OxygenProfile profile;

        [Header("Debug (sola lettura)")]
        [SerializeField] private float currentOxygen;
        [SerializeField] private OxygenLevel currentLevel = OxygenLevel.Safe;
        [SerializeField] private float currentDrainPerSecond;

        private readonly List<IOxygenDrainSource> drainSources = new();

        public event Action<float> OnOxygenChanged;
        public event Action<OxygenLevel> OnLevelChanged;
        public event Action OnOxygenDepleted;

        public float CurrentOxygen => currentOxygen;
        public float MaxOxygen => profile.maxOxygen;
        public float NormalizedOxygen => currentOxygen / profile.maxOxygen;
        public OxygenLevel Level => currentLevel;
        public float CurrentDrainPerSecond => currentDrainPerSecond;

        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogError("[OxygenSystem] OxygenProfile non assegnato. Componente disabilitato.", this);
                enabled = false;
                return;
            }

            currentOxygen = profile.maxOxygen;
            CollectDrainSources();
        }

        /// <summary>
        /// Registra una fonte di consumo creata a runtime (es. equipaggiamento raccolto).
        /// </summary>
        public void RegisterDrainSource(IOxygenDrainSource source)
        {
            if (source != null && !drainSources.Contains(source))
            {
                drainSources.Add(source);
            }
        }

        public void UnregisterDrainSource(IOxygenDrainSource source)
        {
            drainSources.Remove(source);
        }

        /// <summary>
        /// Erogazione da una stazione di aggancio. Ritorna true finché il serbatoio non è pieno.
        /// </summary>
        public bool Refill(float deltaTime)
        {
            if (currentOxygen >= profile.maxOxygen) return false;

            SetOxygen(currentOxygen + profile.refillRate * deltaTime);
            return true;
        }

        private void Update()
        {
            if (currentLevel == OxygenLevel.Depleted) return;

            currentDrainPerSecond = profile.baselineDrain;
            for (int i = 0; i < drainSources.Count; i++)
            {
                currentDrainPerSecond += Mathf.Max(0f, drainSources[i].GetOxygenDrainPerSecond());
            }

            SetOxygen(currentOxygen - currentDrainPerSecond * Time.deltaTime);
        }

        private void CollectDrainSources()
        {
            foreach (IOxygenDrainSource source in GetComponentsInChildren<IOxygenDrainSource>(true))
            {
                drainSources.Add(source);
            }
        }

        private void SetOxygen(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, profile.maxOxygen);
            if (Mathf.Approximately(clamped, currentOxygen)) return;

            currentOxygen = clamped;
            OnOxygenChanged?.Invoke(NormalizedOxygen);
            EvaluateLevel();
        }

        private void EvaluateLevel()
        {
            float normalized = NormalizedOxygen;
            OxygenLevel level;

            if (normalized <= 0f) level = OxygenLevel.Depleted;
            else if (normalized <= profile.emergencyThreshold) level = OxygenLevel.Emergency;
            else if (normalized <= profile.criticalThreshold) level = OxygenLevel.Critical;
            else if (normalized <= profile.warningThreshold) level = OxygenLevel.Warning;
            else level = OxygenLevel.Safe;

            if (level == currentLevel) return;

            currentLevel = level;
            OnLevelChanged?.Invoke(level);

            if (level == OxygenLevel.Depleted)
            {
                OnOxygenDepleted?.Invoke();
            }
        }
    }
}

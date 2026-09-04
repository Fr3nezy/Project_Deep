using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Player
{
    public sealed class StressSystem : MonoBehaviour, IOxygenDrainSource
    {
        private const float STRESS_ACCUMULATION_THRESHOLD = 0.05f;

        [SerializeField] private StressProfile profile;
        [SerializeField] private float currentStress;
        [SerializeField] private float currentStressPerSecond;

        private readonly List<IStressSource> sources = new();

        public event Action<float> OnStressChanged;

        public float CurrentStress => currentStress;
        public float MaxStress => profile.maxStress;
        public float NormalizedStress => profile.maxStress > 0f ? currentStress / profile.maxStress : 0f;
        public float CurrentStressPerSecond => currentStressPerSecond;

        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogError("[StressSystem] StressProfile non assegnato. Componente disabilitato.", this);
                enabled = false;
                return;
            }

            foreach (IStressSource source in GetComponentsInChildren<IStressSource>(true))
                sources.Add(source);
        }

        private void Update()
        {
            currentStressPerSecond = 0f;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null)
                    currentStressPerSecond += Mathf.Max(0f, sources[i].GetStressPerSecond());
            }

            // Se lo stress accumulato per secondo è sopra la soglia, lo stress sale.
            // Se le condizioni sono sicure (es. luce piena, visuale ferma), scatta il recovery continuo verso 0.
            float delta = currentStressPerSecond > STRESS_ACCUMULATION_THRESHOLD
                ? currentStressPerSecond
                : -profile.recoveryPerSecond;

            SetStress(currentStress + delta * Time.deltaTime);
        }

        public void RegisterSource(IStressSource source)
        {
            if (source != null && !sources.Contains(source))
                sources.Add(source);
        }

        public void UnregisterSource(IStressSource source)
        {
            sources.Remove(source);
        }

        public float GetOxygenDrainPerSecond()
        {
            return NormalizedStress * profile.maxOxygenDrainPerSecond;
        }

        private void SetStress(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, profile.maxStress);
            if (Mathf.Approximately(clamped, currentStress))
                return;

            currentStress = clamped;
            OnStressChanged?.Invoke(NormalizedStress);
        }
    }
}

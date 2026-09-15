using System.Collections.Generic;
using Deeploration.Environment;
using UnityEngine;

namespace Deeploration.Player
{
    public sealed class DarknessStressSource : MonoBehaviour, IStressSource
    {
        [Header("Stress Parameters")]
        [SerializeField, Min(0f)] private float darkStressPerSecond = 8f;
        [SerializeField, Min(0f)] private float minimumStressPerSecond;
        [SerializeField, Min(0f)] private float lightTransitionSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float currentLightRelief;

        [Header("Player Flashlight")]
        [SerializeField] private DiverFlashlight flashlight;

        private readonly HashSet<StressLightZone> activeZones = new();

        public float CurrentLightRelief => currentLightRelief;
        public float EffectiveLightRelief => GetEffectiveLightRelief();

        private void Awake()
        {
            if (flashlight == null)
            {
                flashlight = GetComponent<DiverFlashlight>();
                if (flashlight == null)
                    flashlight = GetComponentInParent<DiverFlashlight>();
                if (flashlight == null)
                    flashlight = GetComponentInChildren<DiverFlashlight>();
            }
        }

        private void Update()
        {
            float targetRelief = GetStrongestActiveRelief();
            float blend = 1f - Mathf.Exp(-lightTransitionSpeed * Time.deltaTime);
            currentLightRelief = Mathf.Lerp(currentLightRelief, targetRelief, blend);
        }

        public void RegisterLightZone(StressLightZone zone)
        {
            if (zone != null)
                activeZones.Add(zone);
        }

        public void UnregisterLightZone(StressLightZone zone)
        {
            activeZones.Remove(zone);
        }

        public float GetStressPerSecond()
        {
            if (!enabled)
                return 0f;

            float effectiveRelief = GetEffectiveLightRelief();

            // Se il rilievo è quasi totale (>= 0.95), restituisce minimumStressPerSecond (default 0)
            // in modo che StressSystem possa far scendere lo stress a 0.
            if (effectiveRelief >= 0.95f)
                return minimumStressPerSecond;

            return Mathf.Lerp(darkStressPerSecond, minimumStressPerSecond, effectiveRelief);
        }

        public float GetEffectiveLightRelief()
        {
            float playerRelief = flashlight != null ? flashlight.CurrentRelief : 0f;
            return Mathf.Clamp01(Mathf.Max(currentLightRelief, playerRelief));
        }

        private float GetStrongestActiveRelief()
        {
            activeZones.RemoveWhere(zone => zone == null || !zone.isActiveAndEnabled);

            float strongestRelief = 0f;
            foreach (StressLightZone zone in activeZones)
                strongestRelief = Mathf.Max(strongestRelief, zone.StressRelief);

            return strongestRelief;
        }

        private void OnValidate()
        {
            minimumStressPerSecond = Mathf.Min(minimumStressPerSecond, darkStressPerSecond);
        }
    }
}

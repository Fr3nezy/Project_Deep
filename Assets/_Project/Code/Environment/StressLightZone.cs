using System.Collections.Generic;
using Deeploration.Player;
using UnityEngine;

namespace Deeploration.Environment
{
    [RequireComponent(typeof(Collider))]
    public sealed class StressLightZone : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float stressRelief = 1f;
        [SerializeField] private Light visualLight;

        private readonly HashSet<DarknessStressSource> registeredSources = new();

        public float StressRelief => stressRelief;

        private void Awake()
        {
            if (visualLight == null)
            {
                Debug.LogError("[StressLightZone] Luce visiva non assegnata. Componente disabilitato.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            foreach (DarknessStressSource source in registeredSources)
            {
                if (source != null)
                    source.UnregisterLightZone(this);
            }

            registeredSources.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            DarknessStressSource source = other.GetComponentInParent<DarknessStressSource>();
            if (source == null || !registeredSources.Add(source))
                return;

            source.RegisterLightZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            DarknessStressSource source = other.GetComponentInParent<DarknessStressSource>();
            if (source == null || !registeredSources.Remove(source))
                return;

            source.UnregisterLightZone(this);
        }

        private void OnValidate()
        {
            Collider zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;
        }
    }
}

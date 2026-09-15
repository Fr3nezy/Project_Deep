using UnityEngine;

namespace Deeploration.Player
{
    [CreateAssetMenu(fileName = "Oxygen Profile", menuName = "Deeploration/Oxygen Profile")]
    public class OxygenProfile : ScriptableObject
    {
        [Header("Serbatoio")]
        [Tooltip("Capacità massima del serbatoio in unità di ossigeno")]
        [Range(50f, 1000f)]
        public float maxOxygen = 400f;

        [Tooltip("Consumo garantito del respiro a riposo, per secondo")]
        [Range(0.1f, 5f)]
        public float baselineDrain = 0.8f;

        [Header("Ricarica")]
        [Tooltip("Ossigeno al secondo erogato da una stazione di aggancio")]
        [Range(1f, 100f)]
        public float refillRate = 25f;

        [Header("Soglie di allerta")]
        [Tooltip("Sotto questa frazione del serbatoio si entra in fascia gialla")]
        [Range(0f, 1f)]
        public float warningThreshold = 0.5f;

        [Tooltip("Sotto questa frazione si entra in fascia arancione: interviene H.E.L.M.")]
        [Range(0f, 1f)]
        public float criticalThreshold = 0.25f;

        [Tooltip("Sotto questa frazione si entra in fascia rossa: respiro affannato dominante")]
        [Range(0f, 1f)]
        public float emergencyThreshold = 0.1f;
    }
}

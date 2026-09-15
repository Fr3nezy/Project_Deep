using UnityEngine;

namespace Deeploration.Player
{
    [CreateAssetMenu(fileName = "Stress Profile", menuName = "Deeploration/Stress Profile")]
    public sealed class StressProfile : ScriptableObject
    {
        [Header("Stress")]
        [Range(10f, 200f)] public float maxStress = 100f;
        [Range(0f, 50f)] public float recoveryPerSecond = 10f;
        [Range(0f, 5f)] public float maxOxygenDrainPerSecond = 1.2f;

        [Header("Agitazione visuale")]
        [Range(0f, 500f)] public float agitationThreshold = 110f;
        [Range(1f, 1000f)] public float fullAgitationSpeed = 420f;
        [Range(0f, 100f)] public float stressAtFullAgitationPerSecond = 25f;
        [Range(1f, 40f)] public float angularVelocitySmoothing = 12f;

        private void OnValidate()
        {
            maxStress = Mathf.Max(1f, maxStress);
            fullAgitationSpeed = Mathf.Max(agitationThreshold + 1f, fullAgitationSpeed);
        }
    }
}

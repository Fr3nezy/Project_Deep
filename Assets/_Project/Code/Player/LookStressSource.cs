using UnityEngine;

namespace Deeploration.Player
{
    [DefaultExecutionOrder(-50)]
    public sealed class LookStressSource : MonoBehaviour, IStressSource
    {
        [SerializeField] private StressProfile profile;
        [SerializeField] private Transform lookTarget;
        [SerializeField] private float angularVelocity;
        [SerializeField] private float stressPerSecond;

        private Quaternion previousRotation;

        private void Awake()
        {
            if (profile == null || lookTarget == null)
            {
                Debug.LogError("[LookStressSource] Profilo o target non assegnato. Componente disabilitato.", this);
                enabled = false;
                return;
            }

            previousRotation = lookTarget.rotation;
        }

        private void Update()
        {
            Quaternion rotation = lookTarget.rotation;
            float rawVelocity = Quaternion.Angle(previousRotation, rotation) / Mathf.Max(Time.deltaTime, 0.0001f);
            float blend = 1f - Mathf.Exp(-profile.angularVelocitySmoothing * Time.deltaTime);
            angularVelocity = Mathf.Lerp(angularVelocity, rawVelocity, blend);
            previousRotation = rotation;

            float agitation = Mathf.InverseLerp(
                profile.agitationThreshold,
                profile.fullAgitationSpeed,
                angularVelocity);

            stressPerSecond = agitation * profile.stressAtFullAgitationPerSecond;
        }

        public float GetStressPerSecond()
        {
            return enabled ? stressPerSecond : 0f;
        }
    }
}

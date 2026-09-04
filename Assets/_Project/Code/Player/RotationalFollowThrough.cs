using UnityEngine;

namespace Deeploration.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class RotationalFollowThrough : MonoBehaviour
    {
        [SerializeField] private Transform rotationTarget;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 7f;
        [SerializeField, Range(0f, 45f)] private float maxLagAngle = 12f;

        private Quaternion targetOffset;
        private Quaternion smoothedRotation;

        private void Awake()
        {
            if (rotationTarget == null && Camera.main != null)
                rotationTarget = Camera.main.transform;

            smoothedRotation = transform.rotation;

            if (rotationTarget != null)
                targetOffset = Quaternion.Inverse(rotationTarget.rotation) * transform.rotation;
        }

        private void LateUpdate()
        {
            if (rotationTarget == null)
                return;

            Quaternion desiredRotation = rotationTarget.rotation * targetOffset;
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            smoothedRotation = Quaternion.Slerp(smoothedRotation, desiredRotation, blend);

            if (maxLagAngle > 0f && Quaternion.Angle(smoothedRotation, desiredRotation) > maxLagAngle)
                smoothedRotation = Quaternion.RotateTowards(desiredRotation, smoothedRotation, maxLagAngle);

            transform.rotation = smoothedRotation;
        }

        private void OnValidate()
        {
            rotationSharpness = Mathf.Max(0.01f, rotationSharpness);
            maxLagAngle = Mathf.Clamp(maxLagAngle, 0f, 45f);
        }
    }
}
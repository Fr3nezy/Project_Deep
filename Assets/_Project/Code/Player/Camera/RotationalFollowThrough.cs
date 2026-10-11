using UnityEngine;

namespace Deeploration.Player
{
    [DisallowMultipleComponent]
    // Dopo CinemachineBrain e PlayerWakeUpSequence (entrambi 100): legge la rotazione finale del target nel frame.
    [DefaultExecutionOrder(200)]
    public sealed class RotationalFollowThrough : MonoBehaviour
    {
        [SerializeField] private Transform rotationTarget;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 7f;
        [SerializeField, Range(0f, 45f)] private float maxLagAngle = 12f;

        [SerializeField, Tooltip("Rotazione del pivot rispetto al target, catturata in editor dalla posa authorata. " +
                                 "Non va letta in Awake: altri script (es. PlayerWakeUpSequence) possono aver già ruotato il target.")]
        private Vector3 rotationOffsetEuler;
        [SerializeField, HideInInspector] private bool offsetCaptured;

        private Quaternion targetOffset;
        private Quaternion smoothedRotation;

        private void Awake()
        {
            if (rotationTarget == null && Camera.main != null)
                rotationTarget = Camera.main.transform;

            smoothedRotation = transform.rotation;

            if (offsetCaptured)
                targetOffset = Quaternion.Euler(rotationOffsetEuler);
            else if (rotationTarget != null)
                targetOffset = Quaternion.Inverse(rotationTarget.rotation) * transform.rotation;
        }

        /// <summary>Salva l'offset attuale tra pivot e target come offset authorato.</summary>
        [ContextMenu("Capture Rotation Offset")]
        public void CaptureRotationOffset()
        {
            if (rotationTarget == null)
                return;

            rotationOffsetEuler = (Quaternion.Inverse(rotationTarget.rotation) * transform.rotation).eulerAngles;
            offsetCaptured = true;
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

            // In editor la posa è quella authorata: l'offset si ricattura a ogni modifica
            if (!Application.isPlaying)
                CaptureRotationOffset();
        }
    }
}
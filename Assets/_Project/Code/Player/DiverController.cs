using UnityEngine;
using StarterAssets;

namespace Deeploration.Player
{
    /// <summary>
    /// Movimento del diver: camminata pesante sul fondale con inerzia e visuale smorzata dal casco.
    /// Nessuna verticalità: il salto è un verbo che il design assegna all'Hydropack, sbloccabile
    /// più avanti come componente separato.
    /// Espone il proprio sforzo fisico come fonte di consumo ossigeno, senza conoscere il serbatoio.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DiverController : MonoBehaviour, IOxygenDrainSource
    {
        [Header("Profilo")]
        [SerializeField] private DiverProfile profile;

        [Header("Riferimenti")]
        [SerializeField, Tooltip("Target di rotazione verticale della camera (pitch del casco)")]
        private Transform cameraTarget;

        [Header("Fondale")]
        [SerializeField, Tooltip("Layer considerati fondale/superficie calpestabile")]
        private LayerMask groundLayers = ~0;

        [SerializeField, Tooltip("Offset verticale della sfera di contatto col fondale")]
        private float groundedOffset = -0.14f;

        [SerializeField, Tooltip("Raggio della sfera di contatto. Allinearlo al raggio del CharacterController")]
        private float groundedRadius = 0.5f;

        [Header("Debug (sola lettura)")]
        [SerializeField] private bool grounded;
        [SerializeField] private float currentSpeed;

        private CharacterController controller;
        private StarterAssetsInputs input;

        private Vector3 horizontalVelocity;
        private float verticalVelocity;

        private float targetPitch;
        private float currentPitch;
        private float targetYaw;

        private Vector3 cameraBasePosition;
        private float bobCycle;
        private float bobWeight;
        private float bobRoll;
        private bool footDownPending;

        public bool IsGrounded => grounded;
        public float CurrentSpeed => currentSpeed;
        public bool IsSprinting => input != null && input.sprint && currentSpeed > 0.1f;

        /// <summary>Emesso quando un piede tocca il fondale. Aggancio per il suono dei passi.</summary>
        public event System.Action OnFootstep;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<StarterAssetsInputs>();

            if (profile == null)
            {
                Debug.LogError("[DiverController] DiverProfile non assegnato. Componente disabilitato.", this);
                enabled = false;
                return;
            }

            if (input == null)
            {
                Debug.LogError("[DiverController] StarterAssetsInputs non trovato sul diver. Componente disabilitato.", this);
                enabled = false;
                return;
            }

            targetYaw = transform.eulerAngles.y;

            if (cameraTarget != null)
            {
                cameraBasePosition = cameraTarget.localPosition;
            }
        }

        private void Update()
        {
            GroundedCheck();
            ApplyGravity();
            Move();
        }

        private void LateUpdate()
        {
            RotateView();
            ApplyHeadBob();
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = transform.position + Vector3.up * groundedOffset;
            grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
        }

        private void ApplyGravity()
        {
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                return;
            }

            verticalVelocity = Mathf.Max(
                verticalVelocity + profile.gravity * Time.deltaTime,
                -profile.terminalVelocity);
        }

        private void Move()
        {
            Vector3 inputDirection = (transform.right * input.move.x + transform.forward * input.move.y);
            if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();

            float targetSpeed = input.sprint ? profile.sprintSpeed : profile.walkSpeed;
            Vector3 targetVelocity = inputDirection * targetSpeed;

            // Accelerazione e decelerazione separate: il diver parte lento e si ferma scivolando
            float rate = targetVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude
                ? profile.acceleration
                : profile.deceleration;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity,
                rate * Time.deltaTime);

            currentSpeed = horizontalVelocity.magnitude;

            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void RotateView()
        {
            float pitchInput = profile.invertLookY ? -input.look.y : input.look.y;

            targetYaw += input.look.x * profile.lookSensitivity;
            targetPitch = Mathf.Clamp(
                targetPitch + pitchInput * profile.lookSensitivity,
                profile.lookDownLimit,
                profile.lookUpLimit);

            // Il casco insegue la visuale con ritardo: la testa ha massa
            float t = 1f - Mathf.Exp(-profile.lookDamping * Time.deltaTime);

            float smoothedYaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, t);
            transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);

            if (cameraTarget != null)
            {
                currentPitch = Mathf.Lerp(currentPitch, targetPitch, t);
                cameraTarget.localRotation = Quaternion.Euler(currentPitch, 0f, bobRoll);
            }
        }

        private void ApplyHeadBob()
        {
            if (cameraTarget == null) return;

            if (!profile.enableHeadBob)
            {
                cameraTarget.localPosition = cameraBasePosition;
                bobRoll = 0f;
                return;
            }

            // Il ciclo avanza con la distanza percorsa, non col tempo: la falcata resta
            // agganciata al passo reale a qualsiasi velocità
            bool walking = grounded && currentSpeed > 0.05f;
            if (walking)
            {
                bobCycle += currentSpeed * Time.deltaTime / Mathf.Max(0.01f, profile.bobStrideLength);
                bobCycle %= 1f;
            }

            float targetWeight = walking ? Mathf.Clamp01(currentSpeed / profile.walkSpeed) : 0f;
            bobWeight = Mathf.MoveTowards(bobWeight, targetWeight, profile.bobSettleSpeed * Time.deltaTime);

            float phase = bobCycle * Mathf.PI * 2f;

            // Verticale al doppio della frequenza: due appoggi per falcata
            float vertical = -Mathf.Abs(Mathf.Cos(phase)) * profile.bobVerticalAmplitude;
            float lateral = Mathf.Sin(phase) * profile.bobLateralAmplitude;

            cameraTarget.localPosition = cameraBasePosition + new Vector3(lateral, vertical, 0f) * bobWeight;
            bobRoll = -Mathf.Sin(phase) * profile.bobRollAmplitude * bobWeight;

            EmitFootstep(phase);
        }

        private void EmitFootstep(float phase)
        {
            bool atLowPoint = Mathf.Abs(Mathf.Cos(phase)) > 0.98f;

            if (atLowPoint && !footDownPending && bobWeight > 0.2f)
            {
                footDownPending = true;
                OnFootstep?.Invoke();
            }
            else if (!atLowPoint)
            {
                footDownPending = false;
            }
        }

        public float GetOxygenDrainPerSecond()
        {
            if (currentSpeed <= 0.05f) return 0f;

            float effortDrain = input.sprint ? profile.sprintOxygenDrain : profile.walkOxygenDrain;
            return effortDrain * (currentSpeed / profile.sprintSpeed);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = grounded
                ? new Color(0f, 1f, 0f, 0.35f)
                : new Color(1f, 0f, 0f, 0.35f);

            Gizmos.DrawSphere(transform.position + Vector3.up * groundedOffset, groundedRadius);
        }
    }
}

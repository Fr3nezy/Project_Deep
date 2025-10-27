using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
	[Header("Stamina Integration")]
	[Tooltip("Integrazione con PlayerStatus per gestire stamina")]
	public bool useStaminaSystem = true;

	[Tooltip("Thrust del jetpack mentre si tiene premuto spazio in aria")]
	[Range(0f, 50f)]
	public float jetpackThrust = 12f;

	[Tooltip("Costo stamina per secondo del jetpack")]
	[Range(1f, 50f)]
	public float jetpackCostPerSecond = 12f;

	[Tooltip("Ritardo prima dell'attivazione del jetpack dopo un salto")]
	[Range(0f, 1f)]
	public float jetpackActivationDelay = 0.1f;

		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

	[Space(10)]
	[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
	public float JumpTimeout = 0f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		// grounded hysteresis
		private float _groundedHysteresisTimer = 0f;
		private const float _groundedHysteresisTimeout = 0.05f;

	// jetpack boost tracking
	private bool _jetpackWasActive = false;
	private float _lastJumpTime = -1f;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;
		private Deeploration.EntitySystem.PlayerStatus _playerStatus;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// Get PlayerStatus component if stamina system is enabled
			if (useStaminaSystem)
			{
				_playerStatus = GetComponent<Deeploration.EntitySystem.PlayerStatus>();
				if (_playerStatus == null)
				{
					Debug.LogWarning("[FirstPersonController] useStaminaSystem is enabled but PlayerStatus component not found. Stamina system disabled.");
					useStaminaSystem = false;
				}
			}

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			GroundedCheck();
			JumpAndGravity();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			bool rawGrounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

			// Hysteresis for stable grounded detection
			bool wasGrounded = Grounded;
			if (rawGrounded && !Grounded)
			{
				// Was not grounded, now detected grounded - immediate set
				Grounded = true;
				_groundedHysteresisTimer = 0f;
				Debug.Log("[Grounded Check] Became grounded!");
			}
			else if (!rawGrounded && Grounded)
			{
				// Was grounded, now detected not grounded - start hysteresis delay
				if (_groundedHysteresisTimer == 0f)
				{
					_groundedHysteresisTimer = _groundedHysteresisTimeout;
				}
				_groundedHysteresisTimer -= Time.deltaTime;

				if (_groundedHysteresisTimer <= 0f)
				{
					// Delay expired, set to not grounded
					Grounded = false;
					_groundedHysteresisTimer = 0f;
					Debug.LogWarning("[Grounded Check] Became not grounded!");
				}
				// else stay grounded
			}
			else if (Grounded && rawGrounded)
			{
				// Ensure timer is reset when continuously grounded
				_groundedHysteresisTimer = 0f;
			}
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			// Check if sprinting is allowed with stamina system
			bool canSprint = true;
			if (useStaminaSystem && _playerStatus != null && _input.sprint)
			{
				// Try to consume stamina for sprint (cost per second * deltaTime)
				float sprintCost = _playerStatus.Profile.sprintCostPerSecond * Time.deltaTime;
				canSprint = _playerStatus.TryUseStamina(sprintCost);
			}

			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = (_input.sprint && canSprint) ? SprintSpeed : MoveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void JumpAndGravity()
		{
			// Disable jump input when grounded to reset continuous press
			if (Grounded)
			{
				_input.jump = false;
			}

			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Initial Jump - use jumpDown (rising edge managed by StarterAssetsInputs)
				if (_input.jumpDown && _jumpTimeoutDelta <= 0.0f)
				{
					// Check if stamina system is enabled and consume jump cost
					bool canJump = true;
					if (useStaminaSystem && _playerStatus != null)
					{
						// Jump is a one-time cost
						canJump = _playerStatus.TryUseStamina(_playerStatus.Profile.jumpCost);
						Debug.Log($"[Jump] Trying to jump - Can jump: {canJump}, Stamina: {_playerStatus.CurrentStamina}/{_playerStatus.MaxStamina}");
					}
					else
					{
						Debug.Log("[Jump] No stamina system or player status, jumping freely");
					}

					if (canJump)
					{
						// the square root of H * -2 * G = how much velocity needed to reach desired height
						_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
						_lastJumpTime = Time.time;
						Debug.Log($"[Jump] Jumped! Vertical velocity: {_verticalVelocity}");
					}
					else
					{
						Debug.LogWarning("[Jump] Cannot jump - insufficient stamina or system disabled");
					}
					
					// Consume the jumpDown input
					_input.jumpDown = false;
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
				
				// Reset jetpack state when landing
				_jetpackWasActive = false;
			}
			else
			{
				// IN AIR
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// Jetpack Thrust: If holding SPACE while in air, apply continuous thrust after delay
				if (_input.jump && Time.time >= _lastJumpTime + jetpackActivationDelay && useStaminaSystem && _playerStatus != null)
				{
					if (!_jetpackWasActive)
					{
						_jetpackWasActive = true;
						Debug.Log("[Jetpack] Jetpack activated!");
					}

					// Try to consume stamina for jetpack
					float thrustCostPerFrame = jetpackCostPerSecond * Time.deltaTime;
					bool hasStamina = _playerStatus.TryUseStamina(thrustCostPerFrame);

					if (hasStamina)
					{
						// Apply constant upward thrust
						_verticalVelocity += jetpackThrust * Time.deltaTime;

						// Clamp max upward velocity to prevent floating away
						_verticalVelocity = Mathf.Min(_verticalVelocity, jetpackThrust * 0.5f);
					}
					else
					{
						// No stamina, deactivate jetpack
						_jetpackWasActive = false;
						Debug.Log("[Jetpack] Out of stamina, jetpack deactivated");
					}
				}
				else if (_jetpackWasActive)
				{
					// Deactivate jetpack if player releases space
					_jetpackWasActive = false;
					Debug.Log("[Jetpack] Jetpack boost deactivated (released space)");
				}
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}

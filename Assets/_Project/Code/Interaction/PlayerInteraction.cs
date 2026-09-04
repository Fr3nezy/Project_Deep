using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deeploration.Interaction
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Parametri Raycast")]
        [SerializeField] private float interactionDistance = 2.6f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("Riferimenti")]
        [SerializeField] private Camera playerCamera;

        [Header("Debug")]
        [SerializeField] private bool hasTarget;
        [SerializeField] private string currentPrompt;

        private IInteractable currentTarget;

        public IInteractable CurrentTarget => currentTarget;
        public bool HasTarget => hasTarget;
        public string CurrentPrompt => currentPrompt;

        public event Action<IInteractable> OnTargetChanged;

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>() ?? Camera.main;
            }
        }

        private void Update()
        {
            PerformRaycast();
            CheckInput();
        }

        private void PerformRaycast()
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            IInteractable foundInteractable = null;

            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide))
            {
                foundInteractable = hit.collider.GetComponentInParent<IInteractable>() ?? hit.collider.GetComponent<IInteractable>();
            }

            if (foundInteractable != currentTarget)
            {
                currentTarget = foundInteractable;
                hasTarget = currentTarget != null;
                currentPrompt = hasTarget ? currentTarget.PromptText : string.Empty;
                OnTargetChanged?.Invoke(currentTarget);
            }
            else if (hasTarget)
            {
                currentPrompt = currentTarget.PromptText;
            }
        }

        private void CheckInput()
        {
            if (!hasTarget || currentTarget == null) return;

            bool interactRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                interactRequested = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(interactKey))
                interactRequested = true;
#endif

            if (interactRequested && currentTarget.CanInteract(gameObject))
            {
                currentTarget.Interact(gameObject);
                currentPrompt = currentTarget.PromptText;
            }
        }
    }
}

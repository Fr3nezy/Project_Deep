using Deeploration.Player;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deeploration.Interaction
{
    /// <summary>
    /// Trascinamento fisico col crosshair: tenendo premuto il tasto sinistro su un <see cref="Grabbable"/> il punto
    /// afferrato viene tirato verso un punto davanti alla camera con una forza limitata, quindi gli oggetti pesanti
    /// arrivano lenti. Il diver rallenta in base alla massa. Si stacca al rilascio, se l'oggetto resta indietro oltre
    /// breakDistance o quando entra in uno slot. Va sul PlayerCapsule, accanto a DiverController.
    /// </summary>
    public sealed class PlayerGrab : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private KeyCode grabKey = KeyCode.Mouse0;

        [Tooltip("Distanza massima dalla camera per afferrare (m).")]
        [SerializeField, Min(0.5f)] private float grabDistance = 3f;
        [Tooltip("Distanza minima a cui l'oggetto viene tenuto (m).")]
        [SerializeField, Min(0.3f)] private float minHoldDistance = 1.5f;
        [Tooltip("Oltre questa distanza fra punto afferrato e bersaglio la presa si stacca (m).")]
        [SerializeField, Min(0.5f)] private float breakDistance = 2.5f;

        [Header("Forza")]
        [Tooltip("Velocità cercata per metro di distanza dal bersaglio (1/s).")]
        [SerializeField, Min(0f)] private float followStrength = 6f;
        [SerializeField, Min(0f)] private float maxFollowSpeed = 4f;
        [Tooltip("Forza massima della presa (N): con massa m l'accelerazione massima è maxForce / m.")]
        [SerializeField, Min(0f)] private float maxForce = 400f;

        [Header("Diver")]
        [Tooltip("Massa a cui il diver cammina a metà velocità mentre trascina (kg).")]
        [SerializeField, Min(0.1f)] private float massForHalfSpeed = 40f;

        [Header("Debug (sola lettura)")]
        [SerializeField] private Grabbable held;

        private DiverController diver;
        private Vector3 localAnchor;
        private float holdDistance;

        public Grabbable Held => held;

        private void Awake()
        {
            diver = GetComponent<DiverController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;
        }

        private void OnDisable() => Release();

        private void Update()
        {
            bool pressed = false, releasedKey = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pressed |= Mouse.current.leftButton.wasPressedThisFrame;
                releasedKey |= Mouse.current.leftButton.wasReleasedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(grabKey);
            releasedKey |= Input.GetKeyUp(grabKey);
#endif
            if (pressed && held == null) TryGrab();
            if (releasedKey) Release();
        }

        /// <summary>Afferra il Grabbable sotto il crosshair, se c'è entro grabDistance. Ritorna true se l'ha preso.</summary>
        public bool TryGrab()
        {
            Grabbable target = FindTarget(out RaycastHit hit);
            if (target == null) return false;

            held = target;
            held.IsHeld = true;
            localAnchor = held.transform.InverseTransformPoint(hit.point);
            holdDistance = Mathf.Max(hit.distance, minHoldDistance);
            if (diver != null) diver.SpeedMultiplier = 1f / (1f + held.Body.mass / massForHalfSpeed);
            return true;
        }

        /// <summary>Grabbable libero sotto il crosshair entro grabDistance, o null. Non lo afferra.</summary>
        public Grabbable FindTarget(out RaycastHit targetHit)
        {
            targetHit = default;
            if (playerCamera == null) return null;
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            foreach (RaycastHit hit in Physics.RaycastAll(ray, grabDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                Grabbable target = hit.rigidbody != null ? hit.rigidbody.GetComponent<Grabbable>() : null;
                if (target == null || target.IsSocketed) continue;
                targetHit = hit;
                return target;
            }
            return null;
        }

        public void Release()
        {
            if (held != null) held.IsHeld = false;
            held = null;
            if (diver != null) diver.SpeedMultiplier = 1f;
        }

        private void FixedUpdate()
        {
            if (held == null) return;
            if (held.IsSocketed || !held.isActiveAndEnabled) { Release(); return; }

            Rigidbody body = held.Body;
            Vector3 anchor = held.transform.TransformPoint(localAnchor);
            Vector3 target = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
            Vector3 delta = target - anchor;
            if (delta.magnitude > breakDistance) { Release(); return; }

            Vector3 desired = Vector3.ClampMagnitude(delta * followStrength, maxFollowSpeed);
            Vector3 force = (desired - body.GetPointVelocity(anchor)) * body.mass / Time.fixedDeltaTime;
            body.AddForceAtPosition(Vector3.ClampMagnitude(force, maxForce), anchor);
        }
    }
}

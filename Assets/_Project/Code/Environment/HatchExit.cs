using Deeploration.Interaction;
using Deeploration.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Environment
{
    /// <summary>
    /// Uscita guidata dalla capsula come un salto: con il portello aperto il diver preme [E],
    /// si raccoglie, si spinge attraverso l'apertura con una breve parabola rallentata dall'acqua
    /// e atterra sul fondale, a controlli bloccati. La camera segue il moto (abbassamento, sguardo
    /// lungo la traiettoria, assorbimento all'atterraggio) e alla fine il controllo torna al diver.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HatchExit : MonoBehaviour, IInteractable
    {
        [SerializeField] private HatchDoor hatch;
        [Tooltip("Punto in cui passa la camera, al centro dell'apertura.")]
        [SerializeField] private Transform passPoint;
        [Tooltip("Posizione dei piedi del diver a fine uscita, sul fondale.")]
        [SerializeField] private Transform exitPoint;
        [SerializeField] private float eyeHeight = 1.375f;
        [SerializeField] private string promptText = "Esci";
        [Tooltip("Se attivo l'atterraggio avviene sul suolo sotto exitPoint (raycast), non alla quota del punto.")]
        [SerializeField] private bool snapExitToGround = true;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Raccolta")]
        [SerializeField, Tooltip("Durata della raccolta prima della spinta (s).")]
        private float crouchDuration = 0.45f;
        [SerializeField, Tooltip("Quanto scende la testa nella raccolta (m).")]
        private float crouchDepth = 0.15f;
        [SerializeField, Tooltip("Sguardo in più verso il basso nella raccolta (gradi).")]
        private float crouchPitch = 10f;

        [Header("Salto")]
        [SerializeField, Tooltip("Durata del tratto in volo, dalla spinta all'atterraggio (s).")]
        private float jumpDuration = 1.4f;
        [SerializeField, Tooltip("Avanzamento lungo la traiettoria nel tempo: veloce alla spinta, frenato dall'acqua verso la fine.")]
        private AnimationCurve jumpCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.4f),
            new Keyframe(1f, 1f, 0.2f, 0f));
        [SerializeField, Tooltip("Altezza della parabola dopo il passaggio dal portello (m).")]
        private float arcHeight = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("Quanto lo sguardo segue l'inclinazione della traiettoria.")]
        private float pathLookWeight = 0.6f;
        [SerializeField, Tooltip("Roll massimo a metà volo (gradi).")]
        private float flightRoll = 3f;

        [Header("Atterraggio")]
        [SerializeField, Tooltip("Durata dell'assorbimento sul fondale (s).")]
        private float landDuration = 0.6f;
        [SerializeField, Tooltip("Quanto scende la testa all'impatto (m).")]
        private float landDepth = 0.2f;
        [SerializeField, Tooltip("Scatto in avanti dello sguardo all'impatto (gradi).")]
        private float landPitchKick = 6f;

        [Header("Eventi")]
        public UnityEvent onExitStarted = new UnityEvent();
        public UnityEvent onExited = new UnityEvent();

        private Transform diver;
        private CharacterController controller;
        private DiverController diverController;
        private Transform cameraTarget;
        private Vector3 start, pass, control, land;
        private float startYaw, exitYaw, startPitch, startRoll;
        private Vector3 startCamera;
        private float pitch, yaw, landStartPitch;
        private float elapsed = -1f;
        private bool used;

        public string PromptText => promptText;

        public bool CanInteract(GameObject user) =>
            !used && hatch != null && hatch.IsOpen && !hatch.IsMoving && passPoint != null && exitPoint != null;

        public void Interact(GameObject user)
        {
            if (!CanInteract(user)) return;

            used = true;
            diver = user.transform;
            controller = user.GetComponent<CharacterController>();
            diverController = user.GetComponent<DiverController>();
            cameraTarget = diverController != null ? diverController.CameraTarget : null;
            if (diverController != null) diverController.enabled = false;
            if (controller != null) controller.enabled = false;

            start = diver.position;
            pass = passPoint.position - Vector3.up * eyeHeight;
            land = exitPoint.position;
            if (snapExitToGround &&
                Physics.Raycast(land + Vector3.up, Vector3.down, out RaycastHit hit, 5f, groundMask, QueryTriggerInteraction.Ignore))
            {
                land = hit.point;
                // Il pivot del diver non coincide con la base del capsule: lo si posa dove il controller lo lascerebbe
                if (controller != null)
                    land.y += controller.height * 0.5f - controller.center.y + controller.skinWidth;
            }
            // Bezier quadratica con controllo calcolato perché la curva passi per pass a metà corsa.
            control = 2f * pass - 0.5f * (start + land);

            startYaw = yaw = diver.eulerAngles.y;
            Vector3 flat = land - start;
            flat.y = 0f;
            exitYaw = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat).eulerAngles.y : startYaw;
            startPitch = pitch = cameraTarget != null ? Mathf.DeltaAngle(0f, cameraTarget.localEulerAngles.x) : 0f;
            // Seduto sul sedile inclinato la visuale ha un roll e la camera è spostata di lato: si azzerano durante la spinta
            startRoll = diverController != null ? diverController.ViewRoll : 0f;
            startCamera = cameraTarget != null ? cameraTarget.localPosition : new Vector3(0f, eyeHeight, 0f);

            elapsed = 0f;
            onExitStarted?.Invoke();
        }

        private void Update()
        {
            if (elapsed < 0f) return;

            elapsed += Time.deltaTime;
            float dt = Time.deltaTime;

            if (elapsed < crouchDuration)
                UpdateCrouch(elapsed / crouchDuration);
            else if (elapsed < crouchDuration + jumpDuration)
                UpdateJump((elapsed - crouchDuration) / jumpDuration, dt);
            else if (elapsed < crouchDuration + jumpDuration + landDuration)
                UpdateLanding((elapsed - crouchDuration - jumpDuration) / landDuration);
            else
                Finish();
        }

        private void UpdateCrouch(float t)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);

            // Si gira verso l'apertura e abbassa la testa caricando la spinta
            yaw = Mathf.LerpAngle(startYaw, Mathf.LerpAngle(startYaw, exitYaw, 0.5f), k);
            Vector3 eye = start + Vector3.up * eyeHeight;
            Vector3 toPass = passPoint.position - eye;
            float lookPass = -Mathf.Asin(Mathf.Clamp(toPass.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Lerp(startPitch, lookPass + crouchPitch, k);

            diver.SetPositionAndRotation(start, Quaternion.Euler(0f, yaw, 0f));
            ApplyCamera(-crouchDepth * k, pitch, 0f, Leveling(elapsed));
        }

        /// <summary>Raddrizzamento dal sedile: 0 all'inizio, 1 quando il diver ha passato il portello.</summary>
        private float Leveling(float time) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / (crouchDuration + jumpDuration * 0.4f)));

        private void UpdateJump(float t, float dt)
        {
            float s = Mathf.Clamp01(jumpCurve.Evaluate(t));
            Vector3 position = PathPoint(s);
            Vector3 tangent = PathPoint(Mathf.Min(1f, s + 0.02f)) - PathPoint(Mathf.Max(0f, s - 0.02f));

            float damping = 1f - Mathf.Exp(-8f * dt);
            if (tangent.sqrMagnitude > 0.000001f)
            {
                float pathPitch = -Mathf.Asin(Mathf.Clamp(tangent.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                pitch = Mathf.Lerp(pitch, Mathf.Clamp(pathPitch * pathLookWeight, -30f, 35f), damping);
            }
            yaw = Mathf.LerpAngle(yaw, exitYaw, 1f - Mathf.Exp(-5f * dt));

            // La testa torna su con la spinta e risente del moto a metà volo
            float rise = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 4f));
            diver.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            ApplyCamera(-crouchDepth * rise, pitch, Mathf.Sin(Mathf.PI * s) * flightRoll, Leveling(elapsed));
            landStartPitch = pitch;
        }

        private void UpdateLanding(float t)
        {
            // Le ginocchia assorbono: discesa rapida e risalita lenta, sguardo che si assesta in avanti
            float dip = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f));
            pitch = Mathf.Lerp(landStartPitch, 0f, Mathf.SmoothStep(0f, 1f, t)) + landPitchKick * dip;
            yaw = Mathf.LerpAngle(yaw, exitYaw, Mathf.SmoothStep(0f, 1f, t));

            diver.SetPositionAndRotation(land, Quaternion.Euler(0f, yaw, 0f));
            ApplyCamera(-landDepth * dip, pitch, 0f, 1f);
        }

        private Vector3 PathPoint(float s)
        {
            float u = 1f - s;
            Vector3 point = u * u * start + 2f * u * s * control + s * s * land;
            // Parabola solo oltre il portello, per non toccare il bordo dell'apertura
            float after = Mathf.Clamp01((s - 0.5f) / 0.5f);
            return point + Vector3.up * (arcHeight * Mathf.Sin(Mathf.PI * after));
        }

        private void ApplyCamera(float heightOffset, float cameraPitch, float roll, float leveled)
        {
            if (cameraTarget == null) return;
            Vector3 standing = new Vector3(0f, eyeHeight, 0f);
            cameraTarget.localPosition = Vector3.Lerp(startCamera, standing, leveled) + Vector3.up * heightOffset;
            cameraTarget.localRotation = Quaternion.Euler(cameraPitch, 0f, roll + Mathf.Lerp(startRoll, 0f, leveled));
        }

        private void Finish()
        {
            elapsed = -1f;
            diver.SetPositionAndRotation(land, Quaternion.Euler(0f, exitYaw, 0f));
            ApplyCamera(0f, 0f, 0f, 1f);

            if (controller != null) controller.enabled = true;
            if (diverController != null)
            {
                diverController.ViewRoll = 0f;
                diverController.CameraBasePosition = new Vector3(0f, eyeHeight, 0f);
                diverController.enabled = true;
                diverController.SetViewPitch(0f);
                diverController.ResetMotion();
                diverController.LockMovement = false;
            }
            onExited?.Invoke();
        }
    }
}

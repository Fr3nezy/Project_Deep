using Deeploration.Interaction;
using Deeploration.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Environment
{
    /// <summary>
    /// Uscita guidata dalla capsula: con il portello aperto il diver preme [E] e viene portato
    /// attraverso l'apertura fino al punto d'uscita, a controlli bloccati.
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
        [SerializeField] private float duration = 2.2f;
        [SerializeField] private string promptText = "Esci";

        [Header("Eventi")]
        public UnityEvent onExitStarted = new UnityEvent();
        public UnityEvent onExited = new UnityEvent();

        private Transform diver;
        private CharacterController controller;
        private DiverController diverController;
        private Vector3 start, pass;
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
            if (diverController != null) diverController.enabled = false;
            if (controller != null) controller.enabled = false;

            start = diver.position;
            pass = passPoint.position - Vector3.up * eyeHeight;
            elapsed = 0f;
            onExitStarted?.Invoke();
        }

        private void Update()
        {
            if (elapsed < 0f) return;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f);
            // Bezier quadratica con controllo calcolato perché la curva passi per pass a metà corsa.
            Vector3 control = 2f * pass - 0.5f * (start + exitPoint.position);
            float u = 1f - t;
            diver.position = u * u * start + 2f * u * t * control + t * t * exitPoint.position;

            if (t < 1f) return;

            elapsed = -1f;
            if (controller != null) controller.enabled = true;
            if (diverController != null)
            {
                diverController.enabled = true;
                diverController.ResetMotion();
            }
            onExited?.Invoke();
        }
    }
}

using Deeploration.Interaction;
using Deeploration.Player;
using UnityEngine;

namespace Deeploration.Audio
{
    /// <summary>
    /// Suoni del diver legati a eventi di codice (non UnityEvent): passi, cambio modalità della torcia, presa di un oggetto.
    /// Riferimenti vuoti = cercati sul diver. Ogni SfxCue mancante spegne solo il suo suono.
    /// </summary>
    public sealed class PlayerSfx : MonoBehaviour
    {
        [SerializeField] private DiverController diver;
        [SerializeField] private DiverFlashlight flashlight;
        [SerializeField] private PlayerGrab grab;
        [SerializeField] private SfxCue footstep;
        [SerializeField] private SfxCue torchClick;
        [SerializeField] private SfxCue grabbed;

        private void Awake()
        {
            if (diver == null) diver = GetComponentInParent<DiverController>();
            if (flashlight == null) flashlight = FindFirstObjectByType<DiverFlashlight>();
            if (grab == null) grab = GetComponentInParent<PlayerGrab>();
        }

        private void OnEnable()
        {
            if (diver != null) diver.OnFootstep += PlayFootstep;
            if (flashlight != null) flashlight.OnModeChanged += PlayTorch;
            if (grab != null) grab.OnGrabbed += PlayGrab;
        }

        private void OnDisable()
        {
            if (diver != null) diver.OnFootstep -= PlayFootstep;
            if (flashlight != null) flashlight.OnModeChanged -= PlayTorch;
            if (grab != null) grab.OnGrabbed -= PlayGrab;
        }

        private void PlayFootstep() { if (footstep != null) footstep.Play(); }
        private void PlayTorch(FlashlightMode mode) { if (torchClick != null) torchClick.Play(); }
        private void PlayGrab(Grabbable target) { if (grabbed != null) grabbed.Play(); }
    }
}

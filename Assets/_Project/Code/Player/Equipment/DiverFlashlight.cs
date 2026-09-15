using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deeploration.Player
{
    public enum FlashlightMode
    {
        Full,
        Low,
        Off
    }

    public sealed class DiverFlashlight : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField] private Light spotLight;

        [Header("Parametri Modalità")]
        [SerializeField] private FlashlightMode initialMode = FlashlightMode.Full;
        [SerializeField] private KeyCode toggleKey = KeyCode.F;

        [Header("Full - Fascio Diretto")]
        [SerializeField, Min(0f)] private float fullIntensity = 55f;
        [SerializeField, Min(0f)] private float fullRange = 15f;
        [SerializeField, Range(0f, 1f)] private float fullRelief = 1f;

        [Header("Low - Diffusa / Interna")]
        [SerializeField, Min(0f)] private float lowIntensity = 8f;
        [SerializeField, Min(0f)] private float lowRange = 4f;
        [SerializeField, Range(0f, 1f)] private float lowRelief = 0.4f;

        [Header("Off - Spenta")]
        [SerializeField, Range(0f, 1f)] private float offRelief = 0f;

        [Header("Debug (sola lettura)")]
        [SerializeField] private FlashlightMode currentMode;
        [SerializeField] private float currentRelief;

        public FlashlightMode CurrentMode => currentMode;
        public float CurrentRelief => currentRelief;

        public event Action<FlashlightMode> OnModeChanged;

        private void Awake()
        {
            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light>(true);
            }

            SetMode(initialMode);
        }

        private void Update()
        {
            bool toggleRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                toggleRequested = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(toggleKey))
                toggleRequested = true;
#endif

            if (toggleRequested)
            {
                CycleMode();
            }
        }

        public void CycleMode()
        {
            FlashlightMode nextMode = currentMode switch
            {
                FlashlightMode.Full => FlashlightMode.Low,
                FlashlightMode.Low => FlashlightMode.Off,
                FlashlightMode.Off => FlashlightMode.Full,
                _ => FlashlightMode.Full
            };

            SetMode(nextMode);
        }

        public void SetMode(FlashlightMode mode)
        {
            currentMode = mode;

            switch (mode)
            {
                case FlashlightMode.Full:
                    if (spotLight != null)
                    {
                        spotLight.enabled = true;
                        spotLight.intensity = fullIntensity;
                        spotLight.range = fullRange;
                    }
                    currentRelief = fullRelief;
                    break;

                case FlashlightMode.Low:
                    if (spotLight != null)
                    {
                        spotLight.enabled = true;
                        spotLight.intensity = lowIntensity;
                        spotLight.range = lowRange;
                    }
                    currentRelief = lowRelief;
                    break;

                case FlashlightMode.Off:
                    if (spotLight != null)
                    {
                        spotLight.enabled = false;
                    }
                    currentRelief = offRelief;
                    break;
            }

            OnModeChanged?.Invoke(currentMode);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deeploration.Player
{
    public enum FlashlightMode
    {
        Full,
        Low,
        /// <summary>Luci dello scafandro spente, resta solo la spia rossa d'emergenza (ex Off).</summary>
        Red
    }

    public sealed class DiverFlashlight : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField] private Light spotLight;

        [Tooltip("Spia rossa d'emergenza dello scafandro, accesa solo in modalità Red. Opzionale.")]
        [SerializeField] private Light emergencyLight;

        [Header("Parametri Modalità")]
        [SerializeField] private FlashlightMode initialMode = FlashlightMode.Full;
        [SerializeField] private KeyCode toggleKey = KeyCode.F;

        [Tooltip("Tenendo premuto il tasto per almeno questi secondi si passa subito a Red; un tocco più breve cicla.")]
        [SerializeField, Min(0.05f)] private float holdToRedSeconds = 0.35f;

        [Header("Full - Fascio Diretto")]
        [SerializeField, Min(0f)] private float fullIntensity = 55f;
        [SerializeField, Min(0f)] private float fullRange = 15f;
        [SerializeField, Range(0f, 1f)] private float fullRelief = 1f;

        [Header("Low - Diffusa / Interna")]
        [SerializeField, Min(0f)] private float lowIntensity = 8f;
        [SerializeField, Min(0f)] private float lowRange = 4f;
        [SerializeField, Range(0f, 1f)] private float lowRelief = 0.4f;

        [Header("Red - Spia d'emergenza")]
        [FormerlySerializedAs("offRelief")]
        [SerializeField, Range(0f, 1f)] private float redRelief = 0f;

        [Header("Debug (sola lettura)")]
        [SerializeField] private FlashlightMode currentMode;
        [SerializeField] private float currentRelief;

        private float pressStartTime = -1f;
        private bool holdConsumed;

        public FlashlightMode CurrentMode => currentMode;
        public float CurrentRelief => currentRelief;

        /// <summary>Luce del fascio (può essere null). Posizione e direzione servono agli stimoli delle creature.</summary>
        public Light SpotLight => spotLight;

        public event Action<FlashlightMode> OnModeChanged;

        private void Awake()
        {
            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light>(true);
            }

            SetMode(initialMode);
        }

        private void OnDisable()
        {
            pressStartTime = -1f;
        }

        private void Update()
        {
            bool pressed = false;
            bool held = false;
            bool released = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed |= Keyboard.current.fKey.wasPressedThisFrame;
                held |= Keyboard.current.fKey.isPressed;
                released |= Keyboard.current.fKey.wasReleasedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(toggleKey);
            held |= Input.GetKey(toggleKey);
            released |= Input.GetKeyUp(toggleKey);
#endif

            if (pressed)
            {
                pressStartTime = Time.unscaledTime;
                holdConsumed = false;
            }

            if (pressStartTime < 0f) return;

            // Tenuto premuto: passa a Red appena scatta la soglia, senza aspettare il rilascio.
            if (held && !holdConsumed && Time.unscaledTime - pressStartTime >= holdToRedSeconds)
            {
                holdConsumed = true;
                SetMode(FlashlightMode.Red);
            }

            if (released)
            {
                if (!holdConsumed) CycleMode();
                pressStartTime = -1f;
            }
        }

        public void CycleMode()
        {
            FlashlightMode nextMode = currentMode switch
            {
                FlashlightMode.Full => FlashlightMode.Low,
                FlashlightMode.Low => FlashlightMode.Red,
                FlashlightMode.Red => FlashlightMode.Full,
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

                case FlashlightMode.Red:
                    if (spotLight != null)
                    {
                        spotLight.enabled = false;
                    }
                    currentRelief = redRelief;
                    break;
            }

            if (emergencyLight != null)
            {
                emergencyLight.enabled = mode == FlashlightMode.Red;
            }

            OnModeChanged?.Invoke(currentMode);
        }
    }
}

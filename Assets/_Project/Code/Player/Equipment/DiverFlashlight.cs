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
        [SerializeField] private KeyCode redKey = KeyCode.R;

        [Tooltip("Una pressione più lunga di questi secondi non cicla: con il flash sbloccato inizia a caricarlo.")]
        [FormerlySerializedAs("holdToRedSeconds")]
        [SerializeField, Min(0.05f)] private float holdToChargeSeconds = 0.35f;

        [Header("Flash (modulo a condensatore)")]
        [Tooltip("Senza il modulo il flash non si carica. Lo sblocca UnlockFlash (raccolta del modulo).")]
        [SerializeField] private bool flashUnlocked;

        [Tooltip("Secondi per riempire la carica, contati dopo holdToChargeSeconds.")]
        [SerializeField, Min(0f)] private float flashChargeSeconds = 1f;

        [Tooltip("Sweet spot: secondi a carica piena in cui il rilascio dà il lampo pieno. Rilascio prima = lampo debole; " +
                 "oltre, il condensatore si scarica da solo con un lampo debole.")]
        [SerializeField, Min(0.05f)] private float sweetSpotSeconds = 0.4f;

        [SerializeField, Min(0f)] private float flashIntensity = 800f;
        [SerializeField, Min(0f)] private float flashRange = 30f;
        [Tooltip("Lampo fuori dallo sweet spot: sotto la soglia della fotocellula (200).")]
        [SerializeField, Min(0f)] private float weakFlashIntensity = 150f;
        [SerializeField, Min(0f)] private float weakFlashRange = 12f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.15f;

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
        private float flashEndTime = float.NegativeInfinity;

        public FlashlightMode CurrentMode => currentMode;
        public float CurrentRelief => currentRelief;
        public bool FlashUnlocked => flashUnlocked;

        /// <summary>Carica del flash 0-1 mentre il tasto è tenuto premuto, 0 altrimenti.</summary>
        public float FlashCharge { get; private set; }

        /// <summary>Vero a carica piena, nella finestra in cui il rilascio dà il lampo pieno.</summary>
        public bool FlashInSweetSpot { get; private set; }

        /// <summary>Vero mentre il lampo è acceso.</summary>
        public bool IsFlashing => Time.time < flashEndTime;

        /// <summary>Luce del fascio (può essere null). Posizione e direzione servono agli stimoli delle creature.</summary>
        public Light SpotLight => spotLight;

        public event Action<FlashlightMode> OnModeChanged;

        /// <summary>Invocato quando parte un lampo, prima che la luce torni alla modalità corrente.</summary>
        public event Action OnFlashFired;

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
            EndPress();
            if (IsFlashing)
            {
                flashEndTime = float.NegativeInfinity;
                ApplyLight();
            }
        }

        private void Update()
        {
            if (flashEndTime > float.NegativeInfinity && !IsFlashing)
            {
                flashEndTime = float.NegativeInfinity;
                ApplyLight();
            }

            bool pressed = false;
            bool held = false;
            bool released = false;
            bool redPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed |= Keyboard.current.fKey.wasPressedThisFrame;
                held |= Keyboard.current.fKey.isPressed;
                released |= Keyboard.current.fKey.wasReleasedThisFrame;
                redPressed |= Keyboard.current.rKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(toggleKey);
            held |= Input.GetKey(toggleKey);
            released |= Input.GetKeyUp(toggleKey);
            redPressed |= Input.GetKeyDown(redKey);
#endif

            if (redPressed) SetMode(currentMode == FlashlightMode.Red ? FlashlightMode.Full : FlashlightMode.Red);

            if (pressed) pressStartTime = Time.unscaledTime;
            if (pressStartTime < 0f) return;

            float heldFor = Time.unscaledTime - pressStartTime;
            bool charging = flashUnlocked && heldFor >= holdToChargeSeconds;
            if (held && charging)
            {
                float t = heldFor - holdToChargeSeconds;
                FlashCharge = flashChargeSeconds <= 0f ? 1f : Mathf.Clamp01(t / flashChargeSeconds);
                FlashInSweetSpot = t >= flashChargeSeconds;
                if (t > flashChargeSeconds + sweetSpotSeconds)
                {
                    // Sovraccarica: il condensatore si scarica da solo.
                    FireFlash(false);
                    EndPress();
                    return;
                }
            }

            if (released)
            {
                if (heldFor < holdToChargeSeconds) CycleMode();
                else if (charging) FireFlash(FlashInSweetSpot);
                EndPress();
            }
        }

        private void EndPress()
        {
            pressStartTime = -1f;
            FlashCharge = 0f;
            FlashInSweetSpot = false;
        }

        public void CycleMode()
        {
            FlashlightMode nextMode = currentMode == FlashlightMode.Full ? FlashlightMode.Low : FlashlightMode.Full;
            SetMode(nextMode);
        }

        /// <summary>Sblocca il flash (raccolta del modulo a condensatore).</summary>
        public void UnlockFlash() => flashUnlocked = true;

        /// <summary>
        /// Lampo: per flashDuration il fascio va a flashIntensity e flashRange (pieno) o ai valori deboli, anche da
        /// Red, poi torna alla modalità corrente senza invocare OnModeChanged.
        /// </summary>
        public void FireFlash(bool full = true)
        {
            if (!flashUnlocked) return;

            flashEndTime = Time.time + flashDuration;
            if (spotLight != null)
            {
                spotLight.enabled = true;
                spotLight.intensity = full ? flashIntensity : weakFlashIntensity;
                spotLight.range = full ? flashRange : weakFlashRange;
            }
            OnFlashFired?.Invoke();
        }

        public void SetMode(FlashlightMode mode)
        {
            currentMode = mode;
            currentRelief = mode switch
            {
                FlashlightMode.Full => fullRelief,
                FlashlightMode.Low => lowRelief,
                _ => redRelief
            };

            // Durante il lampo la luce resta al massimo: ApplyLight la riporta alla modalità a lampo finito.
            if (!IsFlashing) ApplyLight();

            if (emergencyLight != null)
            {
                emergencyLight.enabled = mode == FlashlightMode.Red;
            }

            OnModeChanged?.Invoke(currentMode);
        }

        private void ApplyLight()
        {
            if (spotLight == null) return;

            spotLight.enabled = currentMode != FlashlightMode.Red;
            spotLight.intensity = currentMode == FlashlightMode.Full ? fullIntensity : lowIntensity;
            spotLight.range = currentMode == FlashlightMode.Full ? fullRange : lowRange;
        }
    }
}

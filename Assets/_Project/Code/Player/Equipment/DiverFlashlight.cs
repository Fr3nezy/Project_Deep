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
        /// <summary>
        /// Fascio rosso del modulo trovato nella Z0 (ex Off): la fauna non lo vede, la fotocellula sì. Prima dello
        /// sblocco non si seleziona con R; se ci si parte (risveglio in capsula) resta la sola spia d'emergenza.
        /// </summary>
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
        [Tooltip("Falso = fascio spento finché qualcuno non chiama PowerBeam (es. uscita dalla capsula). F e R cambiano modo ma il fascio resta spento.")]
        [SerializeField] private bool beamPowered = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F;
        [SerializeField] private KeyCode redKey = KeyCode.R;

        [Header("Modulo luce rossa")]
        [Tooltip("Senza il modulo R non fa niente. Lo sblocca UnlockRed (raccolta del modulo nella Z0).")]
        [SerializeField] private bool redUnlocked;

        [Header("Full - Fascio Diretto")]
        [SerializeField, Min(0f)] private float fullIntensity = 55f;
        [SerializeField, Min(0f)] private float fullRange = 15f;
        [SerializeField, Range(0f, 1f)] private float fullRelief = 1f;

        [Header("Low - Diffusa / Interna")]
        [SerializeField, Min(0f)] private float lowIntensity = 8f;
        [SerializeField, Min(0f)] private float lowRange = 4f;
        [SerializeField, Range(0f, 1f)] private float lowRelief = 0.4f;

        [Header("Red - Fascio rosso")]
        [Tooltip("Corto e fioco: si vede poco, ma la fauna non lo vede.")]
        [SerializeField, Min(0f)] private float redIntensity = 6f;
        [SerializeField, Min(0f)] private float redRange = 8f;
        [SerializeField] private Color redColor = new Color(1f, 0.08f, 0.04f);
        [Tooltip("Il rosso non toglie l'ansia da buio: il costo di restare invisibili.")]
        [FormerlySerializedAs("offRelief")]
        [SerializeField, Range(0f, 1f)] private float redRelief = 0f;

        [Header("Surriscaldamento rossa")]
        [Tooltip("Secondi di rosso continuo a stress zero prima che il modulo vada in protezione.")]
        [SerializeField, Min(1f)] private float redMaxSeconds = 20f;
        [Tooltip("Accelerazione del riscaldamento a stress pieno: 1 = il doppio più veloce.")]
        [SerializeField, Min(0f)] private float stressHeatBoost = 1f;
        [Tooltip("Stress del sub. Vuoto: cercato in scena; se manca, il riscaldamento ignora lo stress.")]
        [SerializeField] private StressSystem stress;
        [Tooltip("Secondi per raffreddarsi da caldo a freddo. Dopo un surriscaldamento il rosso resta bloccato finché non è freddo.")]
        [SerializeField, Min(0.1f)] private float redCooldownSeconds = 10f;
        [Tooltip("Ultimi secondi in cui il fascio sfarfalla fino a spegnersi.")]
        [SerializeField, Min(0f)] private float flickerSeconds = 4f;
        [Tooltip("Ronzio del modulo: cala dal volume pieno a zero mentre si scalda. Vuoto: sintetizzato.")]
        [SerializeField] private AudioClip humClip;
        [SerializeField, Range(0f, 1f)] private float humVolume = 0.35f;

        [Header("Debug (sola lettura)")]
        [SerializeField] private FlashlightMode currentMode;
        [SerializeField] private float currentRelief;
        [SerializeField, Range(0f, 1f)] private float redHeat;
        [SerializeField] private bool redOverheated;

        private Color beamColor;
        private AudioSource hum;

        public FlashlightMode CurrentMode => currentMode;
        public float CurrentRelief => currentRelief;
        public bool RedUnlocked => redUnlocked;
        /// <summary>Calore del modulo rosso, 0 freddo, 1 surriscaldato.</summary>
        public float RedHeat => redHeat;

        /// <summary>Luce del fascio (può essere null). Posizione e direzione servono agli stimoli delle creature.</summary>
        public Light SpotLight => spotLight;
        public bool BeamPowered => beamPowered;

        public event Action<FlashlightMode> OnModeChanged;

        private void Awake()
        {
            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light>(true);
            }
            if (spotLight != null) beamColor = spotLight.color;
            if (stress == null) stress = FindAnyObjectByType<StressSystem>();

            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = humClip != null ? humClip : SynthHum();
            hum.loop = true;
            hum.playOnAwake = false;
            hum.spatialBlend = 0f;

            SetMode(initialMode);
        }

        private void Update()
        {
            bool pressed = false;
            bool redPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed |= Keyboard.current.fKey.wasPressedThisFrame;
                redPressed |= Keyboard.current.rKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(toggleKey);
            redPressed |= Input.GetKeyDown(redKey);
#endif

            if (redPressed && redUnlocked)
            {
                if (currentMode == FlashlightMode.Red) SetMode(FlashlightMode.Full);
                else if (!redOverheated) SetMode(FlashlightMode.Red);
            }
            if (pressed) CycleMode();

            UpdateRedHeat();
        }

        // Il rosso regge redMaxSeconds (meno se stressati): il ronzio cala, il fascio sfarfalla e a fine corsa il
        // modulo torna da solo alla luce bassa. Si raffredda solo spento.
        private void UpdateRedHeat()
        {
            bool redActive = currentMode == FlashlightMode.Red && redUnlocked;
            if (redActive)
            {
                float stress01 = stress != null ? stress.NormalizedStress : 0f;
                redHeat = Mathf.Min(1f, redHeat + Time.deltaTime / redMaxSeconds * (1f + stressHeatBoost * stress01));
            }
            else
            {
                redHeat = Mathf.Max(0f, redHeat - Time.deltaTime / redCooldownSeconds);
                if (redHeat <= 0f) redOverheated = false;
            }

            if (redActive && redHeat >= 1f)
            {
                redOverheated = true;
                hum.Stop();
                SetMode(FlashlightMode.Low);
                return;
            }

            if (!redActive)
            {
                if (hum.isPlaying) hum.Stop();
                return;
            }

            if (!hum.isPlaying) hum.Play();
            hum.volume = humVolume * (1f - redHeat);

            if (spotLight == null) return;
            float flickerStart = 1f - flickerSeconds / redMaxSeconds;
            float flicker = flickerSeconds > 0f ? Mathf.InverseLerp(flickerStart, 1f, redHeat) : 0f;
            bool on = flicker <= 0f || Mathf.PerlinNoise(Time.time * 18f, 0.37f) > flicker;
            spotLight.intensity = on ? redIntensity * Mathf.Lerp(1f, 0.5f, flicker) : 0f;
        }

        // Ronzio di rete a 100 Hz con armoniche, 1 s esatto: ogni frequenza fa cicli interi e il loop non scatta.
        private static AudioClip SynthHum()
        {
            const int rate = 44100;
            var data = new float[rate];
            for (int i = 0; i < rate; i++)
            {
                float t = i / (float)rate;
                float value = 0.5f * Mathf.Sin(2f * Mathf.PI * 100f * t)
                    + 0.3f * Mathf.Sin(2f * Mathf.PI * 200f * t)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * 300f * t)
                    + 0.08f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 100f * t)); // ronzio elettrico, non sinusoide pulita
                data[i] = value * (1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 4f * t)) * 0.7f;
            }
            AudioClip clip = AudioClip.Create("RedTorch_Hum", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void CycleMode()
        {
            FlashlightMode nextMode = currentMode == FlashlightMode.Full ? FlashlightMode.Low : FlashlightMode.Full;
            SetMode(nextMode);
        }

        /// <summary>Accende il fascio (uscita dalla capsula, teletrasporti del menu cheat).</summary>
        public void PowerBeam()
        {
            beamPowered = true;
            ApplyLight();
        }

        /// <summary>Sblocca la luce rossa (raccolta del modulo nella Z0).</summary>
        public void UnlockRed()
        {
            redUnlocked = true;
            ApplyLight();
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

            ApplyLight();

            if (emergencyLight != null)
            {
                emergencyLight.enabled = mode == FlashlightMode.Red;
            }

            OnModeChanged?.Invoke(currentMode);
        }

        private void ApplyLight()
        {
            if (spotLight == null) return;

            bool red = currentMode == FlashlightMode.Red;
            // Senza modulo (risveglio in capsula) il rosso è la sola spia d'emergenza: fascio spento, la fotocellula non scatta.
            spotLight.enabled = beamPowered && (!red || redUnlocked);
            spotLight.color = red ? redColor : beamColor;
            spotLight.intensity = red ? redIntensity : currentMode == FlashlightMode.Full ? fullIntensity : lowIntensity;
            spotLight.range = red ? redRange : currentMode == FlashlightMode.Full ? fullRange : lowRange;
        }
    }
}

using TMPro;
using UnityEngine;

namespace Deeploration.Player
{
    /// <summary>
    /// Strumenti del casco, in 3D: il modello (<c>Helmet.fbx</c>) ha il gauge dell'ossigeno con il quadrante della
    /// pressione, il CRT ambrato e due spie; questo componente ruota le lancette, accende le spie e scrive i testi del
    /// CRT (TextMeshPro, così font e traduzioni si cambiano qui e non nel modello). Legge OxygenSystem e
    /// DiverFlashlight, non scrive su nessun sistema.
    /// </summary>
    public sealed class HelmetInstruments : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Amber = new(1f, 0.69f, 0.25f);
        private static readonly Color EmergencyRed = new(1f, 0.27f, 0.2f);
        private static readonly Color LampYellow = new(0.94f, 0.82f, 0.35f);
        private static readonly Color LampOrange = new(1f, 0.55f, 0.16f);
        private static readonly Color LampRed = new(1f, 0.24f, 0.2f);

        [Header("Riferimenti (auto-trovati se vuoti)")]
        [SerializeField] private OxygenSystem oxygen;
        [SerializeField] private DiverFlashlight flashlight;
        [Tooltip("Dà la quota per la profondità. Default: il transform dell'OxygenSystem.")]
        [SerializeField] private Transform depthSource;

        [Header("Lancette (perni del modello: Y = su, Z = verso chi guarda)")]
        [SerializeField] private Transform oxygenNeedle;
        [SerializeField] private Transform pressureNeedle;

        [Header("Spie")]
        [SerializeField] private Renderer oxygenLamp;
        [SerializeField] private Renderer pressureLamp;
        [Tooltip("Intensità dell'emissione di una spia accesa (HDR).")]
        [SerializeField, Min(0f)] private float lampIntensity = 3f;

        [Header("CRT")]
        [SerializeField] private TMP_Text depthLabel;
        [SerializeField] private TMP_Text depthValue;
        [SerializeField] private TMP_Text lightLabel;
        [SerializeField] private TMP_Text lightValue;
        [Tooltip("Le tre tacche della torcia, dalla prima all'ultima.")]
        [SerializeField] private Renderer[] lightBars;
        [Tooltip("Intensità dell'emissione di una tacca accesa (HDR).")]
        [SerializeField, Min(0f)] private float barIntensity = 2f;

        [Header("Lancette: gradi")]
        [Tooltip("Rotazione della lancetta dalla fine verde (0) alla fine rossa (1) della scala: 270° in senso orario " +
                 "a partire da in basso a sinistra.")]
        [SerializeField, Range(90f, 359f)] private float sweepDegrees = 270f;
        [Tooltip("Prontezza con cui la lancetta raggiunge il valore (1/s).")]
        [SerializeField, Min(0.1f)] private float needleResponse = 6f;

        [Header("Profondità")]
        [SerializeField] private float startDepth = 2040f;
        [Tooltip("Metri di profondità per ogni metro di scena in verticale: amplifica la discesa.")]
        [SerializeField, Min(0.01f)] private float metersPerUnit = 2f;
        [SerializeField] private float orangeDepth = 2200f;
        [SerializeField] private float redDepth = 2400f;
        [SerializeField] private float gaugeMinDepth = 1800f;
        [SerializeField] private float gaugeMaxDepth = 2600f;

        private MaterialPropertyBlock block;
        private Quaternion oxygenRest, pressureRest;
        private float oxygenFraction, pressureFraction;
        private float startY;
        private int shownDepth = int.MinValue;
        private FlashlightMode? shownMode;

        private void Awake()
        {
            if (oxygen == null) oxygen = FindFirstObjectByType<OxygenSystem>();
            if (flashlight == null) flashlight = FindFirstObjectByType<DiverFlashlight>();
            if (depthSource == null && oxygen != null) depthSource = oxygen.transform;
            if (oxygenNeedle != null) oxygenRest = oxygenNeedle.localRotation;
            if (pressureNeedle != null) pressureRest = pressureNeedle.localRotation;
            block = new MaterialPropertyBlock();
        }

        private void Start()
        {
            startY = depthSource != null ? depthSource.position.y : 0f;
            // Partono dove sono i valori attuali, senza il giro iniziale.
            oxygenFraction = TargetOxygenFraction();
            pressureFraction = PressureFraction(CurrentDepth());
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            float depth = CurrentDepth();
            int pressureBand = depth >= redDepth ? 3 : depth >= orangeDepth ? 2 : 0;
            int oxygenBand = oxygen == null ? 0 : oxygen.Level switch
            {
                OxygenLevel.Safe => 0,
                OxygenLevel.Warning => 1,
                OxygenLevel.Critical => 2,
                _ => 3
            };
            FlashlightMode mode = flashlight != null ? flashlight.CurrentMode : FlashlightMode.Full;

            float blend = 1f - Mathf.Exp(-needleResponse * Time.deltaTime);
            oxygenFraction = Mathf.Lerp(oxygenFraction, TargetOxygenFraction(), blend);
            pressureFraction = Mathf.Lerp(pressureFraction, PressureFraction(depth), blend);
            // Nelle fasce d'allarme la lancetta trema.
            RotateNeedle(oxygenNeedle, oxygenRest, oxygenFraction, oxygenBand >= 2 ? Mathf.Sin(time * 30f) * 1.2f : 0f);
            RotateNeedle(pressureNeedle, pressureRest, pressureFraction, pressureBand >= 2 ? Mathf.Sin(time * 25f) * 1.8f : 0f);

            SetLamp(oxygenLamp, oxygenBand, time);
            SetLamp(pressureLamp, pressureBand, time);
            UpdateCrt(depth, pressureBand, mode, time);
        }

        private float CurrentDepth() =>
            depthSource != null ? startDepth + (startY - depthSource.position.y) * metersPerUnit : startDepth;

        // La scala del gauge va dal verde (pieno) al rosso (vuoto): la lancetta si muove con l'ossigeno consumato.
        private float TargetOxygenFraction() => oxygen != null ? 1f - oxygen.NormalizedOxygen : 0f;

        private float PressureFraction(float depth) => Mathf.InverseLerp(gaugeMinDepth, gaugeMaxDepth, depth);

        // A frazione 0 la lancetta punta in basso a sinistra, a 1 in basso a destra. Unity ruota in senso orario
        // per angoli positivi visto da chi guarda (Z locale verso lo spettatore).
        private void RotateNeedle(Transform needle, Quaternion rest, float fraction, float jitterDegrees)
        {
            if (needle == null) return;
            float angle = sweepDegrees * Mathf.Clamp01(fraction) - sweepDegrees * 0.5f + jitterDegrees;
            needle.localRotation = rest * Quaternion.Euler(0f, 0f, angle);
        }

        private void SetLamp(Renderer lamp, int band, float time)
        {
            if (lamp == null) return;
            Color color = band == 1 ? LampYellow : band == 2 ? LampOrange : LampRed;
            float on = band == 0 ? 0f : band == 1 ? 1f : Mathf.Sin(time * (band == 3 ? 14f : 6f)) > 0f ? 1f : 0.15f;
            WriteEmission(lamp, color * (on * lampIntensity));
        }

        private void UpdateCrt(float depth, int pressureBand, FlashlightMode mode, float time)
        {
            Color ink = mode == FlashlightMode.Red ? EmergencyRed : Amber;
            Color dim = ink * 0.55f;
            dim.a = 1f;

            if (depthLabel != null) depthLabel.color = dim;
            if (lightLabel != null) lightLabel.color = dim;
            if (lightValue != null) lightValue.color = ink;

            if (depthValue != null)
            {
                int meters = Mathf.RoundToInt(depth);
                if (meters != shownDepth)
                {
                    shownDepth = meters;
                    depthValue.SetText(meters.ToString("0000") + " M");
                }
                // Oltre il limite dello scafo il numero lampeggia in rosso.
                Color depthColor = pressureBand == 3 ? LampRed * (Mathf.Sin(time * 6f) > 0f ? 1f : 0.4f) : ink;
                depthColor.a = 1f;
                depthValue.color = depthColor;
            }

            if (shownMode != mode)
            {
                shownMode = mode;
                if (lightValue != null) lightValue.SetText(mode == FlashlightMode.Full ? "PIENA" : mode == FlashlightMode.Low ? "BASSA" : "ROSSA");
            }

            int lit = mode == FlashlightMode.Full ? 3 : 1;
            if (lightBars != null)
            {
                for (int i = 0; i < lightBars.Length; i++)
                    WriteEmission(lightBars[i], ink * ((i < lit ? 1f : 0.12f) * barIntensity));
            }
        }

        private void WriteEmission(Renderer target, Color color)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, color);
            target.SetPropertyBlock(block);
        }
    }
}

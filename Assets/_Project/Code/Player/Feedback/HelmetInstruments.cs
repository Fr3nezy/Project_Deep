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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Amber = new(1f, 0.69f, 0.25f);
        private static readonly Color EmergencyRed = new(1f, 0.27f, 0.2f);
        private static readonly Color LampYellow = new(0.94f, 0.82f, 0.35f);
        private static readonly Color LampOrange = new(1f, 0.55f, 0.16f);
        private static readonly Color LampRed = new(1f, 0.24f, 0.2f);
        private static readonly Color SweetGreen = new(0.3f, 1f, 0.45f);

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

        [Header("Luce rossa")]
        [Tooltip("Materiali delle fasce verde, arancione e rossa delle scale: con la torcia rossa diventano tre rossi " +
                 "di luminosità crescente. Cercati sui renderer figli, per indice di sottomesh.")]
        [SerializeField] private Material greenScale;
        [SerializeField] private Material orangeScale;
        [SerializeField] private Material redScale;

        [Header("Bussola")]
        [Tooltip("Il nastro della bussola (Cylinder.007): un arco nel piano XY locale, centrato su compassCenter.")]
        [SerializeField] private Renderer compassStrip;
        [Tooltip("Dà la rotta. Default: la camera principale.")]
        [SerializeField] private Transform headingSource;
        [Tooltip("Centro dell'arco del nastro, nello spazio locale della mesh.")]
        [SerializeField] private Vector3 compassCenter = new(0f, -0.019f, 0.046f);
        [Tooltip("Raggio a cui stanno le lettere: poco dentro la faccia interna del nastro (r ≈ 0.10).")]
        [SerializeField, Min(0.001f)] private float compassRadius = 0.097f;
        [Tooltip("Metà dell'arco visibile del nastro, in gradi.")]
        [SerializeField, Range(10f, 90f)] private float compassArcHalf = 45f;
        [Tooltip("Metà della finestra di rotta mostrata sul nastro, in gradi.")]
        [SerializeField, Range(10f, 180f)] private float compassWindowHalf = 75f;
        [Tooltip("Altezza delle lettere, in unità del mondo.")]
        [SerializeField, Min(0.001f)] private float compassLetterHeight = 0.009f;

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
        private bool shownCharging;
        private FlashlightMode? scaleMode;
        private readonly System.Collections.Generic.List<(Renderer renderer, int index, int band)> scaleSlots = new();
        private static readonly string[] CompassNames = { "N", "E", "S", "O" };
        private readonly TMP_Text[] compassLetters = new TMP_Text[4];

        private void Awake()
        {
            if (headingSource == null && Camera.main != null) headingSource = Camera.main.transform;
            CollectScaleSlots();
            BuildCompassLetters();
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

            SetLamp(oxygenLamp, oxygenBand, mode, time);
            SetLamp(pressureLamp, pressureBand, mode, time);
            UpdateCrt(depth, pressureBand, mode, time);
            UpdateScales(mode);
            UpdateCompass(mode);
        }

        private void CollectScaleSlots()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    int band = mats[i] == null ? -1 : mats[i] == greenScale ? 0 : mats[i] == orangeScale ? 1 : mats[i] == redScale ? 2 : -1;
                    if (band >= 0) scaleSlots.Add((r, i, band));
                }
            }
        }

        // Con la torcia rossa le fasce diventano tre rossi distinti per luminosità; altrimenti tornano al materiale.
        private void UpdateScales(FlashlightMode mode)
        {
            if (scaleMode == mode) return;
            scaleMode = mode;
            bool red = mode == FlashlightMode.Red;
            foreach (var (r, index, band) in scaleSlots)
            {
                block.Clear();
                if (red)
                {
                    float k = band == 0 ? 0.12f : band == 1 ? 0.4f : 1f;
                    Color c = EmergencyRed * k;
                    c.a = 1f;
                    block.SetColor(BaseColorId, c);
                    block.SetColor(EmissionColorId, c);
                }
                r.SetPropertyBlock(block, index);
            }
        }

        private void BuildCompassLetters()
        {
            if (compassStrip == null || depthValue == null) return;
            for (int i = 0; i < compassLetters.Length; i++)
            {
                var go = new GameObject("Compass_" + CompassNames[i]);
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshPro>();
                t.font = depthValue.font;
                t.fontSharedMaterial = depthValue.fontSharedMaterial;
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.enableWordWrapping = false;
                t.rectTransform.sizeDelta = new Vector2(0.03f, 0.02f);
                t.text = CompassNames[i];
                t.fontSize = 1f;
                float lineHeight = t.GetPreferredValues("H").y; // altezza della riga a fontSize 1
                t.fontSize = compassLetterHeight / Mathf.Max(lineHeight, 1e-6f);
                compassLetters[i] = t;
            }
        }

        // Nastro: la lettera di un punto cardinale scorre sull'arco secondo lo scarto dalla rotta (destra = più a est),
        // e sfuma verso i bordi della finestra. Sulla mesh +X locale è la sinistra di chi guarda.
        private void UpdateCompass(FlashlightMode mode)
        {
            if (compassStrip == null) return;
            Color ink = mode == FlashlightMode.Red ? EmergencyRed : Amber;
            Color glow = ink * 0.12f;
            WriteEmission(compassStrip, glow);
            if (compassLetters[0] == null) return;

            float heading = headingSource != null ? headingSource.eulerAngles.y : 0f;
            Transform strip = compassStrip.transform;
            for (int i = 0; i < compassLetters.Length; i++)
            {
                float off = Mathf.DeltaAngle(heading, i * 90f);
                float fade = 1f - Mathf.Abs(off) / compassWindowHalf;
                var letter = compassLetters[i];
                letter.enabled = fade > 0f;
                if (fade <= 0f) continue;

                float angle = off / compassWindowHalf * compassArcHalf * Mathf.Deg2Rad;
                var outward = new Vector3(-Mathf.Sin(angle), -Mathf.Cos(angle), 0f);
                Vector3 worldOut = strip.TransformDirection(outward);
                letter.transform.SetPositionAndRotation(
                    strip.TransformPoint(compassCenter + outward * compassRadius),
                    Quaternion.LookRotation(worldOut, -strip.TransformDirection(Vector3.forward)));
                Color c = ink;
                c.a = fade;
                letter.color = c;
            }
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

        private void SetLamp(Renderer lamp, int band, FlashlightMode mode, float time)
        {
            if (lamp == null) return;
            Color color = mode == FlashlightMode.Red
                ? EmergencyRed * (band == 1 ? 0.35f : band == 2 ? 0.65f : 1f)
                : band == 1 ? LampYellow : band == 2 ? LampOrange : LampRed;
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

            // Carica del flash: le tacche si riempiono in fila e l'ultima diventa verde nello sweet spot.
            bool charging = flashlight != null && flashlight.FlashCharge > 0f;
            if (charging != shownCharging || (!charging && shownMode != mode))
            {
                shownCharging = charging;
                shownMode = mode;
                if (lightValue != null)
                    lightValue.SetText(charging ? "FLASH" : mode == FlashlightMode.Full ? "FULL" : mode == FlashlightMode.Low ? "LOW" : "RED");
            }

            if (lightBars == null) return;
            if (charging)
            {
                float fill = flashlight.FlashCharge * lightBars.Length;
                for (int i = 0; i < lightBars.Length; i++)
                {
                    bool last = i == lightBars.Length - 1;
                    Color barInk = last && flashlight.FlashInSweetSpot ? SweetGreen : ink;
                    WriteEmission(lightBars[i], barInk * (Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(fill - i)) * barIntensity));
                }
                return;
            }

            int lit = mode == FlashlightMode.Full ? 3 : 1;
            for (int i = 0; i < lightBars.Length; i++)
                WriteEmission(lightBars[i], ink * ((i < lit ? 1f : 0.12f) * barIntensity));
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

using Deeploration.Comms;
using Deeploration.Prologue;
using TMPro;
using UnityEngine;

namespace Deeploration.Player
{
    /// <summary>
    /// Strumenti del casco, in 3D: il modello (<c>Helmet.fbx</c>) ha il gauge dell'ossigeno con il quadrante della
    /// pressione, il CRT ambrato e due spie; questo componente ruota le lancette, accende le spie e scrive i testi del
    /// CRT (TextMeshPro, così font e traduzioni si cambiano qui e non nel modello). Legge OxygenSystem e
    /// DiverFlashlight, non scrive su nessun sistema. Con powerUpLine gli strumenti restano spenti finché HelmetComms
    /// non comincia quella battuta, poi si accendono a cascata: CRT, bussola, gauge.
    /// </summary>
    public sealed class HelmetInstruments : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
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
        [Tooltip("Icona della modalità accanto al nome (quad Unlit trasparente). Opzionale.")]
        [SerializeField] private Renderer lightIcon;
        [Tooltip("Icone in ordine di FlashlightMode: Full, Low, Red.")]
        [SerializeField] private Texture2D[] modeIcons = new Texture2D[3];

        [Header("Avvisi sul CRT")]
        [Tooltip("Schermata d'avviso (icona grande, titolo, tasto): nasconde i testi del CRT per noticeSeconds. Opzionale.")]
        [SerializeField] private GameObject noticeRoot;
        [SerializeField] private Renderer noticeIcon;
        [SerializeField] private TMP_Text noticeTitle;
        [SerializeField] private TMP_Text noticeKey;
        [SerializeField, Min(0.5f)] private float noticeSeconds = 4f;
        [Tooltip("Avviso alla raccolta del modulo luce rossa.")]
        [SerializeField] private string redTitle = "RED MODULE ONLINE";
        [SerializeField] private string redKey = "[R] USE";
        [Tooltip("Avviso della torcia, lanciato da ShowLightsNotice (in SCN_Gameplay all'uscita dalla capsula, HatchExit.onExited).")]
        [SerializeField] private string lightsTitle = "LIGHTS ONLINE";
        [SerializeField] private string lightsKey = "[F] CYCLE";
        [Tooltip("Durata dell'avviso della torcia: più lunga degli altri, è il primo momento in cui le luci servono.")]
        [SerializeField, Min(0.5f)] private float lightsNoticeSeconds = 8f;
        [Tooltip("Battuta comms con cui compare l'avviso della torcia (in SCN_Gameplay HELM_FISH_01). Vuoto = solo da ShowLightsNotice.")]
        [SerializeField] private SubtitleLine lightsNoticeLine;

        [Header("Accensione")]
        [Tooltip("Battuta comms con cui si accendono gli strumenti (in SCN_Gameplay HELM_BOOT_01, quella con la schermata di avvio). " +
                 "Fino ad allora sono spenti. Vuoto = accesi da subito.")]
        [SerializeField] private SubtitleLine powerUpLine;
        [Tooltip("Durata dell'accensione a cascata: CRT, bussola, gauge. Circa apertura del pannello comms + caricamento (0.45 + 3 s).")]
        [SerializeField, Min(0.5f)] private float powerUpSeconds = 3.5f;

        [Header("Luce rossa")]
        [Tooltip("Materiali delle fasce verde, arancione e rossa delle scale: con la torcia rossa diventano tre rossi " +
                 "di luminosità crescente. Cercati sui renderer figli, per indice di sottomesh.")]
        [SerializeField] private Material greenScale;
        [SerializeField] private Material orangeScale;
        [SerializeField] private Material redScale;

        [Header("Bussola")]
        [Tooltip("Lo schermo della bussola (MSH_CompassScreen in SM_Compass.fbx): un arco nel piano XY locale, centrato su compassCenter.")]
        [SerializeField] private Renderer compassStrip;
        [Tooltip("Dà la rotta. Default: la camera principale.")]
        [SerializeField] private Transform headingSource;
        [Tooltip("Centro dell'arco dello schermo, nello spazio locale della mesh (unità del FBX, 1/100 di metro). " +
                 "Z = metà larghezza dello schermo: le lettere sono centrate su quella Z, che si alza verso +Z.")]
        [SerializeField] private Vector3 compassCenter = new(0f, 0.001724f, 0.000018f);
        [Tooltip("Raggio a cui stanno le lettere: poco dentro la faccia dello schermo (r ≈ 0.00165).")]
        [SerializeField, Min(0.0001f)] private float compassRadius = 0.00164f;
        [Tooltip("Metà dell'arco visibile del nastro, in gradi.")]
        [SerializeField, Range(10f, 90f)] private float compassArcHalf = 32f;
        [Tooltip("Metà della finestra di rotta mostrata sul nastro, in gradi.")]
        [SerializeField, Range(10f, 180f)] private float compassWindowHalf = 75f;
        [Tooltip("Altezza delle lettere, in unità del mondo.")]
        [SerializeField, Min(0.001f)] private float compassLetterHeight = 0.009f;
        [Tooltip("Altezza delle intercardinali (NE, SE, SW, NW) rispetto alle cardinali.")]
        [SerializeField, Range(0.2f, 1f)] private float compassMinorScale = 0.65f;
        [Tooltip("Passo delle tacche fra le direzioni, in gradi (0 = niente tacche). Le tacche sulle direzioni sono saltate.")]
        [SerializeField, Range(0f, 45f)] private float compassTickStep = 15f;
        [Tooltip("Altezza delle tacche rispetto alle lettere cardinali.")]
        [SerializeField, Range(0.2f, 1f)] private float compassTickScale = 0.6f;

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
        private FlashlightMode? scaleMode;
        private float noticeUntil = -1f;
        private bool redWasUnlocked;
        private bool noticeRed;
        private readonly System.Collections.Generic.List<(Renderer renderer, int index, int band)> scaleSlots = new();
        // Accensione: -inf = acceso, +inf = spento in attesa di powerUpLine, altrimenti istante d'inizio.
        private float powerStart = float.NegativeInfinity;
        private bool powerSettled = true;
        // Inizio e durata di ogni gruppo, in frazioni di powerUpSeconds: 0 CRT, 1 bussola, 2 gauge.
        private static readonly Vector2[] PowerPhases = { new(0f, 0.45f), new(0.25f, 0.45f), new(0.45f, 0.55f) };
        // Materiali emissivi fissi (schermo CRT, fasce, lancette, bussola del modello), spenti per indice durante l'accensione.
        private readonly System.Collections.Generic.List<(Renderer renderer, int index, Color emission, int group)> powerSlots = new();
        // shortcut: lettere fisse in inglese, localizzare con la tabella quando serve
        private static readonly string[] CompassNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private readonly System.Collections.Generic.List<(TMP_Text text, float bearing)> compassMarks = new();

        internal const float CompassConeSlope = 0.14f;
        internal Renderer CompassStrip => compassStrip;
        internal Vector3 CompassCenter => compassCenter;
        internal float CompassRadius => compassRadius;
        internal float CompassArcHalf => compassArcHalf;
        internal float CompassWindowHalf => compassWindowHalf;
        internal TMP_Text ReferenceText => depthLabel != null ? depthLabel : depthValue;

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
            // In Awake, non in Start: un PowerOn da Start di altri script (CheatMenu con startOutside) deve vincere.
            if (powerUpLine != null)
            {
                powerStart = float.PositiveInfinity;
                powerSettled = false;
            }
        }

        private void Start()
        {
            startY = depthSource != null ? depthSource.position.y : 0f;
            // Partono dove sono i valori attuali, senza il giro iniziale.
            oxygenFraction = TargetOxygenFraction();
            pressureFraction = PressureFraction(CurrentDepth());
            redWasUnlocked = flashlight != null && flashlight.RedUnlocked;
            if (noticeRoot != null) noticeRoot.SetActive(false);
        }

        private void OnEnable() => HelmetComms.LineStarted += OnLineStarted;
        private void OnDisable() => HelmetComms.LineStarted -= OnLineStarted;

        private void OnLineStarted(SubtitleLine line)
        {
            if (line == null) return;
            if (line == powerUpLine) PowerOn(true);
            if (line == lightsNoticeLine) ShowLightsNotice();
        }

        /// <summary>Accende gli strumenti: animated = a cascata in powerUpSeconds (solo se spenti), altrimenti subito (cheat, warp).</summary>
        public void PowerOn(bool animated)
        {
            if (animated && powerStart != float.PositiveInfinity) return;
            powerStart = animated ? Time.unscaledTime : float.NegativeInfinity;
            powerSettled = false;
        }

        /// <summary>Mostra l'avviso della torcia (icona Full, «[F] CYCLE»).</summary>
        public void ShowLightsNotice() => ShowNotice(modeIcons.Length > 0 ? modeIcons[0] : null, lightsTitle, lightsKey, false, lightsNoticeSeconds);

        /// <summary>Per seconds (0 = noticeSeconds) il CRT mostra solo icona, titolo e tasto, poi torna ai testi normali.</summary>
        public void ShowNotice(Texture2D icon, string title, string key, bool red = false, float seconds = 0f)
        {
            noticeRed = red;
            if (noticeRoot == null) return;
            if (noticeIcon != null && icon != null)
            {
                noticeIcon.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, icon);
                noticeIcon.SetPropertyBlock(block);
            }
            if (noticeTitle != null) noticeTitle.SetText(title);
            if (noticeKey != null) noticeKey.SetText(key);
            noticeUntil = Time.unscaledTime + (seconds > 0f ? seconds : noticeSeconds);
            SetNotice(true);
        }

        private void SetNotice(bool on)
        {
            noticeRoot.SetActive(on);
            foreach (TMP_Text text in new[] { depthLabel, depthValue, lightLabel, lightValue })
                if (text != null) text.gameObject.SetActive(!on);
            if (lightBars != null)
                foreach (Renderer bar in lightBars)
                    if (bar != null) bar.gameObject.SetActive(!on);
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
            float elapsed = time - powerStart;
            float crtU = PowerPhase(0, elapsed), compassU = PowerPhase(1, elapsed), gaugeU = PowerPhase(2, elapsed);
            float crtGlow = Glow(crtU, time, 0f), compassGlow = Glow(compassU, time, 3.7f), gaugeGlow = Glow(gaugeU, time, 7.1f);

            if (flashlight != null && flashlight.RedUnlocked && !redWasUnlocked)
                ShowNotice(modeIcons.Length > 2 ? modeIcons[2] : null, redTitle, redKey, true);
            redWasUnlocked = flashlight != null && flashlight.RedUnlocked;
            if (noticeUntil >= 0f && time >= noticeUntil)
            {
                noticeUntil = -1f;
                SetNotice(false);
            }

            bool gaugeOn = gaugeU >= 1f;
            if (gaugeOn)
            {
                float blend = 1f - Mathf.Exp(-needleResponse * Time.deltaTime);
                oxygenFraction = Mathf.Lerp(oxygenFraction, TargetOxygenFraction(), blend);
                pressureFraction = Mathf.Lerp(pressureFraction, PressureFraction(depth), blend);
            }
            else
            {
                // Autotest all'accensione: fondo scala e ritorno al valore.
                oxygenFraction = SelfTest(gaugeU, TargetOxygenFraction());
                pressureFraction = SelfTest(gaugeU, PressureFraction(depth));
            }
            // Nelle fasce d'allarme la lancetta trema.
            RotateNeedle(oxygenNeedle, oxygenRest, oxygenFraction, gaugeOn && oxygenBand >= 2 ? Mathf.Sin(time * 30f) * 1.2f : 0f);
            RotateNeedle(pressureNeedle, pressureRest, pressureFraction, gaugeOn && pressureBand >= 2 ? Mathf.Sin(time * 25f) * 1.8f : 0f);

            if (gaugeOn)
            {
                SetLamp(oxygenLamp, oxygenBand, mode, time);
                SetLamp(pressureLamp, pressureBand, mode, time);
            }
            else
            {
                // Prova spie: accese per un tratto dell'autotest.
                float test = gaugeU > 0.1f && gaugeU < 0.5f ? lampIntensity : 0f;
                WriteEmission(oxygenLamp, LampYellow * test);
                WriteEmission(pressureLamp, LampYellow * test);
            }
            UpdateCrt(depth, pressureBand, mode, time, crtU, crtGlow);
            UpdateCompass(mode, compassU, compassGlow);
            UpdatePowerSlots(elapsed, crtGlow, compassGlow, gaugeGlow);
            if (powerSettled) UpdateScales(mode);
        }

        private float PowerPhase(int group, float elapsed) =>
            Mathf.Clamp01((elapsed - PowerPhases[group].x * powerUpSeconds) / (PowerPhases[group].y * powerUpSeconds));

        // Accensione da tubo: sale sfarfallando, poi resta fissa.
        private static float Glow(float u, float time, float seed) =>
            u <= 0f ? 0f : u >= 1f ? 1f : u * (Mathf.PerlinNoise(time * 30f, seed) > 0.35f ? 1f : 0.15f);

        private static float SelfTest(float u, float target) =>
            u < 0.5f ? Mathf.SmoothStep(0f, 1f, u * 2f) : Mathf.SmoothStep(1f, target, (u - 0.5f) * 2f);

        private void UpdatePowerSlots(float elapsed, float crtGlow, float compassGlow, float gaugeGlow)
        {
            if (powerSettled) return;
            if (elapsed >= powerUpSeconds)
            {
                // Blocchi per indice tolti: tornano il materiale e, con la torcia rossa, UpdateScales.
                foreach (var (r, index, _, _) in powerSlots) r.SetPropertyBlock(null, index);
                powerSettled = true;
                scaleMode = null;
                return;
            }
            foreach (var (r, index, emission, group) in powerSlots)
            {
                block.Clear();
                block.SetColor(EmissionColorId, emission * (group == 0 ? crtGlow : group == 1 ? compassGlow : gaugeGlow));
                r.SetPropertyBlock(block, index);
            }
        }

        private void CollectScaleSlots()
        {
            // Renderer già scritti per renderer dal codice: un blocco per indice coprirebbe il loro.
            var driven = new System.Collections.Generic.HashSet<Renderer> { compassStrip, oxygenLamp, pressureLamp, lightIcon, noticeIcon };
            if (lightBars != null) driven.UnionWith(lightBars);
            Material compassMaterial = compassStrip != null ? compassStrip.sharedMaterial : null;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                // Lo schermo comms ha la sua schermata di avvio.
                bool powered = !driven.Contains(r) && r.GetComponentInParent<HelmetComms>(true) == null;
                bool needle = (oxygenNeedle != null && r.transform.IsChildOf(oxygenNeedle)) ||
                              (pressureNeedle != null && r.transform.IsChildOf(pressureNeedle));
                for (int i = 0; i < mats.Length; i++)
                {
                    int band = mats[i] == null ? -1 : mats[i] == greenScale ? 0 : mats[i] == orangeScale ? 1 : mats[i] == redScale ? 2 : -1;
                    if (band >= 0) scaleSlots.Add((r, i, band));
                    if (!powered || mats[i] == null || !mats[i].IsKeywordEnabled("_EMISSION") || !mats[i].HasProperty(EmissionColorId)) continue;
                    Color emission = mats[i].GetColor(EmissionColorId);
                    if (emission.maxColorComponent < 0.001f) continue;
                    int group = band >= 0 || needle ? 2 : mats[i] == compassMaterial ? 1 : 0;
                    powerSlots.Add((r, i, emission, group));
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
            for (int i = 0; i < CompassNames.Length; i++)
                AddCompassMark(CompassNames[i], i * 45f, i % 2 == 0 ? 1f : compassMinorScale, FontStyles.Bold);
            if (compassTickStep <= 0f) return;
            for (float a = 0f; a < 360f - 0.01f; a += compassTickStep)
                if (Mathf.Abs(Mathf.Repeat(a + 22.5f, 45f) - 22.5f) > 0.01f) // salta le direzioni
                    AddCompassMark("|", a, compassTickScale, FontStyles.Normal);
        }

        private void AddCompassMark(string label, float bearing, float scale, FontStyles style)
        {
            var go = new GameObject("Compass_" + label + "_" + bearing);
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = depthValue.font;
            t.fontSharedMaterial = depthValue.fontSharedMaterial;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.rectTransform.sizeDelta = new Vector2(0.03f, 0.02f);
            t.text = label;
            t.fontSize = 1f;
            float lineHeight = t.GetPreferredValues("H").y; // altezza della riga a fontSize 1
            t.fontSize = compassLetterHeight * scale / Mathf.Max(lineHeight, 1e-6f);
            compassMarks.Add((t, bearing));
        }

        // Nastro: lettere e tacche scorrono sull'arco secondo lo scarto dalla rotta (destra = più a est),
        // e sfumano verso i bordi della finestra. Sulla mesh +X locale è la destra di chi guarda, +Z l'alto.
        private void UpdateCompass(FlashlightMode mode, float powerU, float power)
        {
            if (compassStrip == null) return;
            Color ink = mode == FlashlightMode.Red ? EmergencyRed : Amber;
            Color glow = ink * (0.12f * power);
            WriteEmission(compassStrip, glow);
            if (compassMarks.Count == 0) return;

            // All'accensione il nastro fa un giro di taratura e si ferma sulla rotta.
            float heading = (headingSource != null ? headingSource.eulerAngles.y : 0f) + (1f - Mathf.SmoothStep(0f, 1f, powerU)) * 360f;
            Transform strip = compassStrip.transform;
            foreach (var (mark, bearing) in compassMarks)
            {
                float off = Mathf.DeltaAngle(heading, bearing);
                float fade = 1f - Mathf.Abs(off) / compassWindowHalf;
                mark.enabled = fade > 0f && power > 0f;
                if (!mark.enabled) continue;

                float angle = off / compassWindowHalf * compassArcHalf * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Sin(angle), -Mathf.Cos(angle), 0f);
                // Lo schermo è conico (dr/dz ≈ -0.14): inclina le lettere sulla sua faccia così la cima non entra nella mesh.
                Vector3 worldOut = strip.TransformDirection(outward + Vector3.forward * CompassConeSlope);
                mark.transform.SetPositionAndRotation(
                    strip.TransformPoint(compassCenter + outward * compassRadius),
                    Quaternion.LookRotation(worldOut, strip.TransformDirection(Vector3.forward)));
                Color c = ink;
                c.a = fade * power;
                mark.color = c;
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

        private void UpdateCrt(float depth, int pressureBand, FlashlightMode mode, float time, float powerU, float power)
        {
            Color ink = mode == FlashlightMode.Red ? EmergencyRed : Amber;
            ink.a = power;
            Color dim = ink * 0.55f;
            dim.a = power;

            if (depthLabel != null) depthLabel.color = dim;
            if (lightLabel != null) lightLabel.color = dim;
            if (lightValue != null) lightValue.color = ink;
            if (noticeUntil >= 0f)
            {
                // L'avviso del modulo rosso è rosso anche con la torcia in Full.
                Color noticeInk = noticeRed ? EmergencyRed : ink;
                if (noticeTitle != null) noticeTitle.color = noticeInk;
                if (noticeKey != null) noticeKey.color = ink; // dim in modalità Red era illeggibile
                if (noticeIcon != null)
                {
                    noticeIcon.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, noticeInk);
                    noticeIcon.SetPropertyBlock(block);
                }
            }

            if (depthValue != null)
            {
                // All'accensione il numero conta da 0000 alla quota.
                int meters = Mathf.RoundToInt(depth * Mathf.SmoothStep(0f, 1f, powerU));
                if (meters != shownDepth)
                {
                    shownDepth = meters;
                    depthValue.SetText(meters.ToString("0000") + "<size=60%> M</size>");
                }
                // Oltre il limite dello scafo il numero lampeggia in rosso.
                Color depthColor = pressureBand == 3 ? LampRed * (Mathf.Sin(time * 6f) > 0f ? 1f : 0.4f) : ink;
                depthColor.a = power;
                depthValue.color = depthColor;
            }

            if (shownMode != mode)
            {
                shownMode = mode;
                if (lightValue != null)
                    lightValue.SetText(mode == FlashlightMode.Full ? "FULL" : mode == FlashlightMode.Low ? "LOW" : "RED");
            }

            if (lightIcon != null)
            {
                lightIcon.GetPropertyBlock(block);
                int index = (int)mode;
                if (index < modeIcons.Length && modeIcons[index] != null) block.SetTexture(BaseMapId, modeIcons[index]);
                block.SetColor(BaseColorId, ink);
                lightIcon.SetPropertyBlock(block);
            }

            if (lightBars == null) return;

            int lit = mode == FlashlightMode.Full ? 3 : 1;
            for (int i = 0; i < lightBars.Length; i++)
                WriteEmission(lightBars[i], ink * ((i < lit ? 1f : 0.12f) * barIntensity * power));
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

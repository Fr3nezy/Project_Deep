using UnityEngine;
using UnityEngine.UI;

namespace Deeploration.Player
{
    /// <summary>
    /// HUD diegetico del casco (layout del mockup 2026-10-12): CRT ambrato con profondità e torcia,
    /// gauge O₂ con quadrante della pressione, bussola. Costruito a runtime in un Canvas proprio, disegnato con una sola
    /// mesh UI: nessun asset da collegare. Legge OxygenSystem e DiverFlashlight, non scrive su nessun sistema.
    /// </summary>
    public sealed class HelmetHud : MonoBehaviour
    {
        [Header("Riferimenti (auto-trovati se vuoti)")]
        [SerializeField] private OxygenSystem oxygen;
        [SerializeField] private DiverFlashlight flashlight;
        [Tooltip("Dà la quota per la profondità. Default: il transform dell'OxygenSystem.")]
        [SerializeField] private Transform depthSource;
        [Tooltip("Dà la rotta per la bussola. Default: la camera principale.")]
        [SerializeField] private Transform headingSource;

        [Header("Profondità")]
        [Tooltip("Profondità in metri alla quota di partenza.")]
        [SerializeField] private float startDepth = 2040f;
        [Tooltip("Metri di profondità per ogni metro di scena in verticale: amplifica la discesa.")]
        [SerializeField, Min(0.01f)] private float metersPerUnit = 2f;
        [SerializeField] private float orangeDepth = 2200f;
        [SerializeField] private float redDepth = 2400f;
        [SerializeField] private float gaugeMinDepth = 1800f;
        [SerializeField] private float gaugeMaxDepth = 2600f;

        [Header("Canvas")]
        [Tooltip("Sotto zero: i fade a schermo coprono il casco.")]
        [SerializeField] private int sortingOrder = -5;

        private static readonly Color32 Amber = new(255, 176, 64, 255);
        private static readonly Color32 Green = new(120, 220, 140, 255);
        private static readonly Color32 Yellow = new(240, 210, 90, 255);
        private static readonly Color32 Orange = new(255, 140, 40, 255);
        private static readonly Color32 Red = new(255, 60, 50, 255);
        private static readonly Color32 EmergencyRed = new(255, 70, 50, 255);

        private static readonly (float a, float b, Color32 c)[] OxygenBands =
        {
            (0f, 0.15f, Red), (0.15f, 0.3f, Orange), (0.3f, 0.5f, Yellow), (0.5f, 1f, Green)
        };
        private static readonly (float a, float b, Color32 c)[] PressureBands =
        {
            (0f, 0.5f, Green), (0.5f, 0.75f, Orange), (0.75f, 1f, Red)
        };

        private sealed class HudLayer : MaskableGraphic
        {
            public System.Action<VertexHelper> Painter;

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Painter?.Invoke(vh);
            }
        }

        private HudLayer layer;
        private GameObject canvasObject;
        private Font font;
        private Text depthLabel, depthValue, depthUnit, lightLabel, lightValue, oxygenLabel, pressureLabel;
        private readonly Text[] compassLetters = new Text[4];
        private readonly (string text, Vector2 pos, float alpha)[] compassSlots = new (string, Vector2, float)[4];
        private int compassCount;

        private Rect rect;
        private float u;
        private float time;
        private float startY;
        private float depth;
        private float heading;
        private int oxygenBand;
        private int pressureBand;
        private FlashlightMode mode;
        private Camera cam;

        private void Awake()
        {
            if (oxygen == null) oxygen = FindFirstObjectByType<OxygenSystem>();
            if (flashlight == null) flashlight = FindFirstObjectByType<DiverFlashlight>();
            if (depthSource == null && oxygen != null) depthSource = oxygen.transform;
            BuildCanvas();
        }

        private void Start()
        {
            startY = depthSource != null ? depthSource.position.y : 0f;
        }

        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
        }

        private void BuildCanvas()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("HelmetHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var layerObject = new GameObject("Layer", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudLayer));
            layerObject.transform.SetParent(canvasObject.transform, false);
            var rt = (RectTransform)layerObject.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            layer = layerObject.GetComponent<HudLayer>();
            layer.raycastTarget = false;
            layer.Painter = Paint;

            depthLabel = MakeText("DepthLabel", 11, TextAnchor.MiddleLeft, FontStyle.Normal);
            depthValue = MakeText("DepthValue", 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            depthUnit = MakeText("DepthUnit", 11, TextAnchor.MiddleLeft, FontStyle.Normal);
            lightLabel = MakeText("LightLabel", 11, TextAnchor.MiddleLeft, FontStyle.Normal);
            lightValue = MakeText("LightValue", 11, TextAnchor.MiddleLeft, FontStyle.Normal);
            oxygenLabel = MakeText("OxygenLabel", 12, TextAnchor.MiddleCenter, FontStyle.Bold);
            pressureLabel = MakeText("PressureLabel", 9, TextAnchor.MiddleCenter, FontStyle.Normal);
            for (int i = 0; i < compassLetters.Length; i++)
            {
                compassLetters[i] = MakeText("Compass" + i, 12, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private Text MakeText(string objectName, int size, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(layer.transform, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(anchor == TextAnchor.MiddleLeft ? 0f : 0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240f, 40f);
            return text;
        }

        private void Update()
        {
            if (layer == null) return;

            time = Time.unscaledTime;
            rect = ((RectTransform)layer.transform).rect;
            u = rect.height / 270f;
            if (u <= 0f) return;

            depth = depthSource != null ? startDepth + (startY - depthSource.position.y) * metersPerUnit : startDepth;
            pressureBand = depth >= redDepth ? 3 : depth >= orangeDepth ? 2 : 0;
            oxygenBand = oxygen == null ? 0 : oxygen.Level switch
            {
                OxygenLevel.Safe => 0,
                OxygenLevel.Warning => 1,
                OxygenLevel.Critical => 2,
                _ => 3
            };
            mode = flashlight != null ? flashlight.CurrentMode : FlashlightMode.Full;

            if (headingSource == null)
            {
                if (cam == null) cam = Camera.main;
                headingSource = cam != null ? cam.transform : null;
            }
            heading = headingSource != null ? headingSource.eulerAngles.y : 0f;

            LayoutCompass();
            UpdateTexts();
            layer.SetVerticesDirty();
        }

        // ---- Coordinate del mockup (480 x 270, y verso il basso) -> coordinate locali del layer.
        private Vector2 FromLeft(float mx, float my) => new(rect.xMin + mx * u, rect.yMax - my * u);
        private Vector2 FromRight(float mx, float my) => new(rect.xMax - (480f - mx) * u, rect.yMax - my * u);
        private Vector2 FromCenter(float mx, float my) => new(rect.center.x + (mx - 240f) * u, rect.yMax - my * u);

        private Color Instrument(Color32 rgb, float k = 1f)
        {
            Color32 c = mode == FlashlightMode.Red ? EmergencyRed : rgb;
            return new Color(c.r / 255f, c.g / 255f, c.b / 255f, k);
        }

        private float PressureValue => Mathf.InverseLerp(gaugeMinDepth, gaugeMaxDepth, depth);

        private void LayoutCompass()
        {
            compassCount = 0;
            const float halfWindow = 75f;
            const float width = 150f;
            for (int d = -90; d <= 90; d += 15)
            {
                int deg = (((int)Mathf.Round((heading + d) / 15f) * 15) % 360 + 360) % 360;
                float off = ((deg - heading + 540f) % 360f) - 180f;
                if (Mathf.Abs(off) > halfWindow) continue;

                string letter = deg switch { 0 => "N", 90 => "E", 180 => "S", 270 => "O", _ => null };
                if (letter == null || compassCount >= compassSlots.Length) continue;

                float fade = 1f - Mathf.Abs(off) / halfWindow;
                compassSlots[compassCount++] = (letter, FromCenter(240f + off / halfWindow * (width / 2f), 14f), fade);
            }
        }

        private void UpdateTexts()
        {
            Color amber = Instrument(Amber);
            Color dim = Instrument(Amber, 0.55f);

            SetText(depthLabel, "PROFONDITÀ", FromLeft(21f, 205f), dim);
            Color depthColor = pressureBand == 3 ? Instrument(Red, Mathf.Sin(time * 6f) > 0f ? 1f : 0.4f) : amber;
            SetText(depthValue, Mathf.RoundToInt(depth).ToString("0000"), FromLeft(21f, 221f), depthColor);
            SetText(depthUnit, "M", FromLeft(53f, 224f), amber);
            SetText(lightLabel, "LUCE", FromLeft(21f, 240f), dim);
            SetText(lightValue, mode switch { FlashlightMode.Full => "PIENA", FlashlightMode.Low => "BASSA", _ => "ROSSA" },
                FromLeft(45f, 240f), amber);

            Vector2 gaugeCenter = FromRight(420f, 212f);
            float radius = 48f * u;
            SetText(oxygenLabel, "O2", gaugeCenter + new Vector2(0f, radius * 0.42f), Instrument(Amber, 0.85f));
            SetText(pressureLabel, "PRESS", gaugeCenter + new Vector2(0f, -radius * 0.4f - radius * 0.36f * 0.75f),
                Instrument(Amber, 0.7f));

            for (int i = 0; i < compassLetters.Length; i++)
            {
                bool used = i < compassCount;
                compassLetters[i].enabled = used;
                if (used)
                {
                    SetText(compassLetters[i], compassSlots[i].text, compassSlots[i].pos, Instrument(Amber, compassSlots[i].alpha));
                }
            }
        }

        private static void SetText(Text text, string value, Vector2 position, Color color)
        {
            if (text.text != value) text.text = value;
            text.color = color;
            text.rectTransform.anchoredPosition = position;
        }

        // ---- Disegno

        private void Paint(VertexHelper vh)
        {
            if (u <= 0f) return;
            PaintOxygenGauge(vh);
            PaintCrt(vh);
            PaintCompass(vh);
        }

        private void PaintOxygenGauge(VertexHelper vh)
        {
            Vector2 center = FromRight(420f, 212f);
            float radius = 48f * u;
            Dial(vh, center, radius, OxygenBands, oxygen != null ? oxygen.NormalizedOxygen : 1f,
                2f * u, oxygenBand >= 2 ? Mathf.Sin(time * 30f) * 0.02f : 0f, true);
            Lamp(vh, center + new Vector2(radius * 0.8f, radius * 0.8f), oxygenBand);

            Vector2 subCenter = center + new Vector2(0f, -radius * 0.4f);
            float subRadius = radius * 0.36f;
            Dial(vh, subCenter, subRadius, PressureBands, PressureValue, 1f * u,
                pressureBand >= 2 ? Mathf.Sin(time * 25f) * 0.03f : 0f, false);
            Lamp(vh, subCenter + new Vector2(subRadius * 0.95f, subRadius * 0.75f), pressureBand);
        }

        private void Dial(VertexHelper vh, Vector2 center, float radius, (float a, float b, Color32 c)[] bands,
            float value, float needleWidth, float jitter, bool ticks)
        {
            float bezel = ticks ? 4f * u : 2f * u;
            Disc(vh, center, radius + bezel, new Color32(22, 20, 18, 255));
            Disc(vh, center, radius, new Color32(8, 8, 8, 255));

            float start = Mathf.PI * 0.75f;
            float span = Mathf.PI * 1.5f;
            float bandWidth = (ticks ? 3f : 2f) * u;
            float bandRadius = radius - (ticks ? 4f : 2.5f) * u;
            foreach (var band in bands)
            {
                Arc(vh, center, bandRadius, start + span * band.a, start + span * band.b, bandWidth, Instrument(band.c, 0.55f));
            }

            if (ticks)
            {
                for (int i = 0; i <= 10; i++)
                {
                    float angle = start + span * i / 10f;
                    Line(vh, Polar(center, radius - 9f * u, angle), Polar(center, radius - 13f * u, angle), u, Instrument(Amber, 0.7f));
                }
            }

            float needleAngle = start + span * Mathf.Clamp01(value) + jitter;
            float needleLength = radius - (ticks ? 8f : 4f) * u;
            Line(vh, center, Polar(center, needleLength, needleAngle), needleWidth, Instrument(new Color32(255, 236, 210, 255)));
            Disc(vh, center, (ticks ? 3f : 1.6f) * u, Instrument(Amber));
        }

        private void Lamp(VertexHelper vh, Vector2 position, int band)
        {
            if (band == 0)
            {
                Disc(vh, position, 2.5f * u, new Color32(30, 26, 20, 255));
                return;
            }

            float on = band == 1 ? 1f : Mathf.Sin(time * (band == 3 ? 14f : 6f)) > 0f ? 1f : 0.15f;
            Color32 lamp = band == 1 ? Yellow : band == 2 ? Orange : Red;
            Disc(vh, position, 2.5f * u, Instrument(lamp, on));
        }

        private void PaintCrt(VertexHelper vh)
        {
            Vector2 topLeft = FromLeft(16f, 196f);
            Vector2 size = new(112f * u, 62f * u);
            float frame = 3f * u;
            Box(vh, topLeft.x - frame, topLeft.y - size.y - frame, topLeft.x + size.x + frame, topLeft.y + frame, new Color32(22, 20, 18, 255));
            Color32 screen = mode == FlashlightMode.Red ? new Color32(16, 3, 2, 255) : new Color32(14, 9, 3, 255);
            Box(vh, topLeft.x, topLeft.y - size.y, topLeft.x + size.x, topLeft.y, screen);

            int lit = mode == FlashlightMode.Full ? 3 : 1;
            for (int i = 0; i < 3; i++)
            {
                Vector2 bar = FromLeft(21f + i * 12f, 246f);
                Box(vh, bar.x, bar.y - 6f * u, bar.x + 9f * u, bar.y, Instrument(Amber, i < lit ? 1f : 0.15f));
            }

            for (float y = 0f; y < 62f; y += 2f)
            {
                Box(vh, topLeft.x, topLeft.y - (y + 1f) * u, topLeft.x + size.x, topLeft.y - y * u, new Color(0f, 0f, 0f, 0.25f));
            }
        }

        private void PaintCompass(VertexHelper vh)
        {
            const float halfWindow = 75f;
            const float width = 150f;
            Vector2 topCenter = FromCenter(240f, 2f);
            Box(vh, topCenter.x - (width / 2f + 4f) * u, topCenter.y - 18f * u, topCenter.x + (width / 2f + 4f) * u, topCenter.y,
                new Color32(14, 13, 12, 255));

            for (int d = -90; d <= 90; d += 15)
            {
                int deg = (((int)Mathf.Round((heading + d) / 15f) * 15) % 360 + 360) % 360;
                float off = ((deg - heading + 540f) % 360f) - 180f;
                if (Mathf.Abs(off) > halfWindow || deg % 90 == 0) continue;

                float fade = 1f - Mathf.Abs(off) / halfWindow;
                float x = FromCenter(240f + off / halfWindow * (width / 2f), 0f).x;
                float tick = deg % 45 == 0 ? 5f : 3f;
                Box(vh, x, topCenter.y - (7f + tick) * u, x + u, topCenter.y - 7f * u, Instrument(Amber, fade * 0.6f));
            }

            Vector2 tip = FromCenter(240f, 17f);
            Tri(vh, tip, tip + new Vector2(-3f * u, -4f * u), tip + new Vector2(3f * u, -4f * u), Instrument(Amber));
        }

        // ---- Primitive

        private static Vector2 Polar(Vector2 center, float radius, float angleYDown) =>
            center + new Vector2(Mathf.Cos(angleYDown), -Mathf.Sin(angleYDown)) * radius;

        private static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        private static void Box(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 color) =>
            Quad(vh, new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), color);

        private static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color32 color)
        {
            Vector2 dir = to - from;
            if (dir.sqrMagnitude < 1e-6f) return;
            Vector2 normal = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
            Quad(vh, from - normal, from + normal, to + normal, to - normal, color);
        }

        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color32 color)
        {
            const int segments = 28;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Tri(vh, center, center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                    center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, color);
            }
        }

        private static void Arc(VertexHelper vh, Vector2 center, float radius, float a0, float a1, float width, Color32 color)
        {
            int segments = Mathf.Max(2, Mathf.CeilToInt((a1 - a0) / (Mathf.PI * 2f) * 48f));
            float inner = radius - width * 0.5f;
            float outer = radius + width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float s0 = Mathf.Lerp(a0, a1, (float)i / segments);
                float s1 = Mathf.Lerp(a0, a1, (float)(i + 1) / segments);
                Quad(vh, Polar(center, inner, s0), Polar(center, outer, s0), Polar(center, outer, s1), Polar(center, inner, s1), color);
            }
        }
    }
}

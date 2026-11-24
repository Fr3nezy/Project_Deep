using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deeploration.Player
{
    /// <summary>
    /// Marker diegetici del casco per ogni <see cref="HelmetMarkerTarget"/> attivo.
    /// Supporta due modalità attivabili indipendentemente via toggle per confronto in Play:
    /// 1) visiera olografica 3D (a visorDistance dalla camera, come InteractionCrosshair), con icona e distanza in metri;
    /// 2) icona sull'arco della bussola (<see cref="HelmetInstruments"/>), che si ferma ai bordi quando il bersaglio è fuori campo.
    /// </summary>
    public sealed class HelmetMarkers : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly Color Amber = new(1f, 0.69f, 0.25f, 1f);
        private static readonly Color EmergencyRed = new(1f, 0.27f, 0.2f, 1f);

        [Header("Modalità (toggle di confronto)")]
        [Tooltip("Mostra i marker olografici 3D sulla visiera quando il bersaglio è nel campo visivo.")]
        [SerializeField] private bool showOnVisor = true;
        [Tooltip("Mostra l'icona del bersaglio sull'arco della bussola del casco.")]
        [SerializeField] private bool showOnCompass = true;

        [Header("Riferimenti (auto-trovati se vuoti)")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private HelmetInstruments instruments;
        [SerializeField] private DiverFlashlight flashlight;

        [Header("Aspetto")]
        [Tooltip("Materiale URP Unlit trasparente (es. M_HelmetMarker, coda 3500).")]
        [SerializeField] private Material markerMaterial;
        [Tooltip("Icona usata se HelmetMarkerTarget non ne ha una propria.")]
        [SerializeField] private Texture2D defaultIcon;
        [Tooltip("Font per la distanza sulla visiera. Vuoto = font di HelmetInstruments.")]
        [SerializeField] private TMP_FontAsset distanceFont;
        [Tooltip("Secondi di dissolvenza quando un marker compare o sparisce (tempo non scalato, come lo schermo comms).")]
        [SerializeField, Min(0.01f)] private float revealDuration = 0.8f;

        [Header("Visiera olografica")]
        [Tooltip("Distanza dall'occhio sul piano olografico (m), uguale a InteractionCrosshair.")]
        [SerializeField, Min(0.15f)] private float visorDistance = 0.30f;
        [Tooltip("Lato del quad icona sulla visiera (m a visorDistance; 0.0075 ≈ 45 px a 1080p).")]
        [SerializeField, Min(0.001f)] private float visorIconSize = 0.0075f;
        [Tooltip("Altezza del testo distanza sulla visiera (m).")]
        [SerializeField, Min(0.0005f)] private float visorTextHeight = 0.0046f;
        [Tooltip("Distanza sotto cui il marker sulla visiera inizia a sfumare (m).")]
        [SerializeField, Min(0.5f)] private float fadeStartDistance = 6f;
        [Tooltip("Distanza sotto cui il marker sulla visiera sparisce del tutto (m).")]
        [SerializeField, Min(0.1f)] private float hideDistance = 2.5f;

        [Header("Bussola")]
        [Tooltip("Offset lungo +Z locale di MSH_CompassScreen rispetto alla fila delle lettere (unità del FBX). " +
                 "0 = nella fila: dello schermo si vede solo la fascia delle lettere, sopra è fuori campo e sotto c'è il telaio.")]
        [SerializeField] private float compassZOffset;
        [Tooltip("Lato del quad icona sulla bussola (m), di poco sotto l'altezza delle lettere cardinali.")]
        [SerializeField, Min(0.001f)] private float compassIconSize = 0.007f;
        [Tooltip("Luminosità dell'icona rispetto alle lettere: stesso ambra (CRT mai di altro colore), più spento per distinguerla.")]
        [SerializeField, Range(0.1f, 1f)] private float compassDim = 0.55f;
        [Tooltip("Scarto minimo di rotta fra icona e lettera (gradi di rotta): vicino a una lettera l'icona si sposta di lato, mai sopra.")]
        [SerializeField, Range(0f, 20f)] private float compassLetterClearance = 8f;
        [Tooltip("Scarto minimo di rotta fra due icone sulla bussola (gradi di rotta): POI vicini si mettono in fila, non uno sopra l'altro.")]
        [SerializeField, Range(0f, 20f)] private float compassIconSpacing = 8.5f;

        private sealed class MarkerVisual
        {
            public GameObject visorRoot;
            public Renderer visorIcon;
            public TMP_Text visorDistanceText;
            public Renderer compassIcon;
            public int shownMeters = int.MinValue;
            public float reveal; // 0 nascosto, 1 pieno: dissolvenza di comparsa
        }

        private readonly Dictionary<HelmetMarkerTarget, MarkerVisual> visuals = new();
        private readonly List<HelmetMarkerTarget> stale = new();
        private readonly List<float> placedBearings = new();
        private MaterialPropertyBlock block;
        private TMP_FontAsset resolvedFont;
        private Material resolvedFontMat;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInParent<Camera>();
            if (viewCamera == null) viewCamera = Camera.main;
            if (instruments == null) instruments = FindFirstObjectByType<HelmetInstruments>();
            if (flashlight == null) flashlight = FindFirstObjectByType<DiverFlashlight>();
            block = new MaterialPropertyBlock();

            if (distanceFont != null)
            {
                resolvedFont = distanceFont;
                resolvedFontMat = distanceFont.material;
            }
            else if (instruments != null && instruments.ReferenceText != null)
            {
                resolvedFont = instruments.ReferenceText.font;
                resolvedFontMat = instruments.ReferenceText.fontSharedMaterial;
            }
        }

        private void OnDestroy()
        {
            foreach (var v in visuals.Values) DestroyVisual(v);
            visuals.Clear();
        }

        private void LateUpdate()
        {
            if (viewCamera == null || markerMaterial == null) return;

            var active = HelmetMarkerTarget.Active;
            CleanupStale(active);

            Color ink = flashlight != null && flashlight.CurrentMode == FlashlightMode.Red ? EmergencyRed : Amber;
            Vector3 camPos = viewCamera.transform.position;
            Quaternion camRot = viewCamera.transform.rotation;
            float heading = viewCamera.transform.eulerAngles.y;
            placedBearings.Clear();

            for (int i = 0; i < active.Count; i++)
            {
                HelmetMarkerTarget target = active[i];
                if (target == null) continue;
                if (!visuals.TryGetValue(target, out MarkerVisual v))
                {
                    v = CreateVisual(target);
                    visuals[target] = v;
                }

                v.reveal = Mathf.MoveTowards(v.reveal, target.IsVisible ? 1f : 0f, Time.unscaledDeltaTime / revealDuration);
                bool visible = v.reveal > 0f;
                Texture2D tex = target.Icon != null ? target.Icon : defaultIcon;
                Vector3 worldPos = target.WorldPosition;
                Vector3 delta = worldPos - camPos;
                float dist = delta.magnitude;

                UpdateVisor(v, visible && showOnVisor && dist > 0.01f, worldPos, delta, dist, camPos, camRot, tex, ink);
                UpdateCompass(v, visible && showOnCompass, delta, heading, tex, ink);
            }
        }

        private void UpdateVisor(MarkerVisual v, bool on, Vector3 worldPos, Vector3 delta, float dist,
            Vector3 camPos, Quaternion camRot, Texture2D tex, Color ink)
        {
            if (!on)
            {
                if (v.visorRoot.activeSelf) v.visorRoot.SetActive(false);
                return;
            }

            Vector3 vp = viewCamera.WorldToViewportPoint(worldPos);
            // Resta dentro la zona libera della visiera (lontano da bussola in alto e CRT in basso).
            bool inView = vp.z > 0f && vp.x >= 0.06f && vp.x <= 0.94f && vp.y >= 0.10f && vp.y <= 0.87f;
            float nearFade = Mathf.InverseLerp(hideDistance, fadeStartDistance, dist);
            if (!inView || nearFade <= 0.001f)
            {
                if (v.visorRoot.activeSelf) v.visorRoot.SetActive(false);
                return;
            }

            if (!v.visorRoot.activeSelf) v.visorRoot.SetActive(true);
            v.visorRoot.transform.SetPositionAndRotation(camPos + (delta / dist) * visorDistance, camRot);
            v.visorIcon.transform.localScale = new Vector3(visorIconSize, visorIconSize, 1f);

            Color c = new(ink.r, ink.g, ink.b, 0.9f * nearFade * v.reveal);
            ApplyQuad(v.visorIcon, tex, c);

            if (v.visorDistanceText != null)
            {
                int meters = Mathf.RoundToInt(dist);
                if (meters != v.shownMeters)
                {
                    v.shownMeters = meters;
                    v.visorDistanceText.SetText("{0}M", meters);
                }
                v.visorDistanceText.color = c;
            }
        }

        private void UpdateCompass(MarkerVisual v, bool on, Vector3 delta, float heading, Texture2D tex, Color ink)
        {
            if (!on || instruments == null || instruments.CompassStrip == null)
            {
                if (v.compassIcon.enabled) v.compassIcon.enabled = false;
                return;
            }

            float windowHalf = instruments.CompassWindowHalf;
            float arcHalf = instruments.CompassArcHalf;
            float bearing = CompassSlot(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg);
            float off = Mathf.DeltaAngle(heading, bearing);
            bool clamped = Mathf.Abs(off) > windowHalf;
            float angle = Mathf.Clamp(off, -windowHalf, windowHalf) / windowHalf * arcHalf * Mathf.Deg2Rad;

            Transform strip = instruments.CompassStrip.transform;
            var outward = new Vector3(Mathf.Sin(angle), -Mathf.Cos(angle), 0f);
            Vector3 center = instruments.CompassCenter + Vector3.forward * compassZOffset;
            float radius = instruments.CompassRadius - HelmetInstruments.CompassConeSlope * compassZOffset;

            v.compassIcon.transform.SetPositionAndRotation(
                strip.TransformPoint(center + outward * radius),
                Quaternion.LookRotation(
                    strip.TransformDirection(outward + Vector3.forward * HelmetInstruments.CompassConeSlope),
                    strip.TransformDirection(Vector3.forward)));
            v.compassIcon.transform.localScale = new Vector3(compassIconSize, compassIconSize, 1f);

            float alpha = (clamped ? 0.55f : 0.95f) * v.reveal;
            ApplyQuad(v.compassIcon, tex, new Color(ink.r * compassDim, ink.g * compassDim, ink.b * compassDim, alpha));
            if (!v.compassIcon.enabled) v.compassIcon.enabled = true;
        }

        // Rotta a cui disegnare l'icona: mai sopra una lettera (ogni 45°) né sopra un'altra icona già piazzata in questo frame.
        // Si sposta di lato, dalla parte in cui già pende, finché trova posto.
        private float CompassSlot(float bearing)
        {
            float dir = 0f;
            for (int pass = 0; pass < 8; pass++)
            {
                float letter = Mathf.Round(bearing / 45f) * 45f;
                float fromLetter = Mathf.DeltaAngle(letter, bearing);
                if (Mathf.Abs(fromLetter) < compassLetterClearance)
                {
                    if (dir == 0f) dir = fromLetter < 0f ? -1f : 1f;
                    bearing = letter + dir * compassLetterClearance;
                    continue;
                }
                bool moved = false;
                for (int i = 0; i < placedBearings.Count; i++)
                {
                    float fromIcon = Mathf.DeltaAngle(placedBearings[i], bearing);
                    if (Mathf.Abs(fromIcon) >= compassIconSpacing) continue;
                    if (dir == 0f) dir = fromIcon < 0f ? -1f : 1f;
                    bearing = placedBearings[i] + dir * compassIconSpacing;
                    moved = true;
                    break;
                }
                if (!moved) break;
            }
            placedBearings.Add(bearing);
            return bearing;
        }

        private MarkerVisual CreateVisual(HelmetMarkerTarget target)
        {
            var v = new MarkerVisual();

            v.visorRoot = new GameObject("VisorMarker_" + target.name);
            v.visorRoot.transform.SetParent(transform, false);
            v.visorIcon = CreateQuad("Icon", v.visorRoot.transform);

            if (resolvedFont != null)
            {
                var textGo = new GameObject("Distance");
                textGo.transform.SetParent(v.visorRoot.transform, false);
                textGo.transform.localPosition = new Vector3(0f, -visorIconSize * 0.95f, 0f);
                var tmp = textGo.AddComponent<TextMeshPro>();
                tmp.font = resolvedFont;
                tmp.fontSharedMaterial = resolvedFontMat;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.rectTransform.sizeDelta = new Vector2(0.03f, 0.01f);
                tmp.text = "0M";
                tmp.fontSize = 1f;
                float lineHeight = tmp.GetPreferredValues("0").y;
                tmp.fontSize = visorTextHeight / Mathf.Max(lineHeight, 1e-6f);
                v.visorDistanceText = tmp;
            }

            Transform compassParent = instruments != null ? instruments.transform : transform;
            v.compassIcon = CreateQuad("CompassMarker_" + target.name, compassParent);
            return v;
        }

        private Renderer CreateQuad(string quadName, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = quadName;
            go.transform.SetParent(parent, false);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = markerMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private void ApplyQuad(Renderer target, Texture2D tex, Color color)
        {
            target.GetPropertyBlock(block);
            if (tex != null) block.SetTexture(BaseMapId, tex);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }

        private void CleanupStale(IReadOnlyList<HelmetMarkerTarget> active)
        {
            stale.Clear();
            foreach (var kv in visuals)
                if (kv.Key == null || !Contains(active, kv.Key)) stale.Add(kv.Key);
            for (int i = 0; i < stale.Count; i++)
            {
                DestroyVisual(visuals[stale[i]]);
                visuals.Remove(stale[i]);
            }
        }

        private static bool Contains(IReadOnlyList<HelmetMarkerTarget> list, HelmetMarkerTarget item)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return true;
            return false;
        }

        private static void DestroyVisual(MarkerVisual v)
        {
            if (v.visorRoot != null) Destroy(v.visorRoot);
            if (v.compassIcon != null) Destroy(v.compassIcon.gameObject);
        }
    }
}

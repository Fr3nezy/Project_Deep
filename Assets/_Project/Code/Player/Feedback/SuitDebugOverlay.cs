using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Deeploration.Player
{
    /// <summary>
    /// Vignetta URP che segue lo stress e, solo per il tuning, un testo di debug con O₂ e stress acceso con toggleKey.
    /// Dal 2026-11-25 non mostra più obiettivi, mani né prompt [E]: il sistema missioni è deprecato e in build il giocatore vede solo il casco.
    /// </summary>
    public sealed class SuitDebugOverlay : MonoBehaviour
    {
        [Header("Sistemi Tuta")]
        [SerializeField] private OxygenSystem oxygen;
        [SerializeField] private StressSystem stress;

        [Header("UI")]
        [SerializeField] private Text counter;
        [Tooltip("Testo di debug con O₂ e stress in percentuale. Spento di default, anche in build.")]
        [SerializeField] private bool showVitals;
        [SerializeField] private Key toggleKey = Key.F3;

        [Header("Feedback Vignetta URP")]
        [SerializeField] private Volume volume;
        [SerializeField, Range(0f, 1f)] private float minimumVignetteIntensity = 0.2f;
        [SerializeField, Range(0f, 1f)] private float maximumVignetteIntensity = 0.4f;
        [SerializeField, Range(1f, 20f)] private float vignetteResponse = 7f;

        private float displayedStress;
        private Vignette vignette;

        private void Awake()
        {
            if (oxygen == null) oxygen = FindFirstObjectByType<OxygenSystem>();
            if (stress == null) stress = FindFirstObjectByType<StressSystem>();

            if (counter != null)
            {
                counter.verticalOverflow = VerticalWrapMode.Overflow;
                counter.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            VolumeProfile profile = volume != null ? volume.profile : null;
            if (profile != null && profile.TryGet(out vignette))
                vignette.intensity.overrideState = true;
        }

        private void Update()
        {
            float stressNormalized = stress != null ? stress.NormalizedStress : 0f;
            float blend = 1f - Mathf.Exp(-vignetteResponse * Time.deltaTime);
            displayedStress = Mathf.Lerp(displayedStress, stressNormalized, blend);

            if (vignette != null)
            {
                float maximum = Mathf.Max(minimumVignetteIntensity, maximumVignetteIntensity);
                vignette.intensity.value = Mathf.Lerp(minimumVignetteIntensity, maximum, displayedStress);
            }

            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) showVitals = !showVitals;
            if (counter == null) return;
            counter.text = showVitals
                ? $"O₂  {(oxygen != null ? oxygen.NormalizedOxygen : 0f) * 100f:0.0}%\nSTRESS  {stressNormalized * 100f:0}%"
                : string.Empty;
        }
    }
}

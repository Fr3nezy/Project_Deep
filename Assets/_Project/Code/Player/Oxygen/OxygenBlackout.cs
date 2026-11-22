using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Deeploration.Player
{
    /// <summary>
    /// Sul PlayerCapsule, accanto a <see cref="OxygenSystem"/>. Con l'ossigeno sotto <c>startNormalized</c> una vignetta
    /// nera si stringe e lo schermo si scurisce; a ossigeno finito va al nero pieno (in tempo per il respawn di
    /// <see cref="DiverRespawn"/>) e torna visibile quando il serbatoio si ricarica. Volume globale creato a runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OxygenSystem))]
    public sealed class OxygenBlackout : MonoBehaviour
    {
        [Tooltip("Frazione di ossigeno sotto cui inizia a scurirsi (0.25 = soglia Critical del profilo di default)")]
        [SerializeField, Range(0.01f, 1f)] private float startNormalized = 0.25f;

        [Tooltip("Buio raggiunto a ossigeno quasi zero, prima della morte (0-1; 1 = nero pieno)")]
        [SerializeField, Range(0f, 1f)] private float lowOxygenDarkness = 0.8f;

        [Tooltip("Secondi per andare da schermo pulito a nero pieno; più corto di DiverRespawn.respawnDelay (2 s)")]
        [SerializeField, Min(0.05f)] private float blackoutSeconds = 1.5f;

        [Tooltip("Secondi per tornare da nero pieno a schermo pulito dopo il respawn")]
        [SerializeField, Min(0.05f)] private float recoverSeconds = 1.5f;

        [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.6f;

        [Tooltip("Priorità del Volume: sopra la vignetta del morso (100)")]
        [SerializeField] private float volumePriority = 110f;

        [Tooltip("Layer del Volume: deve essere nella Volume Mask della camera")]
        [SerializeField, Range(0, 31)] private int volumeLayer = 0;

        private OxygenSystem oxygen;
        private Volume volume;
        private VolumeProfile runtimeProfile;
        private ColorAdjustments colorAdjustments;

        /// <summary>Buio mostrato ora (0 = niente, 1 = nero pieno).</summary>
        public float Darkness { get; private set; }

        private void Awake()
        {
            oxygen = GetComponent<OxygenSystem>();

            var volumeObject = new GameObject("OxygenBlackoutVolume") { layer = volumeLayer };
            volumeObject.transform.SetParent(transform, false);
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = volumePriority;
            volume.weight = 0f;
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            Vignette vignette = runtimeProfile.Add<Vignette>(true);
            vignette.intensity.Override(1f);
            vignette.color.Override(Color.black);
            vignette.smoothness.Override(vignetteSmoothness);
            colorAdjustments = runtimeProfile.Add<ColorAdjustments>(false);
            colorAdjustments.colorFilter.Override(Color.white);
            volume.sharedProfile = runtimeProfile;
        }

        private void OnDestroy()
        {
            if (runtimeProfile != null) Destroy(runtimeProfile);
        }

        private void Update()
        {
            float target = oxygen.Level == OxygenLevel.Depleted
                ? 1f
                : lowOxygenDarkness * Mathf.InverseLerp(startNormalized, 0f, oxygen.NormalizedOxygen);
            float seconds = target > Darkness ? blackoutSeconds : recoverSeconds;
            Darkness = Mathf.MoveTowards(Darkness, target, Time.deltaTime / seconds);

            // Il peso sfuma vignetta e filtro insieme: filtro nero al quadrato, così il buio totale cresce come d³
            // e arriva al nero pieno solo a d = 1.
            volume.weight = Darkness;
            colorAdjustments.colorFilter.value = Color.Lerp(Color.white, Color.black, Darkness * Darkness);
        }
    }
}

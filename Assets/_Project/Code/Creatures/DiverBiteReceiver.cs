using System;
using Deeploration.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Sul PlayerCapsule, accanto a <see cref="DiverStimulusSource"/>. Riceve i morsi dal <see cref="CreatureManager"/>
    /// e li trasforma in effetti sul sub: picco di stress, perdita di ossigeno, vignetta che si stringe e battito
    /// cardiaco. Niente combattimento: il morso fa paura e costa ossigeno. Il battito è un audio provvisorio generato
    /// in codice finché non c'è quello definitivo (si sostituisce assegnando <c>heartbeatClip</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiverBiteReceiver : MonoBehaviour
    {
        [Header("Sub")]
        [Tooltip("Vuoto = GetComponent sullo stesso GameObject")]
        [SerializeField] private StressSystem stress;

        [Tooltip("Vuoto = GetComponent sullo stesso GameObject")]
        [SerializeField] private OxygenSystem oxygen;

        [Tooltip("Stress aggiunto di colpo a ogni morso")]
        [SerializeField, Min(0f)] private float stressSpike = 45f;

        [Tooltip("Ossigeno perso di colpo a ogni morso (stesse unità di OxygenProfile.maxOxygen)")]
        [SerializeField, Min(0f)] private float oxygenLoss = 30f;

        [Header("Vignetta")]
        [Tooltip("Intensità della vignetta al culmine del morso")]
        [SerializeField, Range(0f, 1f)] private float vignettePeak = 0.65f;

        [SerializeField] private Color vignetteColor = new Color(0.22f, 0f, 0f);

        [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.45f;

        [Tooltip("Secondi in cui la vignetta si chiude")]
        [SerializeField, Min(0.01f)] private float vignetteAttack = 0.08f;

        [Tooltip("Secondi in cui resta chiusa")]
        [SerializeField, Min(0f)] private float vignetteHold = 0.4f;

        [Tooltip("Secondi in cui si riapre")]
        [SerializeField, Min(0.01f)] private float vignetteRelease = 2.5f;

        [Tooltip("Priorità del Volume del morso: più alta di quelle della scena, così la sua vignetta vince")]
        [SerializeField] private float volumePriority = 100f;

        [Tooltip("Layer del Volume del morso: deve essere nella Volume Mask della camera")]
        [SerializeField, Range(0, 31)] private int volumeLayer = 0;

        [Header("Battito (provvisorio)")]
        [Tooltip("Clip di un battito. Vuoto = battito sintetico generato all'avvio")]
        [SerializeField] private AudioClip heartbeatClip;

        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.9f;

        [Tooltip("Battiti dopo un morso")]
        [SerializeField, Range(1, 20)] private int heartbeats = 7;

        [Tooltip("Secondi fra i primi battiti (cuore in gola)")]
        [SerializeField, Min(0.2f)] private float firstBeatInterval = 0.42f;

        [Tooltip("Secondi fra gli ultimi battiti (si calma)")]
        [SerializeField, Min(0.2f)] private float lastBeatInterval = 0.8f;

        private Volume volume;
        private VolumeProfile runtimeProfile;
        private AudioSource audioSource;
        private AudioClip generatedClip;
        private int beatIndex;
        private int beatsLeft;
        private float nextBeatTime;

        /// <summary>Lanciato a ogni morso, con il predatore che ha morso.</summary>
        public event Action<Creature> OnBitten;

        /// <summary>Istante (Time.time) dell'ultimo morso; -infinito se il sub non è mai stato morso.</summary>
        public float LastBiteTime { get; private set; } = float.NegativeInfinity;

        /// <summary>Morsi ricevuti da quando il componente è attivo.</summary>
        public int BiteCount { get; private set; }

        /// <summary>Peso attuale del Volume della vignetta del morso (0 = spento, 1 = al culmine).</summary>
        public float VignetteWeight => volume != null ? volume.weight : 0f;

        private void Awake()
        {
            if (stress == null) stress = GetComponent<StressSystem>();
            if (oxygen == null) oxygen = GetComponent<OxygenSystem>();

            var volumeObject = new GameObject("BiteVignetteVolume") { layer = volumeLayer };
            volumeObject.transform.SetParent(transform, false);
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = volumePriority;
            volume.weight = 0f;
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            Vignette vignette = runtimeProfile.Add<Vignette>(true);
            vignette.intensity.Override(vignettePeak);
            vignette.color.Override(vignetteColor);
            vignette.smoothness.Override(vignetteSmoothness);
            volume.sharedProfile = runtimeProfile;

            var audioObject = new GameObject("BiteHeartbeat");
            audioObject.transform.SetParent(transform, false);
            audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (heartbeatClip == null)
            {
                generatedClip = CreateHeartbeatClip();
                heartbeatClip = generatedClip;
            }
        }

        private void OnDestroy()
        {
            if (runtimeProfile != null) Destroy(runtimeProfile);
            if (generatedClip != null) Destroy(generatedClip);
        }

        /// <summary>Applica un morso al sub: stress, ossigeno, vignetta e battito. Chiamato dal CreatureManager.</summary>
        public void ReceiveBite(Creature biter)
        {
            if (stress != null) stress.AddStress(stressSpike);
            if (oxygen != null) oxygen.Consume(oxygenLoss);
            LastBiteTime = Time.time;
            BiteCount++;
            beatIndex = 0;
            beatsLeft = heartbeats;
            nextBeatTime = Time.time;
            OnBitten?.Invoke(biter);
        }

        private void Update()
        {
            float t = Time.time - LastBiteTime;
            float weight;
            if (t < vignetteAttack) weight = t / vignetteAttack;
            else if (t < vignetteAttack + vignetteHold) weight = 1f;
            else weight = 1f - Mathf.SmoothStep(0f, 1f, (t - vignetteAttack - vignetteHold) / vignetteRelease);
            volume.weight = Mathf.Clamp01(weight);

            if (beatsLeft > 0 && Time.time >= nextBeatTime && heartbeatClip != null)
            {
                float k = heartbeats > 1 ? beatIndex / (float)(heartbeats - 1) : 1f;
                audioSource.PlayOneShot(heartbeatClip, heartbeatVolume * Mathf.Lerp(1f, 0.45f, k));
                nextBeatTime += Mathf.Lerp(firstBeatInterval, lastBeatInterval, k);
                beatIndex++;
                beatsLeft--;
            }
        }

        /// <summary>
        /// Battito sintetico "lub-dub": due colpi bassi (55 Hz e 72 Hz) con attacco rapido e decadimento
        /// esponenziale, a 0.16 s l'uno dall'altro. Provvisorio.
        /// </summary>
        private static AudioClip CreateHeartbeatClip()
        {
            const int rate = 44100;
            const float length = 0.5f;
            int samples = Mathf.CeilToInt(rate * length);
            var data = new float[samples];
            float peak = 0f;
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)rate;
                float value = Thump(time, 0f, 55f, 0.06f, 1f) + Thump(time, 0.16f, 72f, 0.05f, 0.7f);
                data[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            if (peak > 0f)
                for (int i = 0; i < samples; i++) data[i] *= 0.9f / peak;
            AudioClip clip = AudioClip.Create("Heartbeat_Generated", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Thump(float time, float start, float frequency, float decay, float amplitude)
        {
            float t = time - start;
            if (t < 0f) return 0f;
            float attack = Mathf.Clamp01(t / 0.006f);
            return amplitude * attack * Mathf.Exp(-t / decay) * Mathf.Sin(2f * Mathf.PI * frequency * t);
        }
    }
}

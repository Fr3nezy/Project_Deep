using System.Collections.Generic;
using Deeploration.Creatures;
using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Luce ferma che attira le creature attratte dalla luce (lanternfish): emette uno stimolo WeakLight puntiforme
    /// finché è accesa. Con fromPlayer acceso conta come luce accesa dal sub, che le creature preferiscono a ogni
    /// altra luce: così batte anche una cella a terra più vicina allo sciame. Con onDuration > 0 si accende e si
    /// spegne a ciclo (lampada da cantiere guasta), con uno sfarfallio a ogni cambio; con 0 resta accesa.
    /// </summary>
    public sealed class LightLure : MonoBehaviour, IStimulusSource
    {
        [SerializeField, Min(0f)] private float reach = 10f;
        [Tooltip("Acceso: lo stimolo conta come del giocatore (le creature lo preferiscono alle altre luci).")]
        [SerializeField] private bool fromPlayer = true;

        [Header("Ciclo")]
        [Tooltip("Luce visibile da accendere e spegnere col ciclo. Vuoto = la Light su questo oggetto, se c'è.")]
        [SerializeField] private Light lamp;
        [Tooltip("Secondi accesa per ciclo. 0 = sempre accesa.")]
        [SerializeField, Min(0f)] private float onDuration;
        [Tooltip("Secondi spenta per ciclo.")]
        [SerializeField, Min(0f)] private float offDuration = 8f;
        [Tooltip("Secondi di sfarfallio all'inizio e alla fine di ogni accensione.")]
        [SerializeField, Min(0f)] private float flickerDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float flickerRate = 12f;

        private CreatureManager manager;
        private float baseIntensity;
        private float cycleStart;

        /// <summary>Vero quando la luce è accesa (anche mentre sfarfalla) ed emette lo stimolo.</summary>
        public bool IsLit { get; private set; } = true;

        private void Awake()
        {
            if (lamp == null) lamp = GetComponent<Light>();
            if (lamp != null) baseIntensity = lamp.intensity;
        }

        private void OnEnable()
        {
            cycleStart = Time.time;
            manager = FindAnyObjectByType<CreatureManager>();
            if (manager != null) manager.AddSource(this);
        }

        private void OnDisable()
        {
            if (manager != null) manager.RemoveSource(this);
        }

        private void Update()
        {
            if (onDuration <= 0f) { IsLit = true; SetLevel(1f); return; }

            float t = (Time.time - cycleStart) % (onDuration + offDuration);
            IsLit = t < onDuration;
            bool flickering = IsLit && (t < flickerDuration || t > onDuration - flickerDuration);
            if (!IsLit) SetLevel(0f);
            else if (flickering) SetLevel(Mathf.PerlinNoise(Time.time * flickerRate, 0f) > 0.45f ? 1f : 0.1f);
            else SetLevel(1f);
        }

        private void SetLevel(float level)
        {
            if (lamp == null) return;
            lamp.enabled = level > 0f;
            lamp.intensity = baseIntensity * level;
        }

        public void EmitStimuli(List<CreatureStimulus> output)
        {
            if (IsLit) output.Add(CreatureStimulus.PointLight(transform.position, reach, StimulusCategory.WeakLight, fromPlayer));
        }
    }
}

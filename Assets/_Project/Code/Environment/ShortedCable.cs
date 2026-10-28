using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Deeploration.Creatures;
using Deeploration.Interaction;

namespace Deeploration.Environment
{
    /// <summary>
    /// Cavo elettrico scoperto della chiusa (puzzle Z2). Emette luce debole che attira le creature della specie
    /// indicata; finché almeno una sta entro <see cref="shortRadius"/>, il circuito è in corto e
    /// <see cref="TryRestorePower"/> non fa niente. A circuito libero ridà corrente una volta sola e lancia
    /// <see cref="onPowerRestored"/> (gate, stazione O₂).
    /// </summary>
    public sealed class ShortedCable : MonoBehaviour, IStimulusSource
    {
        [Header("Riferimenti")]
        [Tooltip("CreatureManager della scena: la luce del cavo entra nei suoi stimoli. Senza, il cavo non attira nessuno.")]
        [SerializeField] private CreatureManager manager;

        [Tooltip("Specie che manda il cavo in corto (Isopod).")]
        [SerializeField] private CreatureProfile shortingSpecies;

        [Tooltip("Leva della chiusa (opzionale): il suo prompt mostra lo stato del circuito.")]
        [SerializeField] private SimpleInteractable lever;

        [Tooltip("Luce del cavo (opzionale): sfarfalla quando il circuito è in corto.")]
        [SerializeField] private Light cableLight;

        [Header("Stimolo e corto")]
        [Tooltip("Portata della luce debole del cavo per una creatura con sensibilità 1 (m)")]
        [SerializeField, Min(0f)] private float lightRadius = 10f;

        [Tooltip("Una creatura della specie entro questa distanza dal cavo lo manda in corto (m). Tenerla sopra " +
                 "l'inspectDistance della specie, che è dove si fermano a guardare la luce")]
        [SerializeField, Min(0.1f)] private float shortRadius = 3.5f;

        [Header("Prompt della leva")]
        [SerializeField] private string shortedPrompt = "Leva: circuito in corto";
        [SerializeField] private string readyPrompt = "Riattiva la chiusa";

        [Header("Eventi")]
        public UnityEvent onPowerRestored = new UnityEvent();
        public UnityEvent onShortedAttempt = new UnityEvent();

        private float baseIntensity;

        /// <summary>True se una creatura della specie è sul cavo in questo frame.</summary>
        public bool IsShorted { get; private set; }

        /// <summary>True dopo che la corrente è stata ridata.</summary>
        public bool IsPowered { get; private set; }

        private void Awake()
        {
            if (cableLight != null) baseIntensity = cableLight.intensity;
        }

        private void OnEnable()
        {
            if (manager == null) Debug.LogError($"[ShortedCable] {name}: CreatureManager non assegnato.", this);
            else manager.AddSource(this);
        }

        private void OnDisable()
        {
            if (manager != null) manager.RemoveSource(this);
        }

        private void Update()
        {
            IsShorted = CountShorting() > 0;

            if (lever != null && !IsPowered) lever.SetPromptText(IsShorted ? shortedPrompt : readyPrompt);

            if (cableLight != null)
                cableLight.intensity = IsShorted && !IsPowered
                    ? baseIntensity * Mathf.PerlinNoise(Time.time * 12f, 0f)
                    : baseIntensity;
        }

        /// <summary>Ridà corrente se il circuito è libero (da collegare a <c>SimpleInteractable.onInteracted</c>).</summary>
        public void TryRestorePower()
        {
            if (IsPowered) return;
            if (IsShorted)
            {
                onShortedAttempt.Invoke();
                return;
            }

            IsPowered = true;
            if (lever != null) lever.SetInteractable(false);
            onPowerRestored.Invoke();
        }

        public void EmitStimuli(List<CreatureStimulus> output)
        {
            CreatureStimulus glow = CreatureStimulus.PointLight(transform.position, lightRadius, StimulusCategory.WeakLight, false);
            glow.OnlySpecies = shortingSpecies;
            output.Add(glow);
        }

        private int CountShorting()
        {
            if (manager == null || shortingSpecies == null) return 0;
            float sqr = shortRadius * shortRadius;
            int count = 0;
            IReadOnlyList<Creature> creatures = manager.ActiveCreatures;
            for (int i = 0; i < creatures.Count; i++)
            {
                Creature c = creatures[i];
                if (ReferenceEquals(c.Profile, shortingSpecies) &&
                    (c.transform.position - transform.position).sqrMagnitude <= sqr) count++;
            }
            return count;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, lightRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, shortRadius);
        }
    }
}

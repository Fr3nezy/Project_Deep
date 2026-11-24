using System.Collections.Generic;
using Deeploration.Creatures;
using Deeploration.Interaction;
using Deeploration.Prologue;
using UnityEngine;

namespace Deeploration.Comms
{
    /// <summary>
    /// Battuta critica alla prima allerta di una <see cref="CreatureStealth"/> (Neutral verso Investigate o Attack)
    /// mentre il giocatore tiene in mano un oggetto che emette luce (stimolo WeakLight del sub). Ascolta l'evento
    /// statico <see cref="CreatureStealth.AnyAlertChanged"/> perché le creature sono in pool.
    /// </summary>
    public sealed class FaunaCommsCue : MonoBehaviour
    {
        [SerializeField] private SubtitleLine line;
        [Tooltip("Una sola volta per partita.")]
        [SerializeField] private bool once = true;

        private HelmetComms comms;
        private PlayerGrab grab;
        private bool played;
        private readonly List<CreatureStimulus> probe = new List<CreatureStimulus>(2);

        private void OnEnable() => CreatureStealth.AnyAlertChanged += OnAlert;
        private void OnDisable() => CreatureStealth.AnyAlertChanged -= OnAlert;

        private void OnAlert(CreatureStealth creature, StealthAlert alert)
        {
            if (alert == StealthAlert.Neutral || (once && played) || line == null) return;
            if (!HoldsLitItem()) return;
            if (comms == null) comms = FindFirstObjectByType<HelmetComms>();
            if (comms == null) { Debug.LogError("[FaunaCommsCue] Nessun HelmetComms in scena.", this); return; }
            played = true;
            comms.Say(line, CommsPriority.Critical);
        }

        // Cella accesa in mano: Grabbable tenuto (IsHeld) che emette davvero uno stimolo WeakLight del sub.
        private bool HoldsLitItem()
        {
            if (grab == null) grab = FindFirstObjectByType<PlayerGrab>();
            Grabbable held = grab != null ? grab.Held : null;
            if (held == null || !held.IsHeld) return false;
            probe.Clear();
            held.EmitStimuli(probe);
            foreach (CreatureStimulus s in probe)
                if (s.Category == StimulusCategory.WeakLight && s.FromPlayer) return true;
            return false;
        }
    }
}

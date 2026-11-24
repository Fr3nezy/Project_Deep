using System.Collections.Generic;
using Deeploration.Comms;
using Deeploration.Interaction;
using Deeploration.Prologue;
using UnityEngine;

namespace Deeploration.Player
{
    /// <summary>
    /// Punto d'interesse tracciato da <see cref="HelmetMarkers"/> sulla visiera olografica e/o sulla bussola del casco.
    /// Parte nascosto e compare una volta sola: quando H.E.L.M. pronuncia <c>revealOnLine</c> (stile Subnautica: lo nomina,
    /// poi lo segna) o la prima volta che <c>revealWhenHeld</c> viene preso in mano. Sparisce per sempre con <see cref="Dismiss"/>,
    /// quando <c>dismissWhenHeld</c> viene preso o quando il diver arriva entro <c>dismissWithinDistance</c>.
    /// Si registra in automatico in OnEnable/OnDisable.
    /// </summary>
    public sealed class HelmetMarkerTarget : MonoBehaviour
    {
        private static readonly List<HelmetMarkerTarget> ActiveTargets = new();

        [Tooltip("Icona bianca su alfa (es. T_Marker_O2, T_Marker_Objective, T_Marker_Power, T_Marker_Plug). Vuoto = icona default di HelmetMarkers.")]
        [SerializeField] private Texture2D icon;
        [Tooltip("Offset nello spazio locale del bersaglio, così il marker punta sopra l'oggetto e non ai piedi.")]
        [SerializeField] private Vector3 localOffset = new(0f, 1.4f, 0f);
        [Tooltip("Se vero il marker è visibile dall'avvio. Di regola falso: niente marker prima che H.E.L.M. ne parli.")]
        [SerializeField] private bool startVisible;

        [Header("Comparsa (una volta sola)")]
        [Tooltip("Battuta di H.E.L.M. che rivela il marker quando comincia sullo schermo comms.")]
        [SerializeField] private SubtitleLine revealOnLine;
        [Tooltip("Oggetto che rivela il marker la prima volta che il diver lo prende (es. la cella rivela la presa).")]
        [SerializeField] private Grabbable revealWhenHeld;

        [Header("Scomparsa (definitiva)")]
        [Tooltip("Oggetto che spegne il marker quando viene preso o inserito (es. il marker sulla cella stessa).")]
        [SerializeField] private Grabbable dismissWhenHeld;
        [Tooltip("Il marker si spegne quando la camera arriva entro questa distanza dal bersaglio (m). 0 = mai.")]
        [SerializeField, Min(0f)] private float dismissWithinDistance;

        private bool visible, revealed, dismissed;

        public static IReadOnlyList<HelmetMarkerTarget> Active => ActiveTargets;
        public Texture2D Icon => icon;
        public bool IsVisible => visible && isActiveAndEnabled;
        public Vector3 WorldPosition => transform.TransformPoint(localOffset);

        private void Awake()
        {
            visible = startVisible;
            revealed = startVisible;
        }

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this)) ActiveTargets.Add(this);
            HelmetComms.LineStarted += OnLineStarted;
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
            HelmetComms.LineStarted -= OnLineStarted;
        }

        private void Update()
        {
            if (dismissed) return;
            if (revealWhenHeld != null && revealWhenHeld.IsHeld) Reveal();
            if (!visible) return;
            if (dismissWhenHeld != null && (dismissWhenHeld.IsHeld || dismissWhenHeld.IsSocketed)) Dismiss();
            else if (dismissWithinDistance > 0f && Camera.main != null &&
                     (Camera.main.transform.position - WorldPosition).sqrMagnitude < dismissWithinDistance * dismissWithinDistance)
                Dismiss();
        }

        private void OnLineStarted(SubtitleLine line)
        {
            if (revealOnLine != null && line == revealOnLine) Reveal();
        }

        /// <summary>Mostra il marker la prima volta; dopo un Dismiss o una comparsa già avvenuta non fa nulla.</summary>
        public void Reveal()
        {
            if (revealed || dismissed) return;
            revealed = true;
            visible = true;
        }

        /// <summary>Spegne il marker per sempre (es. da GrabSocket.onSocketed a obiettivo chiuso).</summary>
        public void Dismiss()
        {
            dismissed = true;
            visible = false;
        }

        /// <summary>Mostra o nasconde il marker senza le regole di comparsa unica (controllo manuale da UnityEvent).</summary>
        public void SetMarkerVisible(bool show) => visible = show;
    }
}

using Deeploration.Prologue;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Comms
{
    /// <summary>
    /// Suggerimento di H.E.L.M. a tempo: una volta armato, se il giocatore non risolve entro seconds di silenzio
    /// (il conto riparte a ogni battuta finita), manda line una volta sola. Cancel() o cancelOnLine lo annullano per sempre.
    /// Si arma alla fine di armAfterLine, all'ingresso del diver nel trigger di questo oggetto o da Arm().
    /// </summary>
    public sealed class CommsHint : MonoBehaviour
    {
        [SerializeField] private SubtitleLine line;
        [SerializeField] private CommsPriority priority = CommsPriority.Dialogue;
        [Tooltip("Secondi di silenzio di H.E.L.M. dall'armamento (o dall'ultima battuta finita) prima del suggerimento.")]
        [SerializeField, Min(1f)] private float seconds = 40f;
        [Tooltip("Si arma quando HelmetComms finisce questa battuta. Vuoto = solo dal trigger o da Arm().")]
        [SerializeField] private SubtitleLine armAfterLine;
        [Tooltip("Se attivo, si arma quando il diver entra nel collider trigger di questo oggetto.")]
        [SerializeField] private bool armOnEnter;
        [Tooltip("Annulla il suggerimento quando HelmetComms comincia questa battuta (il giocatore ci è arrivato da solo).")]
        [SerializeField] private SubtitleLine cancelOnLine;
        [Tooltip("Dopo il suggerimento (es. HelmetMarkerTarget.Reveal).")]
        public UnityEvent onPlayed = new UnityEvent();

        private HelmetComms comms;
        private bool armed;
        private bool done;
        private float idleSince;

        public bool Armed => armed;
        public bool Done => done;

        private void Start()
        {
            comms = FindFirstObjectByType<HelmetComms>();
            if (comms == null) Debug.LogError("[CommsHint] Nessun HelmetComms in scena.", this);
        }

        private void OnEnable()
        {
            HelmetComms.LineStarted += OnLineStarted;
            HelmetComms.LineFinished += OnLineFinished;
        }

        private void OnDisable()
        {
            HelmetComms.LineStarted -= OnLineStarted;
            HelmetComms.LineFinished -= OnLineFinished;
        }

        private void OnLineStarted(SubtitleLine started)
        {
            if (started != null && started == cancelOnLine) Cancel();
        }

        private void OnLineFinished(SubtitleLine finished)
        {
            if (finished != null && finished == armAfterLine) Arm();
            idleSince = Time.time;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (armOnEnter && other.GetComponentInParent<CharacterController>() != null) Arm();
        }

        /// <summary>Fa partire il conto (una volta sola; nulla se già annullato).</summary>
        public void Arm()
        {
            if (armed || done) return;
            armed = true;
            idleSince = Time.time;
        }

        /// <summary>Annulla per sempre il suggerimento (es. Photocell.onTriggered, modulo raccolto).</summary>
        public void Cancel() => done = true;

        private void Update()
        {
            if (!armed || done || comms == null) return;
            if (comms.IsBusy) { idleSince = Time.time; return; }
            if (Time.time - idleSince < seconds) return;
            done = true;
            if (line != null) comms.Say(line, priority);
            onPlayed?.Invoke();
        }
    }
}

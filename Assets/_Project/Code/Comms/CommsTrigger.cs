using Deeploration.Interaction;
using Deeploration.Prologue;
using UnityEngine;

namespace Deeploration.Comms
{
    /// <summary>
    /// Manda battute allo schermo comunicazioni del casco: quando il diver entra nel trigger (collider con
    /// isTrigger su questo oggetto) oppure da un evento di scena collegato a <see cref="Play"/>
    /// (es. onRejected di una presa, sblocco della luce rossa). Con <c>playOnGaze</c> parte invece quando il diver,
    /// dentro il trigger, guarda <c>gazeTarget</c> abbastanza a lungo. Le battute vanno in coda nell'ordine dell'array.
    /// </summary>
    public sealed class CommsTrigger : MonoBehaviour
    {
        [SerializeField] private SubtitleLine[] lines;
        [SerializeField] private CommsPriority priority = CommsPriority.Dialogue;
        [Tooltip("Parte una volta sola per partita.")]
        [SerializeField] private bool once = true;
        [Tooltip("Parte quando il diver entra nel collider trigger di questo oggetto.")]
        [SerializeField] private bool playOnEnter = true;
        [Tooltip("Parte quando il diver, dentro il trigger, guarda gazeTarget per gazeSeconds di fila. Se attivo, l'ingresso da solo non fa partire la battuta (anche con playOnEnter).")]
        [SerializeField] private bool playOnGaze;
        [Tooltip("Oggetto da guardare (es. il cartello). Un hit sul suo collider o su quello di un figlio vale come linea libera.")]
        [SerializeField] private Transform gazeTarget;
        [Tooltip("Secondi continui di sguardo necessari.")]
        [SerializeField, Min(0f)] private float gazeSeconds = 1f;
        [Tooltip("Semi-angolo massimo (gradi) fra il forward della camera e la direzione verso gazeTarget.")]
        [SerializeField, Range(1f, 90f)] private float gazeAngle = 10f;
        [Tooltip("Parte la prima volta che il diver prende in mano questo oggetto (es. la cella di energia). Vuoto = ignorato.")]
        [SerializeField] private Grabbable playWhenHeld;

        private HelmetComms comms;
        private bool played;
        private bool inside;
        private float gazeTime;

        private void Start()
        {
            comms = FindFirstObjectByType<HelmetComms>();
            if (comms == null) Debug.LogError("[CommsTrigger] Nessun HelmetComms in scena.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<CharacterController>() == null) return;
            inside = true;
            gazeTime = 0f;
            if (playOnEnter && !playOnGaze) Play();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<CharacterController>() == null) return;
            inside = false;
            gazeTime = 0f;
        }

        private void OnDisable()
        {
            inside = false;
            gazeTime = 0f;
        }

        private void Update()
        {
            if (playWhenHeld != null && playWhenHeld.IsHeld && !played) Play();
            if (!playOnGaze || !inside || gazeTarget == null || (once && played)) return;
            if (IsLookingAtTarget()) gazeTime += Time.deltaTime; else gazeTime = 0f;
            if (gazeTime < gazeSeconds) return;
            gazeTime = 0f;
            Play();
        }

        private bool IsLookingAtTarget()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 from = cam.transform.position;
            Vector3 to = gazeTarget.position - from;
            if (Vector3.Angle(cam.transform.forward, to) > gazeAngle) return false;
            RaycastHit[] hits = Physics.RaycastAll(from, to.normalized, to.magnitude + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            bool hitTarget = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                hitTarget = hit.collider.transform.IsChildOf(gazeTarget);
            }
            return hitTarget;
        }

        /// <summary>Mette in coda le battute. Con once attivo, solo la prima volta.</summary>
        public void Play()
        {
            if (comms == null || (once && played)) return;
            played = true;
            foreach (SubtitleLine line in lines) comms.Say(line, priority);
        }
    }
}

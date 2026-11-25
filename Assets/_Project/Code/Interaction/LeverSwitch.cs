using Deeploration.Comms;
using Deeploration.Prologue;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Interaction
{
    /// <summary>
    /// Leva fisica: un Rigidbody con HingeJoint e Grabbable che il diver tira con la presa (PlayerGrab).
    /// Quando l'angolo della cerniera supera triggerAngle scatta una volta sola, resta ferma e lancia onPulled.
    /// Con unlockAfterLine resta bloccata (cinematica) finché H.E.L.M. non finisce quella battuta.
    /// </summary>
    [RequireComponent(typeof(HingeJoint))]
    public sealed class LeverSwitch : MonoBehaviour
    {
        [Tooltip("Angolo della cerniera (gradi, in valore assoluto) oltre cui la leva scatta. Deve stare dentro i limiti del HingeJoint.")]
        [SerializeField] private float triggerAngle = 55f;
        [Tooltip("Leva bloccata finché HelmetComms non finisce questa battuta. Vuoto = libera da subito.")]
        [SerializeField] private SubtitleLine unlockAfterLine;
        public UnityEvent onPulled = new UnityEvent();
        [Tooltip("Sbloccata e mai toccata per questi secondi dalla fine dell'ultima battuta di HelmetComms: lancia onReminder (per esempio una battuta di H.E.L.M.) e riconta. 0 = mai.")]
        [SerializeField, Min(0f)] private float reminderSeconds = 12f;
        public UnityEvent onReminder = new UnityEvent();

        private HingeJoint hinge;
        private Rigidbody body;
        private Grabbable grabbable;
        private float idleSince;

        public bool Locked { get; private set; }
        public bool Pulled { get; private set; }
        public float Angle => hinge != null ? hinge.angle : 0f;

        private void Awake()
        {
            hinge = GetComponent<HingeJoint>();
            body = GetComponent<Rigidbody>();
            grabbable = GetComponent<Grabbable>();
            SetLocked(unlockAfterLine != null);
        }

        private void OnEnable() => HelmetComms.LineFinished += OnLineFinished;
        private void OnDisable() => HelmetComms.LineFinished -= OnLineFinished;

        private void OnLineFinished(SubtitleLine line)
        {
            if (line == unlockAfterLine) Unlock();
            // Il promemoria conta dal silenzio: mentre H.E.L.M. parla della leva non deve scattare.
            else if (!Locked && !Pulled) idleSince = Time.time;
        }

        /// <summary>Sblocca la leva (anche da evento di scena o cheat).</summary>
        public void Unlock()
        {
            if (Locked) idleSince = Time.time;
            SetLocked(false);
        }

        private void SetLocked(bool locked)
        {
            Locked = locked;
            if (body != null) body.isKinematic = locked || Pulled;
        }

        private void FixedUpdate()
        {
            if (Locked || Pulled) return;
            if (grabbable != null && grabbable.IsHeld) idleSince = Time.time;
            else if (reminderSeconds > 0f && Time.time - idleSince >= reminderSeconds)
            {
                idleSince = Time.time;
                onReminder?.Invoke();
            }
            // NaN (stato fisico rotto) fallirebbe il confronto e farebbe scattare la leva da ferma: visto nel playtest 2026-11-25.
            float angle = hinge.angle;
            if (float.IsNaN(angle) || Mathf.Abs(angle) < triggerAngle) return;
            Pulled = true;
            body.isKinematic = true;
            onPulled?.Invoke();
        }
    }
}

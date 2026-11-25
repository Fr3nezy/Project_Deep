using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Interaction
{
    /// <summary>
    /// Slot per oggetti trascinati: quando un <see cref="Grabbable"/> con itemId uguale entra nel trigger, viene
    /// bloccato su snapPoint (la presa si stacca) e parte onSocketed. Con allowEject il giocatore può sfilarlo
    /// afferrandolo (onUnsocketed) e reinserirlo; senza, una volta sola. Un oggetto con rejectedItemId portato nel
    /// trigger (la cella scarica) viene respinto: la presa si stacca, l'oggetto è spinto via e parte onRejected.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class GrabSocket : MonoBehaviour
    {
        [SerializeField] private string acceptedItemId = "power_cell";
        [Tooltip("Posa dell'oggetto inserito. Vuoto = questo transform.")]
        [SerializeField] private Transform snapPoint;

        [Header("Poggiamento fisico")]
        [Tooltip("Spento: l'oggetto si aggancia appena entra nel trigger (snap). Acceso: solo se è sopra snapPoint, dritto e quasi fermo.")]
        [SerializeField] private bool requireSeating;
        [Tooltip("Distanza orizzontale massima del centro dell'oggetto da snapPoint, in metri.")]
        [SerializeField, Min(0f)] private float seatRadius = 0.05f;
        [Tooltip("Scarto verticale massimo dal centro dell'oggetto a snapPoint, in metri.")]
        [SerializeField, Min(0f)] private float seatHeight = 0.06f;
        [Tooltip("Inclinazione massima rispetto all'alto di snapPoint, in gradi.")]
        [SerializeField, Range(0f, 90f)] private float maxTilt = 12f;
        [Tooltip("Velocità massima dell'oggetto, in m/s.")]
        [SerializeField, Min(0f)] private float maxSpeed = 0.35f;
        [Tooltip("Secondi in cui le condizioni devono restare vere prima dell'aggancio.")]
        [SerializeField, Min(0f)] private float seatTime = 0.15f;

        [Header("Magnete")]
        [Tooltip("Raggio attorno a snapPoint entro cui l'oggetto trascinato viene attirato e raddrizzato, in metri. 0 = spento.")]
        [SerializeField, Min(0f)] private float magnetRadius = 0f;
        [Tooltip("Velocità cercata verso snapPoint per metro di distanza (1/s).")]
        [SerializeField, Min(0f)] private float magnetFollow = 3f;
        [Tooltip("Velocità massima con cui il magnete porta l'oggetto, in m/s. Sotto maxSpeed l'oggetto può poggiarsi.")]
        [SerializeField, Min(0f)] private float magnetMaxSpeed = 0.3f;
        [Tooltip("Forza massima al centro, in N; cala a 0 sul bordo del raggio. La presa di PlayerGrab cede in proporzione al peso del magnete.")]
        [SerializeField, Min(0f)] private float magnetMaxForce = 900f;
        [Tooltip("Rigidità della molla che raddrizza l'oggetto sull'alto di snapPoint (rad/s), al centro.")]
        [SerializeField, Min(0f)] private float magnetUprightFrequency = 6f;
        [Tooltip("Se il punto di presa del giocatore va oltre questa distanza da snapPoint, il magnete molla, in metri.")]
        [SerializeField, Min(0f)] private float magnetReleaseDistance = 1.2f;
        [Tooltip("Secondi di oggetto fermo e non allineato dentro il raggio prima che il magnete molli (incastro); molla per altrettanti secondi.")]
        [SerializeField, Min(0f)] private float magnetStuckTime = 1f;
        // Playtest 2026-11-25: tirata dritta, il fondo della cella sbatteva sul bordo della presa (Inlet ~16 cm sopra il fondale).
        [Tooltip("Imbuto: il magnete punta sopra snapPoint di tanti metri per metro di scarto orizzontale, poi scende man mano che l'oggetto si centra. 0 = dritto.")]
        [SerializeField, Min(0f)] private float magnetFunnelSlope = 1f;

        [Header("Feedback")]
        [SerializeField] private Light statusLight;
        [SerializeField] private Color emptyColor = Color.red;
        [SerializeField] private Color filledColor = Color.green;

        // Prima la cella scarica entrava nel trigger senza nessuna reazione e il giocatore non capiva (playtest 2026-11-23).
        [Header("Rifiuto")]
        [Tooltip("Id dell'oggetto che la presa respinge con una spinta e onRejected. Vuoto = nessun rifiuto.")]
        [SerializeField] private string rejectedItemId = "power_cell_dead";
        [Tooltip("Velocità con cui l'oggetto respinto viene allontanato da snapPoint, in m/s.")]
        [SerializeField, Min(0f)] private float rejectSpeed = 2.5f;
        [Tooltip("Scintille o altro effetto da avviare al rifiuto, su snapPoint. Opzionale.")]
        [SerializeField] private ParticleSystem rejectEffect;

        [Header("Estrazione")]
        [Tooltip("Acceso: afferrando l'oggetto inserito il giocatore lo sfila e parte onUnsocketed. Spento: resta bloccato per sempre.")]
        [SerializeField] private bool allowEject;

        public UnityEvent onSocketed = new UnityEvent();
        public UnityEvent onUnsocketed = new UnityEvent();
        public UnityEvent onRejected = new UnityEvent();

        [Header("Debug (sola lettura)")]
        [SerializeField] private Grabbable current;

        public bool IsOccupied => current != null;
        public Grabbable Current => current;
        public bool AllowEject => allowEject;

        /// <summary>Vero nei frame in cui un oggetto accettato è in posizione per agganciarsi (solo con requireSeating).</summary>
        public bool IsAligned { get; private set; }

        private float alignedSince = -1f;
        private float lastAlignedCheck = -1f;
        private PlayerGrab playerGrab;
        // Strappo: magnete spento finché l'oggetto non esce dal raggio. Incastro: spento per magnetStuckTime, poi riprova.
        private bool magnetDisarmed;
        private float stuckFor;
        private float stuckPauseUntil = -1f;
        // Appena sfilato: ignorato finché non si allontana, se no lo slot lo riprende subito.
        private Grabbable ejected;

        private void Awake()
        {
            if (snapPoint == null) snapPoint = transform;
            if (statusLight != null) statusLight.color = emptyColor;
        }

        private void Start() => playerGrab = FindAnyObjectByType<PlayerGrab>();

        private void OnTriggerStay(Collider other)
        {
            if (current != null || other.attachedRigidbody == null) return;
            Grabbable item = other.attachedRigidbody.GetComponent<Grabbable>();
            if (item == null || item == ejected || item.IsSocketed) return;
            if (!string.IsNullOrEmpty(rejectedItemId) && item.ItemId == rejectedItemId) { if (item.IsHeld) Reject(item); return; }
            if (item.ItemId != acceptedItemId) return;
            if (requireSeating && !Seated(item)) return;
            Insert(item);
        }

        private void FixedUpdate()
        {
            // OnTriggerStay non arriva più quando l'oggetto esce: senza conferma nell'ultimo passo fisico si azzera.
            if (Time.time - lastAlignedCheck > Time.fixedDeltaTime * 1.5f)
            {
                IsAligned = false;
                alignedSince = -1f;
            }
            if (ejected != null && Vector3.Distance(ejected.transform.position, snapPoint.position) > Mathf.Max(magnetRadius, 0.5f))
                ejected = null;
            Attract();
        }

        // Solo l'oggetto tenuto in mano: una cella lasciata vicino allo slot non ci scivola dentro da sola.
        private void Attract()
        {
            if (magnetRadius <= 0f || current != null || playerGrab == null) return;
            Grabbable item = playerGrab.Held;
            if (item == null || item == ejected || item.IsSocketed || item.ItemId != acceptedItemId) return;

            Rigidbody body = item.Body;
            Vector3 delta = snapPoint.position - body.position;
            float distance = delta.magnitude;
            if (distance > magnetRadius) { magnetDisarmed = false; stuckFor = 0f; return; }
            if (magnetDisarmed || Time.time < stuckPauseUntil) return;

            // Il magnete al centro batte la forza della presa: senza queste uscite una cella incastrata storta non si stacca più.
            if (Vector3.Distance(playerGrab.HoldTarget, snapPoint.position) > magnetReleaseDistance)
            {
                magnetDisarmed = true;
                stuckFor = 0f;
                return;
            }
            bool stuck = !IsAligned && body.linearVelocity.magnitude < 0.05f;
            stuckFor = stuck ? stuckFor + Time.fixedDeltaTime : 0f;
            if (stuckFor >= magnetStuckTime)
            {
                stuckPauseUntil = Time.time + magnetStuckTime;
                stuckFor = 0f;
                return;
            }

            float weight = 1f - distance / magnetRadius;
            playerGrab.MagnetYield = Mathf.Max(playerGrab.MagnetYield, weight);
            float horizontal = Vector3.ProjectOnPlane(delta, snapPoint.up).magnitude;
            Vector3 aim = delta + snapPoint.up * (horizontal * magnetFunnelSlope);
            Vector3 desired = Vector3.ClampMagnitude(aim * magnetFollow, magnetMaxSpeed);
            Vector3 force = (desired - body.linearVelocity) * body.mass / Time.fixedDeltaTime;
            body.AddForce(Vector3.ClampMagnitude(force, magnetMaxForce * weight));
            item.AlignUp(snapPoint.up, magnetUprightFrequency * weight);
        }

        // Centro dell'oggetto sopra snapPoint, dritto e quasi fermo per seatTime secondi.
        private bool Seated(Grabbable item)
        {
            Vector3 offset = item.transform.position - snapPoint.position;
            Vector3 up = snapPoint.up;
            float vertical = Vector3.Dot(offset, up);
            float horizontal = (offset - up * vertical).magnitude;
            bool aligned = horizontal <= seatRadius && Mathf.Abs(vertical) <= seatHeight &&
                           Vector3.Angle(item.transform.up, up) <= maxTilt &&
                           item.Body.linearVelocity.magnitude <= maxSpeed;

            lastAlignedCheck = Time.time;
            IsAligned = aligned;
            if (!aligned) { alignedSince = -1f; return false; }
            if (alignedSince < 0f) alignedSince = Time.time;
            return Time.time - alignedSince >= seatTime;
        }

        /// <summary>Blocca l'oggetto nello slot come se ci fosse entrato.</summary>
        public void Insert(Grabbable item)
        {
            if (current != null || item == null) return;
            current = item;
            item.Socket(snapPoint);
            item.Holder = this;
            if (statusLight != null) statusLight.color = filledColor;
            onSocketed?.Invoke();
        }

        /// <summary>Respinge un oggetto tenuto in mano: la presa del giocatore si stacca, l'oggetto è spinto via da snapPoint e parte onRejected.</summary>
        public void Reject(Grabbable item)
        {
            if (item == null) return;
            if (playerGrab != null && playerGrab.Held == item) playerGrab.Release();
            ejected = item; // ignorato finché non si allontana, come dopo un'estrazione
            Vector3 away = item.Body.position - snapPoint.position;
            away = (away.sqrMagnitude > 0.0001f ? away.normalized : -snapPoint.forward) + snapPoint.up * 0.5f;
            item.Body.linearVelocity = away.normalized * rejectSpeed;
            if (rejectEffect != null)
            {
                rejectEffect.transform.position = snapPoint.position;
                rejectEffect.Play();
            }
            onRejected?.Invoke();
        }

        /// <summary>Sfila l'oggetto inserito, se allowEject è acceso. Lo chiama PlayerGrab quando il giocatore lo afferra.</summary>
        public void Eject()
        {
            if (current == null || !allowEject) return;
            ejected = current;
            current = null;
            ejected.Unsocket();
            if (statusLight != null) statusLight.color = emptyColor;
            onUnsocketed?.Invoke();
        }
    }
}

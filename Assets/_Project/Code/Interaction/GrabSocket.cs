using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Interaction
{
    /// <summary>
    /// Slot per oggetti trascinati: quando un <see cref="Grabbable"/> con itemId uguale entra nel trigger, viene
    /// bloccato su snapPoint (la presa si stacca) e parte onSocketed. Una volta sola.
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
        [Tooltip("Forza massima al centro, in N; cala a 0 sul bordo del raggio. Sopra PlayerGrab.maxForce il magnete vince vicino allo slot.")]
        [SerializeField, Min(0f)] private float magnetMaxForce = 900f;
        [Tooltip("Rigidità della molla che raddrizza l'oggetto sull'alto di snapPoint (rad/s), al centro.")]
        [SerializeField, Min(0f)] private float magnetUprightFrequency = 6f;

        [Header("Feedback")]
        [SerializeField] private Light statusLight;
        [SerializeField] private Color emptyColor = Color.red;
        [SerializeField] private Color filledColor = Color.green;

        public UnityEvent onSocketed = new UnityEvent();

        [Header("Debug (sola lettura)")]
        [SerializeField] private Grabbable current;

        public bool IsOccupied => current != null;
        public Grabbable Current => current;

        /// <summary>Vero nei frame in cui un oggetto accettato è in posizione per agganciarsi (solo con requireSeating).</summary>
        public bool IsAligned { get; private set; }

        private float alignedSince = -1f;
        private float lastAlignedCheck = -1f;
        private PlayerGrab playerGrab;

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
            if (item == null || item.IsSocketed || item.ItemId != acceptedItemId) return;
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
            Attract();
        }

        // Solo l'oggetto tenuto in mano: una cella lasciata vicino allo slot non ci scivola dentro da sola.
        private void Attract()
        {
            if (magnetRadius <= 0f || current != null || playerGrab == null) return;
            Grabbable item = playerGrab.Held;
            if (item == null || item.IsSocketed || item.ItemId != acceptedItemId) return;

            Rigidbody body = item.Body;
            Vector3 delta = snapPoint.position - body.position;
            float distance = delta.magnitude;
            if (distance > magnetRadius) return;

            float weight = 1f - distance / magnetRadius;
            Vector3 desired = Vector3.ClampMagnitude(delta * magnetFollow, magnetMaxSpeed);
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
            if (statusLight != null) statusLight.color = filledColor;
            onSocketed?.Invoke();
        }
    }
}

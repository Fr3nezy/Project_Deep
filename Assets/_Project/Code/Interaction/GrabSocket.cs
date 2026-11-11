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

        private void Awake()
        {
            if (snapPoint == null) snapPoint = transform;
            if (statusLight != null) statusLight.color = emptyColor;
        }

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

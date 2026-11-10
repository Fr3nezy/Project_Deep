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

        [Header("Feedback")]
        [SerializeField] private Light statusLight;
        [SerializeField] private Color emptyColor = Color.red;
        [SerializeField] private Color filledColor = Color.green;

        public UnityEvent onSocketed = new UnityEvent();

        [Header("Debug (sola lettura)")]
        [SerializeField] private Grabbable current;

        public bool IsOccupied => current != null;
        public Grabbable Current => current;

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
            Insert(item);
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

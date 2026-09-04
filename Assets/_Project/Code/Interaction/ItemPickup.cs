using UnityEngine;

namespace Deeploration.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [Header("Identità Oggetto")]
        [SerializeField] private string itemId = "power_cell";
        [SerializeField] private string itemName = "Cella Energetica";

        [Header("Fisica & Stato")]
        [SerializeField] private bool isCarried;
        [SerializeField] private Vector3 heldLocalOffset = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 heldLocalRotation = new Vector3(20f, -25f, 15f);
        [SerializeField] private float heldScaleFactor = 0.5f;

        private Collider itemCollider;
        private Rigidbody itemRigidbody;
        private Vector3 originalScale;

        public string ItemId => itemId;
        public string ItemName => itemName;
        public bool IsCarried => isCarried;

        public string PromptText => $"Raccogli {itemName}";

        private void Awake()
        {
            itemCollider = GetComponent<Collider>();
            itemRigidbody = GetComponent<Rigidbody>();
            originalScale = transform.localScale;
        }

        public bool CanInteract(GameObject user)
        {
            if (isCarried) return false;

            PlayerHands hands = user.GetComponent<PlayerHands>() ?? user.GetComponentInChildren<PlayerHands>();
            return hands != null && !hands.IsHoldingItem;
        }

        public void Interact(GameObject user)
        {
            PlayerHands hands = user.GetComponent<PlayerHands>() ?? user.GetComponentInChildren<PlayerHands>();
            if (hands != null)
            {
                hands.Pickup(this);
            }
        }

        public void OnPickedUp(Transform holdParent)
        {
            isCarried = true;
            if (itemCollider != null) itemCollider.enabled = false;
            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = true;
                itemRigidbody.linearVelocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
            }

            transform.SetParent(holdParent);
            transform.localPosition = heldLocalOffset;
            transform.localRotation = Quaternion.Euler(heldLocalRotation);
            transform.localScale = originalScale * heldScaleFactor;
        }

        public void OnDropped()
        {
            isCarried = false;
            transform.SetParent(null);
            transform.localScale = originalScale;

            if (itemCollider != null) itemCollider.enabled = true;
            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = false;
            }
        }

        public void OnSocketed(Transform socketTransform)
        {
            isCarried = false;
            transform.SetParent(socketTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = originalScale;

            if (itemCollider != null) itemCollider.enabled = true;
            if (itemRigidbody != null) itemRigidbody.isKinematic = true;
        }
    }
}

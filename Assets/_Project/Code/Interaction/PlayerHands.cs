using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deeploration.Interaction
{
    public class PlayerHands : MonoBehaviour
    {
        [Header("Punto di Presa")]
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Vector3 defaultHoldOffset = new Vector3(0.35f, -0.32f, 0.65f);
        [SerializeField] private KeyCode dropKey = KeyCode.G;

        [Header("Debug")]
        [SerializeField] private ItemPickup heldItem;

        public ItemPickup CurrentItem => heldItem;
        public bool IsHoldingItem => heldItem != null;

        public event Action<ItemPickup> OnItemPickedUp;
        public event Action<ItemPickup> OnItemDropped;

        private void Awake()
        {
            if (holdPoint == null)
            {
                Camera cam = GetComponentInChildren<Camera>() ?? Camera.main;
                if (cam != null)
                {
                    GameObject hp = new GameObject("HoldPoint");
                    hp.transform.SetParent(cam.transform);
                    hp.transform.localPosition = defaultHoldOffset;
                    hp.transform.localRotation = Quaternion.identity;
                    holdPoint = hp.transform;
                }
                else
                {
                    holdPoint = transform;
                }
            }
        }

        private void Update()
        {
            if (!IsHoldingItem) return;

            bool dropRequested = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                dropRequested = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(dropKey))
                dropRequested = true;
#endif

            if (dropRequested)
            {
                Drop();
            }
        }

        public bool Pickup(ItemPickup item)
        {
            if (item == null || IsHoldingItem) return false;

            heldItem = item;
            heldItem.OnPickedUp(holdPoint);
            OnItemPickedUp?.Invoke(heldItem);
            return true;
        }

        public ItemPickup Drop()
        {
            if (!IsHoldingItem) return null;

            ItemPickup item = heldItem;
            heldItem = null;
            item.OnDropped();
            OnItemDropped?.Invoke(item);
            return item;
        }

        public ItemPickup ReleaseItemForSocket()
        {
            if (!IsHoldingItem) return null;

            ItemPickup item = heldItem;
            heldItem = null;
            OnItemDropped?.Invoke(item);
            return item;
        }
    }
}

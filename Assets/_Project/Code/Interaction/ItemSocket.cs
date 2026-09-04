using System;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class ItemSocket : MonoBehaviour, IInteractable
    {
        [Header("Configurazione Alloggiamento")]
        [SerializeField] private string acceptedItemId = "power_cell";
        [SerializeField] private string socketName = "Alloggiamento Generatore";
        [SerializeField] private Transform snapPoint;

        [Header("Feedback Visivo")]
        [SerializeField] private Light statusLight;
        [SerializeField] private Color emptyColor = Color.red;
        [SerializeField] private Color filledColor = Color.green;

        [Header("Integrazione Quest")]
        [SerializeField] private string questObjectiveId = "gen_power";
        [SerializeField] private int questAdvanceAmount = 1;

        [Header("Eventi")]
        public UnityEvent<ItemPickup> onSocketFilled = new UnityEvent<ItemPickup>();

        [Header("Debug")]
        [SerializeField] private bool isOccupied;
        [SerializeField] private ItemPickup currentItem;

        public bool IsOccupied => isOccupied;
        public string AcceptedItemId => acceptedItemId;
        public ItemPickup CurrentItem => currentItem;

        public string PromptText
        {
            get
            {
                if (isOccupied) return $"{socketName} (ATTIVO)";
                return $"Inserisci Cella in {socketName}";
            }
        }

        private void Awake()
        {
            if (snapPoint == null) snapPoint = transform;
            UpdateLightStatus();
        }

        public bool CanInteract(GameObject user)
        {
            if (isOccupied) return false;

            PlayerHands hands = user.GetComponent<PlayerHands>() ?? user.GetComponentInChildren<PlayerHands>();
            if (hands == null || !hands.IsHoldingItem) return false;

            return hands.CurrentItem.ItemId == acceptedItemId;
        }

        public void Interact(GameObject user)
        {
            if (isOccupied) return;

            PlayerHands hands = user.GetComponent<PlayerHands>() ?? user.GetComponentInChildren<PlayerHands>();
            if (hands == null || !hands.IsHoldingItem) return;

            if (hands.CurrentItem.ItemId != acceptedItemId) return;

            ItemPickup item = hands.ReleaseItemForSocket();
            if (item != null)
            {
                currentItem = item;
                isOccupied = true;
                item.OnSocketed(snapPoint);
                UpdateLightStatus();
                onSocketFilled?.Invoke(item);

                // Notifica avanzamento al QuestManager
                if (!string.IsNullOrEmpty(questObjectiveId) && Deeploration.Quests.QuestManager.Instance != null)
                {
                    Deeploration.Quests.QuestManager.Instance.AdvanceObjective(questObjectiveId, questAdvanceAmount);
                }
            }
        }

        private void UpdateLightStatus()
        {
            if (statusLight != null)
            {
                statusLight.color = isOccupied ? filledColor : emptyColor;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class SimpleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "Interagisci";
        [SerializeField] private bool isReusable = false;
        [SerializeField] private bool isEnabled = true;

        [Header("Integrazione Quest")]
        [SerializeField] private string questObjectiveId;
        [SerializeField] private int questAdvanceAmount = 1;

        [Header("Eventi")]
        public UnityEvent onInteracted = new UnityEvent();

        private bool wasInteracted;

        public string PromptText => promptText;

        public bool CanInteract(GameObject user)
        {
            if (!isEnabled) return false;
            if (!isReusable && wasInteracted) return false;
            return true;
        }

        public void Interact(GameObject user)
        {
            if (!CanInteract(user)) return;

            wasInteracted = true;
            onInteracted?.Invoke();

            if (!string.IsNullOrEmpty(questObjectiveId) && Deeploration.Quests.QuestManager.Instance != null)
            {
                Deeploration.Quests.QuestManager.Instance.AdvanceObjective(questObjectiveId, questAdvanceAmount);
            }
        }

        public void SetInteractable(bool state)
        {
            isEnabled = state;
        }
    }
}

using UnityEngine;

namespace Deeploration.Quests
{
    public class QuestObjectiveTrigger : MonoBehaviour
    {
        [Header("Target Obiettivo")]
        [SerializeField] private string objectiveId = "o2_station";
        [SerializeField] private int amountToAdvance = 1;
        [SerializeField] private bool triggerOnlyOnce = true;

        [Header("Trigger su Ingresso Collider")]
        [SerializeField] private bool triggerOnEnter = false;
        [SerializeField] private string targetTag = "Player";

        private bool hasTriggered;

        public void TriggerAdvance()
        {
            if (triggerOnlyOnce && hasTriggered) return;

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AdvanceObjective(objectiveId, amountToAdvance);
                hasTriggered = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter) return;
            if (triggerOnlyOnce && hasTriggered) return;

            if (other.CompareTag(targetTag) || (other.transform.root != null && other.transform.root.CompareTag(targetTag)))
            {
                TriggerAdvance();
            }
        }
    }
}

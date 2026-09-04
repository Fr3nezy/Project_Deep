using UnityEngine;
using Deeploration.Quests;

namespace Deeploration.Environment
{
    public class AirlockDoor : MonoBehaviour
    {
        [Header("Feedback Visivo")]
        [SerializeField] private Light doorStatusLight;
        [SerializeField] private Transform doorPanel;
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 3.5f, 0f);
        [SerializeField] private float openSpeed = 2f;
        [SerializeField] private Color lockedColor = Color.red;
        [SerializeField] private Color unlockedColor = Color.green;

        [Header("Stato")]
        [SerializeField] private bool isOpen;

        private Vector3 closedPosition;
        private Vector3 targetPosition;

        private void Awake()
        {
            if (doorPanel != null)
            {
                closedPosition = doorPanel.localPosition;
                targetPosition = closedPosition;
            }

            if (doorStatusLight != null)
            {
                doorStatusLight.color = lockedColor;
            }
        }

        private void Start()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
                if (QuestManager.Instance.IsQuestCompleted)
                {
                    HandleQuestCompleted();
                }
            }
        }

        private void OnDestroy()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
            }
        }

        private void Update()
        {
            if (isOpen && doorPanel != null)
            {
                doorPanel.localPosition = Vector3.MoveTowards(doorPanel.localPosition, targetPosition, openSpeed * Time.deltaTime);
            }
        }

        public void HandleQuestCompleted()
        {
            isOpen = true;
            if (doorPanel != null)
            {
                targetPosition = closedPosition + openOffset;
            }

            if (doorStatusLight != null)
            {
                doorStatusLight.color = unlockedColor;
            }

            Debug.Log("<color=#10b981>[AirlockDoor]</color> Portale Base DR-04 SBLOCCATO E IN APERTURA!");
        }
    }
}

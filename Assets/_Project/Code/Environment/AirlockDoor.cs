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
        [Tooltip("Seconda anta (porte a due battenti), opzionale. Si sposta di secondOpenOffset insieme alla prima.")]
        [SerializeField] private Transform secondPanel;
        [SerializeField] private Vector3 secondOpenOffset = new Vector3(0f, 3.5f, 0f);
        [SerializeField] private float openSpeed = 2f;
        [SerializeField] private Color lockedColor = Color.red;
        [SerializeField] private Color unlockedColor = Color.green;

        [Header("Stato")]
        [SerializeField] private bool isOpen;

        private Vector3 closedPosition;
        private Vector3 targetPosition;
        private Vector3 secondClosedPosition;
        private Vector3 secondTargetPosition;

        private void Awake()
        {
            if (doorPanel != null)
            {
                closedPosition = doorPanel.localPosition;
                targetPosition = closedPosition;
            }

            if (secondPanel != null)
            {
                secondClosedPosition = secondPanel.localPosition;
                secondTargetPosition = secondClosedPosition;
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
            if (QuestManager.TryGetInstance(out QuestManager questManager))
            {
                questManager.OnQuestCompleted -= HandleQuestCompleted;
            }
        }

        private void Update()
        {
            if (isOpen && doorPanel != null)
            {
                doorPanel.localPosition = Vector3.MoveTowards(doorPanel.localPosition, targetPosition, openSpeed * Time.deltaTime);
            }

            if (isOpen && secondPanel != null)
            {
                secondPanel.localPosition = Vector3.MoveTowards(secondPanel.localPosition, secondTargetPosition, openSpeed * Time.deltaTime);
            }
        }

        public void HandleQuestCompleted()
        {
            isOpen = true;
            if (doorPanel != null)
            {
                targetPosition = closedPosition + openOffset;
            }

            if (secondPanel != null)
            {
                secondTargetPosition = secondClosedPosition + secondOpenOffset;
            }

            if (doorStatusLight != null)
            {
                doorStatusLight.color = unlockedColor;
            }

            Debug.Log("<color=#10b981>[AirlockDoor]</color> Portale Base DR-04 SBLOCCATO E IN APERTURA!");
        }
    }
}

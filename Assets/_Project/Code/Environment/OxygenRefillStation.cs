using UnityEngine;
using Deeploration.Player;
using Deeploration.Quests;

namespace Deeploration.Environment
{
    [RequireComponent(typeof(Collider))]
    public class OxygenRefillStation : MonoBehaviour
    {
        [Header("Parametri Ricarica")]
        [SerializeField] private float refillRatePerSecond = 10f; // Litri o % al secondo
        [SerializeField] private string questObjectiveId = "o2_beacon";

        [Header("Feedback Visivo")]
        [SerializeField] private Light stationBeaconLight;
        [SerializeField] private Color standbyColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.6f);

        private bool hasNotifiedQuest;
        private OxygenSystem playerOxygen;

        private void Awake()
        {
            if (stationBeaconLight != null)
                stationBeaconLight.color = standbyColor;
        }

        private void OnTriggerEnter(Collider other)
        {
            OxygenSystem oxy = other.GetComponentInParent<OxygenSystem>() ?? other.GetComponent<OxygenSystem>();
            if (oxy != null)
            {
                playerOxygen = oxy;

                if (!hasNotifiedQuest && QuestManager.Instance != null)
                {
                    hasNotifiedQuest = true;
                    QuestManager.Instance.AdvanceObjective(questObjectiveId, 1);
                }

                if (stationBeaconLight != null)
                    stationBeaconLight.color = activeColor;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (playerOxygen != null)
            {
                playerOxygen.Refill(Time.deltaTime);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            OxygenSystem oxy = other.GetComponentInParent<OxygenSystem>() ?? other.GetComponent<OxygenSystem>();
            if (oxy == playerOxygen)
            {
                playerOxygen = null;
                if (stationBeaconLight != null)
                    stationBeaconLight.color = standbyColor;
            }
        }
    }
}

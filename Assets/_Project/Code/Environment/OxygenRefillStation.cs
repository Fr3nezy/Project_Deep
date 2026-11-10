using UnityEngine;
using UnityEngine.Events;
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

        [Header("Alimentazione")]
        [Tooltip("Spenta, la stazione non ricarica finché qualcuno non chiama PowerOn (per esempio una cella inserita).")]
        [SerializeField] private bool startsPowered = true;
        public UnityEvent onPowered = new UnityEvent();

        [Header("Checkpoint")]
        [Tooltip("Punto di respawn registrato su DiverRespawn quando il diver entra a stazione accesa. Vuoto = questo transform.")]
        [SerializeField] private Transform respawnPoint;

        [Header("Feedback Visivo")]
        [SerializeField] private Light stationBeaconLight;
        [SerializeField] private Color standbyColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.6f);

        private bool hasNotifiedQuest;
        private bool isPowered;
        private OxygenSystem playerOxygen;

        public bool IsPowered => isPowered;

        private void Awake()
        {
            isPowered = startsPowered;
            if (respawnPoint == null) respawnPoint = transform;
            if (stationBeaconLight != null)
            {
                stationBeaconLight.color = standbyColor;
                stationBeaconLight.enabled = isPowered;
            }
        }

        /// <summary>Accende la stazione; se il diver è già dentro, inizia subito a ricaricare.</summary>
        public void PowerOn()
        {
            if (isPowered) return;
            isPowered = true;
            if (stationBeaconLight != null) stationBeaconLight.enabled = true;
            onPowered?.Invoke();
            if (playerOxygen != null) Dock(playerOxygen);
        }

        private void OnTriggerEnter(Collider other)
        {
            OxygenSystem oxy = other.GetComponentInParent<OxygenSystem>() ?? other.GetComponent<OxygenSystem>();
            if (oxy == null) return;

            playerOxygen = oxy;
            if (isPowered) Dock(oxy);
        }

        private void Dock(OxygenSystem oxy)
        {
            if (!hasNotifiedQuest && !string.IsNullOrEmpty(questObjectiveId) && QuestManager.Instance != null)
            {
                hasNotifiedQuest = true;
                QuestManager.Instance.AdvanceObjective(questObjectiveId, 1);
            }

            DiverRespawn respawn = oxy.GetComponent<DiverRespawn>();
            if (respawn != null) respawn.SetCheckpoint(respawnPoint);

            if (stationBeaconLight != null)
                stationBeaconLight.color = activeColor;
        }

        private void OnTriggerStay(Collider other)
        {
            if (isPowered && playerOxygen != null)
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

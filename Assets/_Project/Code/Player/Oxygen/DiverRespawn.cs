using System.Collections;
using UnityEngine;

namespace Deeploration.Player
{
    /// <summary>
    /// Checkpoint del diver: quando l'ossigeno finisce lo riporta all'ultimo punto registrato
    /// (le stazioni O₂ accese lo registrano all'ingresso) con il serbatoio ricaricato.
    /// Va sul PlayerCapsule, accanto a OxygenSystem.
    /// </summary>
    [RequireComponent(typeof(OxygenSystem))]
    public sealed class DiverRespawn : MonoBehaviour
    {
        [Tooltip("Secondi fra l'ossigeno a zero e il respawn.")]
        [SerializeField, Min(0f)] private float respawnDelay = 2f;

        [Tooltip("Frazione del serbatoio al respawn.")]
        [SerializeField, Range(0f, 1f)] private float respawnOxygen = 1f;

        private OxygenSystem oxygen;
        private Transform checkpoint;
        private Vector3 startPosition;
        private Quaternion startRotation;

        public Transform Checkpoint => checkpoint;

        private void Awake()
        {
            oxygen = GetComponent<OxygenSystem>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void OnEnable() => oxygen.OnOxygenDepleted += HandleDepleted;
        private void OnDisable() => oxygen.OnOxygenDepleted -= HandleDepleted;

        public void SetCheckpoint(Transform point) => checkpoint = point;

        private void HandleDepleted() => StartCoroutine(RespawnAfterDelay());

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            Respawn();
        }

        /// <summary>Porta il diver al checkpoint (o al punto di partenza) e ricarica il serbatoio.</summary>
        public void Respawn()
        {
            Vector3 position = checkpoint != null ? checkpoint.position : startPosition;
            Quaternion rotation = checkpoint != null ? Quaternion.Euler(0f, checkpoint.eulerAngles.y, 0f) : startRotation;

            // Il CharacterController sovrascrive la posizione se resta acceso durante il teletrasporto.
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (controller != null) controller.enabled = true;

            DiverController diver = GetComponent<DiverController>();
            if (diver != null) diver.ResetMotion();

            oxygen.SetNormalized(respawnOxygen);
        }
    }
}

using UnityEngine;

namespace Deeploration.Player
{
    [CreateAssetMenu(fileName = "Diver Profile", menuName = "Deeploration/Diver Profile")]
    public class DiverProfile : ScriptableObject
    {
        [Header("Andatura")]
        [Tooltip("Velocità di camminata in m/s. Bassa: lo scafandro è pesante")]
        [Range(0.5f, 4f)]
        public float walkSpeed = 1.6f;

        [Tooltip("Velocità in affanno (sprint) in m/s")]
        [Range(0.5f, 6f)]
        public float sprintSpeed = 2.8f;

        [Header("Inerzia")]
        [Tooltip("Quanto rapidamente il diver raggiunge la velocità target. Valori bassi = massa percepita alta")]
        [Range(0.5f, 12f)]
        public float acceleration = 2.2f;

        [Tooltip("Quanto rapidamente il diver si ferma. Sotto il valore di accelerazione = scivolamento")]
        [Range(0.5f, 12f)]
        public float deceleration = 1.6f;

        [Header("Assetto sul fondale")]
        [Tooltip("Gravità efficace: peso dello scafandro meno la spinta di galleggiamento")]
        [Range(-20f, -1f)]
        public float gravity = -4.5f;

        [Tooltip("Velocità massima di caduta")]
        [Range(1f, 20f)]
        public float terminalVelocity = 6f;

        [Header("Visuale (casco)")]
        [Tooltip("Sensibilità della visuale")]
        [Range(0.1f, 5f)]
        public float lookSensitivity = 1f;

        [Tooltip("Inerzia della visuale: il casco non segue il mouse istantaneamente. Valori bassi = testa più pesante")]
        [Range(1f, 40f)]
        public float lookDamping = 12f;

        [Tooltip("Limite di rotazione verso l'alto in gradi")]
        [Range(0f, 90f)]
        public float lookUpLimit = 70f;

        [Tooltip("Limite di rotazione verso il basso in gradi")]
        [Range(-90f, 0f)]
        public float lookDownLimit = -70f;

        [Tooltip("Inverte l'asse verticale della visuale")]
        public bool invertLookY = false;

        [Header("Oscillazione del passo")]
        [Tooltip("Attiva l'oscillazione della testa a ogni passo")]
        public bool enableHeadBob = true;

        [Tooltip("Metri percorsi per un ciclo completo di passo (destro + sinistro)")]
        [Range(0.5f, 5f)]
        public float bobStrideLength = 1.8f;

        [Tooltip("Ampiezza verticale dell'oscillazione in metri")]
        [Range(0f, 0.3f)]
        public float bobVerticalAmplitude = 0.055f;

        [Tooltip("Ampiezza laterale del dondolio in metri: il peso si sposta da un piede all'altro")]
        [Range(0f, 0.3f)]
        public float bobLateralAmplitude = 0.035f;

        [Tooltip("Inclinazione laterale della testa a ogni passo, in gradi")]
        [Range(0f, 5f)]
        public float bobRollAmplitude = 0.8f;

        [Tooltip("Quanto rapidamente l'oscillazione si smorza da fermo")]
        [Range(1f, 20f)]
        public float bobSettleSpeed = 6f;

        [Header("Consumo ossigeno da attività fisica")]
        [Tooltip("Ossigeno al secondo consumato camminando a velocità piena")]
        [Range(0f, 5f)]
        public float walkOxygenDrain = 0.4f;

        [Tooltip("Ossigeno al secondo consumato in sprint")]
        [Range(0f, 10f)]
        public float sprintOxygenDrain = 1.6f;
    }
}

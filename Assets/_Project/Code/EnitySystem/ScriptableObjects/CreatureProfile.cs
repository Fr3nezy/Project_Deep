using UnityEngine;
using System.Collections.Generic;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// ScriptableObject che definisce tutti i parametri di una creatura.
    /// Permette di creare profili riutilizzabili senza modificare il codice.
    /// </summary>
    [CreateAssetMenu(fileName = "New Creature Profile", menuName = "Deeploration/Creature Profile")]
    public class CreatureProfile : ScriptableObject
    {
        [Header("Identità")]
        [Tooltip("Tipo di entità (usato per catena alimentare)")]
        public EntityType entityType = EntityType.SmallFish;
        
        [Tooltip("Nome identificativo della creatura")]
        public string creatureName = "Generic Fish";

        [Header("Parametri Vitali")]
        [Tooltip("Salute massima (range: 0-100)")]
        [Range(1f, 100f)]
        public float maxHealth = 100f;

        [Tooltip("Stamina massima (range: 0-100)")]
        [Range(1f, 100f)]
        public float maxStamina = 100f;

        [Tooltip("Fame massima (range: 0-100). A 0 = affamato, a 100 = sazio")]
        [Range(1f, 100f)]
        public float maxHunger = 100f;

        [Header("Tasso di Decadimento")]
        [Tooltip("Velocità di aumento della fame per secondo")]
        [Range(0.1f, 10f)]
        public float hungerRate = 1f;

        [Tooltip("Velocità di recupero della stamina per secondo (durante riposo)")]
        [Range(0.5f, 20f)]
        public float staminaRecoveryRate = 5f;

        [Header("Soglie Comportamentali")]
        [Tooltip("Soglia di fame sotto cui l'entità cerca cibo (0-100)")]
        [Range(0f, 100f)]
        public float hungerThreshold = 40f;

        [Tooltip("Soglia di stamina sotto cui l'entità va in riposo (0-100)")]
        [Range(0f, 50f)]
        public float staminaThreshold = 20f;

        [Tooltip("Distanza di rilevamento per prede/predatori")]
        [Range(1f, 50f)]
        public float senseRadius = 15f;

        [Tooltip("Distanza minima da un predatore per attivare la fuga")]
        [Range(1f, 30f)]
        public float fearThreshold = 10f;

        [Header("Parametri di Movimento")]
        [Tooltip("Velocità base di movimento")]
        [Range(0.5f, 10f)]
        public float speedBase = 3f;

        [Tooltip("Velocità aumentata durante fuga/caccia (richiede stamina)")]
        [Range(1f, 20f)]
        public float speedFlee = 8f;

        [Tooltip("Costo di stamina per secondo durante l'accelerazione")]
        [Range(1f, 30f)]
        public float staminaCostPerSecond = 10f;

        [Tooltip("Velocità di rotazione (gradi per secondo)")]
        [Range(30f, 360f)]
        public float rotationSpeed = 120f;

        [Header("Catena Alimentare")]
        [Tooltip("Tipi di entità che questa creatura può cacciare")]
        public List<EntityType> preyTypes = new List<EntityType>();

        [Tooltip("Tipi di entità che cacciano questa creatura")]
        public List<EntityType> predatorTypes = new List<EntityType>();

        [Header("Parametri di Attacco")]
        [Tooltip("Danno inflitto quando attacca una preda")]
        [Range(5f, 100f)]
        public float attackDamage = 20f;

        [Tooltip("Tempo tra attacchi successivi")]
        [Range(0.5f, 10f)]
        public float attackCooldown = 2f;

        [Header("Campo Visivo (FOV) 3D")]
        [Tooltip("Angolo del campo visivo del pesce (gradi)")]
        [Range(60f, 180f)]
        public float fovAngle = 120f;

        [Tooltip("Inclinazione del FOV verso il basso (gradi)")]
        [Range(-30f, 30f)]
        public float fovVerticalOffset = -15f;

        [Tooltip("Rotazione orizzontale del FOV (gradi)")]
        [Range(-45f, 45f)]
        public float fovHorizontalOffset = 0f;

        [Tooltip("Distanza zona di morso (attacco diretto)")]
        [Range(0.5f, 3f)]
        public float biteRange = 1f;

        [Tooltip("Distanza zona di decisione (fuga/caccia)")]
        [Range(2f, 8f)]
        public float decisionRange = 4f;

        [Tooltip("Distanza zona di avvistamento (inspect)")]
        [Range(3f, 15f)]
        public float detectionRange = 5f;

        [Header("Parametri di Steering")]
        [Tooltip("Forza massima di steering per l'evitamento ostacoli")]
        [Range(1f, 20f)]
        public float maxSteerForce = 10f;

        [Tooltip("Distanza massima per rilevare ostacoli (raycast)")]
        [Range(1f, 10f)]
        public float obstacleDetectionDistance = 5f;

        [Header("Movimento Organico (Perlin Noise)")]
        [Tooltip("Altitudine preferita sopra il terreno (Y positivo)")]
        [Range(1f, 20f)]
        public float preferredAltitude = 5f;

        [Tooltip("Intensità del movimento wander (0.5=calmo, 2=nervoso)")]
        [Range(0.1f, 3f)]
        public float wanderStrength = 1f;

        [Tooltip("Frequenza cambio direzione (0.1=lento, 2=veloce)")]
        [Range(0.05f, 2f)]
        public float wanderFrequency = 0.5f;

        [Tooltip("Scala del Perlin Noise (più basso=più fluido)")]
        [Range(0.1f, 1f)]
        public float perlinScale = 0.3f;

        [Tooltip("Stile di movimento preset")]
        public MovementStyle movementStyle = MovementStyle.Active;

        /// <summary>
        /// Valida i parametri del profilo per evitare configurazioni invalide.
        /// </summary>
        private void OnValidate()
        {
            // Assicura che speedFlee sia sempre maggiore di speedBase
            if (speedFlee <= speedBase)
            {
                speedFlee = speedBase + 1f;
                Debug.LogWarning($"[{creatureName}] speedFlee deve essere > speedBase. Corretto automaticamente.");
            }

            // Assicura che hungerThreshold sia sensato
            if (hungerThreshold > maxHunger)
            {
                hungerThreshold = maxHunger * 0.5f;
                Debug.LogWarning($"[{creatureName}] hungerThreshold non può essere > maxHunger. Corretto automaticamente.");
            }
        }
    }
}

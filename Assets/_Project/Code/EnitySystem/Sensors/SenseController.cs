using UnityEngine;
using System.Collections.Generic;
using System;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Rileva entità vicine (prede, predatori, player) usando Physics queries ottimizzate.
    /// Notifica il StateManager quando rileva minacce o opportunità di caccia.
    /// </summary>
    [RequireComponent(typeof(EntityStatus))]
    public class SenseController : MonoBehaviour
    {
        [Header("Configurazione Sensing")]
        [SerializeField, Tooltip("Layer delle entità da rilevare")]
        private LayerMask entityLayer;

        [SerializeField, Tooltip("Intervallo di update per il sensing (secondi). Aumentare per migliorare performance")]
        [Range(0.1f, 1f)]
        private float senseUpdateInterval = 0.3f;

        [SerializeField, Tooltip("Numero massimo di collider da rilevare per query")]
        [Range(5, 50)]
        private int maxDetections = 20;

        [Header("Filtri")]
        [SerializeField, Tooltip("Rileva solo entità in line of sight (senza ostacoli)")]
        private bool requireLineOfSight = true;

        [SerializeField, Tooltip("Layer degli ostacoli per line of sight check")]
        private LayerMask obstacleLayer;

        [Header("Player Detection")]
        [SerializeField, Tooltip("Layer del Player per auto-rilevamento (default Player layer)")]
        private LayerMask playerLayer = 0;

        [SerializeField, Tooltip("Player Transform trovato automaticamente (read-only)")]
        private Transform playerTransform;

        [Header("Debug")]
        [SerializeField, Tooltip("Mostra sfere di debug per entità rilevate")]
        private bool showDebugSpheres = true;

        // Eventi
        public event Action<Transform, EntityType> OnPreyDetected;
        public event Action<Transform, EntityType> OnPredatorDetected;
        public event Action OnThreatsCleared;

        // Componenti
        private EntityStatus entityStatus;
        private Transform cachedTransform;

        // Cache per rilevamento
        private Collider[] detectedColliders;
        private List<Transform> nearbyPrey = new List<Transform>();
        private List<Transform> nearbyPredators = new List<Transform>();
        private float lastSenseTime;

        // Proprietà pubbliche
        public Transform ClosestPrey { get; private set; }
        public Transform ClosestPredator { get; private set; }
        public int PreyCount => nearbyPrey.Count;
        public int PredatorCount => nearbyPredators.Count;
        public bool HasThreats => nearbyPredators.Count > 0;
        public bool HasPreyNearby => nearbyPrey.Count > 0;

        // Proprietà FOV
        public FovZone GetFovZone(Vector3 targetPosition)
        {
            return CalculateFovZone(GetFovOrigin(), targetPosition);
        }

        public Vector3 GetFovDirection()
        {
            return CalculateFovDirection();
        }

        /// <summary>
        /// Restituisce l'origine del FOV (posizione offset per evitare che la mesh blocchi la bite zone).
        /// </summary>
        public Vector3 GetFovOrigin()
        {
            if (entityStatus?.Profile == null) return cachedTransform.position;

            // Applica offset in avanti/indietro sull'asse Z locale
            return cachedTransform.position + cachedTransform.forward * entityStatus.Profile.fovForwardOffset;
        }

        private void Awake()
        {
            entityStatus = GetComponent<EntityStatus>();
            cachedTransform = transform;

            // Pre-alloca array per evitare GC
            detectedColliders = new Collider[maxDetections];

            // Rileva automaticamente Player via layer se configurato
            DetectPlayerTransform();
        }

        private void Start()
        {
            // Primo sensing immediato
            PerformSense();
        }

        private void Update()
        {
            // Update periodico per ottimizzare performance
            if (Time.time - lastSenseTime >= senseUpdateInterval)
            {
                PerformSense();
                lastSenseTime = Time.time;
            }
        }

        /// <summary>
        /// Esegue il rilevamento di entità vicine.
        /// </summary>
        private void PerformSense()
        {
            if (!entityStatus.IsAlive) return;

            // Pulisci liste precedenti
            nearbyPrey.Clear();
            nearbyPredators.Clear();
            ClosestPrey = null;
            ClosestPredator = null;

            // Esegui physics query
            float senseRadius = entityStatus.Profile.senseRadius;
            int hitCount = Physics.OverlapSphereNonAlloc(
                cachedTransform.position,
                senseRadius,
                detectedColliders,
                entityLayer
            );

            // Processa tutti i collider rilevati
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = detectedColliders[i];
                
                // Ignora se stesso
                if (col.transform == cachedTransform) continue;

                // Ottieni EntityStatus del target (anche su parent se collider è su child)
                EntityStatus targetStatus = col.GetComponentInParent<EntityStatus>();
                if (targetStatus == null || !targetStatus.IsAlive) continue;

                // Line of sight check
                if (requireLineOfSight && !HasLineOfSight(col.transform.position))
                {
                    continue;
                }

                // Classifica l'entità
                ClassifyEntity(col.transform, targetStatus);
            }

            // Trova i target più vicini
            UpdateClosestTargets();

            // Rileva Player come predatore esterno
            DetectPlayerAsPredator();

            // Emetti eventi
            EmitSenseEvents();
        }

        /// <summary>
        /// Classifica un'entità come preda o predatore.
        /// </summary>
        private void ClassifyEntity(Transform target, EntityStatus targetStatus)
        {
            EntityType targetType = targetStatus.EntityType;

            // Verifica se è una preda
            if (entityStatus.CanHunt(targetType))
            {
                nearbyPrey.Add(target);
            }

            // Verifica se è un predatore
            if (entityStatus.IsPreyOf(targetType))
            {
                nearbyPredators.Add(target);
            }
        }

        /// <summary>
        /// Aggiorna i riferimenti ai target più vicini.
        /// </summary>
        private void UpdateClosestTargets()
        {
            float closestPreyDistance = float.MaxValue;
            float closestPredatorDistance = float.MaxValue;

            // Trova preda più vicina
            foreach (Transform prey in nearbyPrey)
            {
                if (prey == null) continue;

                float distance = Vector3.Distance(cachedTransform.position, prey.position);
                if (distance < closestPreyDistance)
                {
                    closestPreyDistance = distance;
                    ClosestPrey = prey;
                }
            }

            // Trova predatore più vicino
            foreach (Transform predator in nearbyPredators)
            {
                if (predator == null) continue;

                float distance = Vector3.Distance(cachedTransform.position, predator.position);
                if (distance < closestPredatorDistance)
                {
                    closestPredatorDistance = distance;
                    ClosestPredator = predator;
                }
            }
        }

        /// <summary>
        /// Emette eventi basati sui rilevamenti.
        /// </summary>
        private void EmitSenseEvents()
        {
            // Notifica preda più vicina
            if (ClosestPrey != null)
            {
                EntityStatus preyStatus = ClosestPrey.GetComponent<EntityStatus>();
                if (preyStatus != null)
                {
                    OnPreyDetected?.Invoke(ClosestPrey, preyStatus.EntityType);
                }
            }

            // Notifica predatore più vicino (priorità alta!)
            if (ClosestPredator != null)
            {
                EntityStatus predatorStatus = ClosestPredator.GetComponent<EntityStatus>();
                if (predatorStatus != null)
                {
                    OnPredatorDetected?.Invoke(ClosestPredator, predatorStatus.EntityType);
                }
                else if (ClosestPredator == playerTransform) // Player rilevato come predatore
                {
                    OnPredatorDetected?.Invoke(ClosestPredator, EntityType.None);
                }
            }
            else if (nearbyPredators.Count == 0)
            {
                // Nessuna minaccia, notifica sicurezza
                OnThreatsCleared?.Invoke();
            }
        }

        /// <summary>
        /// Verifica se c'è line of sight verso un target.
        /// </summary>
        private bool HasLineOfSight(Vector3 targetPosition)
        {
            Vector3 fovOrigin = GetFovOrigin();
            Vector3 direction = targetPosition - fovOrigin;
            float distance = direction.magnitude;

            // Raycast verso il target dall'origine FOV offset
            if (Physics.Raycast(fovOrigin, direction.normalized, out RaycastHit hit, distance, obstacleLayer))
            {
                // C'è un ostacolo nel mezzo
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifica se un predatore specifico è entro la distanza di paura.
        /// </summary>
        /// <param name="predator">Transform del predatore</param>
        /// <returns>True se il predatore è troppo vicino</returns>
        public bool IsPredatorTooClose(Transform predator)
        {
            if (predator == null) return false;

            float distance = Vector3.Distance(cachedTransform.position, predator.position);
            return distance <= entityStatus.Profile.fearThreshold;
        }

        /// <summary>
        /// Ottiene tutte le prede rilevate (copia difensiva).
        /// </summary>
        public List<Transform> GetAllPrey()
        {
            return new List<Transform>(nearbyPrey);
        }

        /// <summary>
        /// Ottiene tutti i predatori rilevati (copia difensiva).
        /// </summary>
        public List<Transform> GetAllPredators()
        {
            return new List<Transform>(nearbyPredators);
        }

        /// <summary>
        /// Forza un update immediato del sensing (utile per debug o eventi critici).
        /// </summary>
        public void ForceSenseUpdate()
        {
            PerformSense();
        }

        /// <summary>
        /// Calcola la direzione media di fuga da tutti i predatori vicini.
        /// </summary>
        public Vector3 GetAverageFleeDirection()
        {
            if (nearbyPredators.Count == 0) return Vector3.zero;

            Vector3 fleeDirection = Vector3.zero;

            foreach (Transform predator in nearbyPredators)
            {
                if (predator == null) continue;

                Vector3 directionAway = cachedTransform.position - predator.position;
                float distance = directionAway.magnitude;

                // Peso inversamente proporzionale alla distanza
                if (distance > 0.01f)
                {
                    fleeDirection += directionAway.normalized / distance;
                }
            }

            return fleeDirection.normalized;
        }

        /// <summary>
        /// Trova automaticamente il Player via layer nella scena.
        /// </summary>
        private void DetectPlayerTransform()
        {
            if (playerLayer == 0)
            {
                Debug.LogWarning($"[SenseController] {gameObject.name}: Player Layer non configurato, saltato auto-rilevamento Player.");
                return;
            }

            // Usa una search rapida per trovare Player nella scena (raggio ampio)
            Collider[] tempColliders = new Collider[1]; // Solo bisogno del primo trovato
            int hitCount = Physics.OverlapSphereNonAlloc(cachedTransform.position, 10000f, tempColliders, playerLayer);

            if (hitCount > 0)
            {
                playerTransform = tempColliders[0].transform;
                Debug.Log($"[SenseController] {gameObject.name}: Player trovato automaticamente: {playerTransform.name}");
            }
            else
            {
                Debug.LogWarning($"[SenseController] {gameObject.name}: Nessun Player trovato sul layer configurato.");
            }
        }

        /// <summary>
        /// Rileva Player come predatore o preda basandosi sui profili di entrambi.
        /// </summary>
        private void DetectPlayerAsPredator()
        {
            if (playerTransform == null) return;

            float playerDistance = Vector3.Distance(cachedTransform.position, playerTransform.position);
            if (playerDistance > entityStatus.Profile.senseRadius) return;

            if (requireLineOfSight && !HasLineOfSight(playerTransform.position)) return;

            // Ottieni PlayerStatus per verificare il profilo (anche su parent se collider è su child)
            PlayerStatus playerStatus = playerTransform.GetComponentInParent<PlayerStatus>();
            if (playerStatus == null || !playerStatus.IsAlive)
            {
                return; // Player non ha PlayerStatus o è morto
            }

            EntityType myType = entityStatus.EntityType;

            // Verifica relazione predatore-preda in entrambe le direzioni
            bool playerIsMyPredator = playerStatus.Profile.preyTypes.Contains(myType);
            bool iAmPlayerPredator = playerStatus.Profile.predatorTypes.Contains(myType);
            
            // IMPORTANTE: Verifica anche se IO posso cacciare il Player dal MIO profilo
            bool iCanHuntPlayer = entityStatus.Profile.preyTypes.Contains(EntityType.Player);

            // Determina la relazione finale
            if (playerIsMyPredator && !iCanHuntPlayer)
            {
                // Il Player mi caccia e io NON caccio il Player → Player è predatore
                nearbyPredators.Add(playerTransform);

                if (ClosestPredator == null || playerDistance < Vector3.Distance(cachedTransform.position, ClosestPredator.position))
                {
                    ClosestPredator = playerTransform;
                }
            }
            else if (iAmPlayerPredator || iCanHuntPlayer)
            {
                // Io sono predatore del Player (dal PlayerProfile) O posso cacciare Player (dal mio profile) → Player è preda
                nearbyPrey.Add(playerTransform);

                if (ClosestPrey == null || playerDistance < Vector3.Distance(cachedTransform.position, ClosestPrey.position))
                {
                    ClosestPrey = playerTransform;
                }
            }
            // Altrimenti neutrale (si ignorano)
        }

        /// <summary>
        /// Calcola in quale zona FOV si trova una posizione target.
        /// </summary>
        private FovZone CalculateFovZone(Vector3 sourcePosition, Vector3 targetPosition)
        {
            float distance = Vector3.Distance(sourcePosition, targetPosition);

            // Determina zona base per distanza (priorità alla zona più vicina)
            FovZone distanceZone;
            if (distance <= entityStatus.Profile.biteRange)
                distanceZone = FovZone.Bite1;
            else if (distance <= entityStatus.Profile.decisionRange)
                distanceZone = FovZone.Decision4;
            else if (distance <= entityStatus.Profile.detectionRange)
                distanceZone = FovZone.Detection5;
            else
                return FovZone.None;

            // Verifica se il target è entro l'angolo FOV
            if (!IsInFovCone(targetPosition))
                return FovZone.None;

            // Verifica line of sight per zone più piccole
            if (requireLineOfSight && distanceZone != FovZone.Detection5)
            {
                if (!HasLineOfSight(targetPosition))
                    return FovZone.None;
            }

            return distanceZone;
        }

        /// <summary>
        /// Verifica se una posizione target è entro il cono FOV.
        /// </summary>
        private bool IsInFovCone(Vector3 targetPosition)
        {
            Vector3 fovOrigin = GetFovOrigin();
            Vector3 fovDirection = CalculateFovDirection();
            Vector3 targetDirection = (targetPosition - fovOrigin).normalized;

            float angle = Vector3.Angle(fovDirection, targetDirection);
            return angle <= (entityStatus.Profile.fovAngle / 2f);
        }

        /// <summary>
        /// Calcola la direzione del FOV basata sulla direzione fissa dell'entità e offset configurati.
        /// </summary>
        private Vector3 CalculateFovDirection()
        {
            // Direzione base: sempre la direzione forward dell'entità (fissa)
            Vector3 baseDirection = cachedTransform.forward;

            // Applica rotazioni offset del profilo
            Quaternion fovRotation = Quaternion.Euler(
                entityStatus.Profile.fovVerticalOffset,
                entityStatus.Profile.fovHorizontalOffset,
                0f
            );

            return (fovRotation * baseDirection).normalized;
        }

        #region Debug Visualization

        private void OnDrawGizmos()
        {
            if (!showDebugSpheres || entityStatus == null) return;

            // Disegna prede rilevate (verde)
            Gizmos.color = Color.green;
            foreach (Transform prey in nearbyPrey)
            {
                if (prey != null)
                {
                    Gizmos.DrawWireSphere(prey.position, 0.5f);
                    Gizmos.DrawLine(transform.position, prey.position);
                }
            }

            // Disegna predatori rilevati (rosso)
            Gizmos.color = Color.red;
            foreach (Transform predator in nearbyPredators)
            {
                if (predator != null)
                {
                    Gizmos.DrawWireSphere(predator.position, 0.7f);
                    Gizmos.DrawLine(transform.position, predator.position);
                }
            }

            // Evidenzia target più vicini
            if (ClosestPrey != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(ClosestPrey.position, 0.8f);
            }

            if (ClosestPredator != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
                Gizmos.DrawWireSphere(ClosestPredator.position, 1f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (entityStatus == null) return;

            // Disegna fear threshold
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, entityStatus.Profile.fearThreshold);
        }

        #endregion
    }
}

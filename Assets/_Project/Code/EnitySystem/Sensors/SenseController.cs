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

        private void Awake()
        {
            entityStatus = GetComponent<EntityStatus>();
            cachedTransform = transform;

            // Pre-alloca array per evitare GC
            detectedColliders = new Collider[maxDetections];
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

                // Ottieni EntityStatus del target
                EntityStatus targetStatus = col.GetComponent<EntityStatus>();
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
            Vector3 direction = targetPosition - cachedTransform.position;
            float distance = direction.magnitude;

            // Raycast verso il target
            if (Physics.Raycast(cachedTransform.position, direction.normalized, out RaycastHit hit, distance, obstacleLayer))
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

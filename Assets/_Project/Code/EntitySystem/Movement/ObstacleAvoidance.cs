using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Gestisce l'evitamento degli ostacoli usando raycast conici.
    /// Utilizza multiple direzioni per rilevare ostacoli e calcolare forze di avoidance.
    /// </summary>
    public class ObstacleAvoidance : MonoBehaviour
    {
        [Header("Configurazione Raycast")]
        [SerializeField, Tooltip("Distanza massima per rilevare ostacoli")]
        [Range(1f, 20f)]
        private float detectionDistance = 5f;

        [SerializeField, Tooltip("Numero di raggi da lanciare (più raggi = più preciso ma più costoso)")]
        [Range(3, 9)]
        private int rayCount = 5;

        [SerializeField, Tooltip("Angolo del cono di rilevamento (gradi)")]
        [Range(30f, 120f)]
        private float coneAngle = 60f;

        [SerializeField, Tooltip("Layer mask per gli ostacoli da evitare")]
        private LayerMask obstacleLayer = -1;

        [Header("Parametri di Forza")]
        [SerializeField, Tooltip("Forza massima di avoidance")]
        [Range(1f, 20f)]
        private float maxAvoidanceForce = 10f;

        [SerializeField, Tooltip("Moltiplicatore di forza basato sulla distanza")]
        private AnimationCurve forceCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Debug")]
        [SerializeField, Tooltip("Visualizza i raycast nel Scene view")]
        private bool showDebugRays = true;

        [SerializeField, Tooltip("Colore per raggi che non colpiscono")]
        private Color noHitColor = Color.green;

        [SerializeField, Tooltip("Colore per raggi che colpiscono")]
        private Color hitColor = Color.red;

        // Cache
        private Transform cachedTransform;
        private RaycastHit[] raycastHits;

        private void Awake()
        {
            cachedTransform = transform;
            // Pre-alloca array per evitare GC
            raycastHits = new RaycastHit[rayCount];
        }

        /// <summary>
        /// Calcola la forza di steering per evitare ostacoli davanti all'entità.
        /// </summary>
        /// <param name="velocity">Velocità corrente dell'entità</param>
        /// <returns>Forza di avoidance da applicare</returns>
        public Vector3 CalculateAvoidanceForce(Vector3 velocity)
        {
            if (velocity.magnitude < 0.1f)
            {
                // Se l'entità è quasi ferma, non c'è bisogno di evitare
                return Vector3.zero;
            }

            Vector3 forward = velocity.normalized;
            Vector3 avoidanceForce = Vector3.zero;
            float closestDistance = detectionDistance;

            // Lancia raggi in un pattern conico
            for (int i = 0; i < rayCount; i++)
            {
                Vector3 rayDirection = CalculateRayDirection(forward, i);
                
                if (Physics.Raycast(cachedTransform.position, rayDirection, out RaycastHit hit, detectionDistance, obstacleLayer))
                {
                    // Ostacolo rilevato
                    float normalizedDistance = hit.distance / detectionDistance;
                    float forceMultiplier = forceCurve.Evaluate(normalizedDistance);

                    // Calcola forza di repulsione perpendicolare alla normale dell'ostacolo
                    Vector3 avoidDirection = Vector3.Cross(Vector3.up, hit.normal).normalized;
                    
                    // Se la direzione è troppo piccola, usa una direzione laterale predefinita
                    if (avoidDirection.magnitude < 0.1f)
                    {
                        avoidDirection = Vector3.Cross(forward, Vector3.up).normalized;
                    }

                    // Aggiungi anche componente verso l'alto per creature marine
                    avoidDirection += Vector3.up * 0.3f;

                    avoidanceForce += avoidDirection * maxAvoidanceForce * forceMultiplier;

                    // Traccia l'ostacolo più vicino
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                    }

                    // Debug
                    if (showDebugRays)
                    {
                        Debug.DrawRay(cachedTransform.position, rayDirection * hit.distance, hitColor);
                    }
                }
                else
                {
                    // Nessun ostacolo
                    if (showDebugRays)
                    {
                        Debug.DrawRay(cachedTransform.position, rayDirection * detectionDistance, noHitColor);
                    }
                }
            }

            return avoidanceForce;
        }

        /// <summary>
        /// Calcola la direzione del raggio per il pattern conico.
        /// </summary>
        /// <param name="forward">Direzione forward dell'entità</param>
        /// <param name="rayIndex">Indice del raggio</param>
        /// <returns>Direzione normalizzata del raggio</returns>
        private Vector3 CalculateRayDirection(Vector3 forward, int rayIndex)
        {
            if (rayIndex == 0)
            {
                // Primo raggio è sempre dritto avanti
                return forward;
            }

            // Calcola angolo per questo raggio
            float angleStep = coneAngle / (rayCount - 1);
            float angle = -coneAngle / 2f + angleStep * rayIndex;

            // Ruota il forward vector attorno all'asse Y
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 direction = rotation * forward;

            // Aggiungi variazione verticale per raggi laterali
            if (rayIndex % 2 == 0)
            {
                direction = Quaternion.AngleAxis(angle * 0.3f, cachedTransform.right) * direction;
            }

            return direction.normalized;
        }

        /// <summary>
        /// Verifica se c'è un ostacolo direttamente davanti.
        /// </summary>
        /// <param name="distance">Distanza di check</param>
        /// <returns>True se c'è un ostacolo</returns>
        public bool HasObstacleAhead(float distance)
        {
            Vector3 forward = cachedTransform.forward;
            return Physics.Raycast(cachedTransform.position, forward, distance, obstacleLayer);
        }

        /// <summary>
        /// Trova una direzione libera verso cui muoversi.
        /// </summary>
        /// <param name="preferredDirection">Direzione preferita</param>
        /// <param name="searchRadius">Raggio di ricerca</param>
        /// <returns>Direzione libera più vicina alla preferita</returns>
        public Vector3 FindFreeDirection(Vector3 preferredDirection, float searchRadius = 10f)
        {
            // Cerca in 8 direzioni principali
            Vector3[] directions = new Vector3[]
            {
                preferredDirection,
                Quaternion.Euler(0, 45, 0) * preferredDirection,
                Quaternion.Euler(0, -45, 0) * preferredDirection,
                Quaternion.Euler(0, 90, 0) * preferredDirection,
                Quaternion.Euler(0, -90, 0) * preferredDirection,
                Quaternion.Euler(30, 0, 0) * preferredDirection,  // Su
                Quaternion.Euler(-30, 0, 0) * preferredDirection  // Giù
            };

            foreach (Vector3 dir in directions)
            {
                if (!Physics.Raycast(cachedTransform.position, dir, searchRadius, obstacleLayer))
                {
                    return dir.normalized;
                }
            }

            // Se tutte le direzioni sono bloccate, torna sopra
            return Vector3.up;
        }

        /// <summary>
        /// Calcola una forza per navigare attorno a un ostacolo specifico.
        /// </summary>
        /// <param name="obstaclePosition">Posizione dell'ostacolo</param>
        /// <param name="obstacleRadius">Raggio dell'ostacolo</param>
        /// <returns>Forza di steering per aggirare l'ostacolo</returns>
        public Vector3 SteerAroundObstacle(Vector3 obstaclePosition, float obstacleRadius)
        {
            Vector3 toObstacle = obstaclePosition - cachedTransform.position;
            float distance = toObstacle.magnitude;

            if (distance > obstacleRadius + detectionDistance)
            {
                return Vector3.zero; // Troppo lontano
            }

            // Calcola direzione tangente all'ostacolo
            Vector3 tangent = Vector3.Cross(toObstacle.normalized, Vector3.up);
            
            // Scegli la direzione tangente che è più allineata con il forward
            if (Vector3.Dot(tangent, cachedTransform.forward) < 0)
            {
                tangent = -tangent;
            }

            float forceMagnitude = maxAvoidanceForce * (1f - distance / (obstacleRadius + detectionDistance));
            return tangent * forceMagnitude;
        }

        /// <summary>
        /// Imposta la distanza di detection dinamicamente.
        /// </summary>
        public void SetDetectionDistance(float distance)
        {
            detectionDistance = Mathf.Clamp(distance, 1f, 20f);
        }

        /// <summary>
        /// Imposta la forza massima di avoidance.
        /// </summary>
        public void SetMaxAvoidanceForce(float force)
        {
            maxAvoidanceForce = Mathf.Clamp(force, 1f, 20f);
        }
    }
}

using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Controlla il movimento fisico dell'entità utilizzando steering behaviors.
    /// Gestisce la velocità, l'accelerazione e l'uso della stamina.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(EntityStatus))]
    [RequireComponent(typeof(ObstacleAvoidance))]
    public class MovementController : MonoBehaviour
    {
        [Header("Configurazione Movimento")]
        [SerializeField, Tooltip("Usa interpolazione smoothing per movimenti più fluidi")]
        private bool useSmoothMovement = true;

        [SerializeField, Tooltip("Velocità di smoothing per la rotazione")]
        [Range(1f, 20f)]
        private float rotationSmoothing = 5f;

        [Header("Wander Settings (per Idle)")]
        [SerializeField, Tooltip("Distanza del cerchio di wander")]
        [Range(1f, 10f)]
        private float wanderDistance = 3f;

        [SerializeField, Tooltip("Raggio del cerchio di wander")]
        [Range(0.5f, 5f)]
        private float wanderRadius = 2f;

        [SerializeField, Tooltip("Variazione casuale per frame")]
        [Range(1f, 50f)]
        private float wanderJitter = 10f;

        [Header("Bounds Settings")]
        [SerializeField, Tooltip("Usa bounds per limitare il movimento")]
        private bool useBounds = true;

        [SerializeField, Tooltip("Centro dell'area di movimento")]
        private Vector3 boundsCenter = Vector3.zero;

        [SerializeField, Tooltip("Dimensioni dell'area di movimento")]
        private Vector3 boundsSize = new Vector3(50f, 30f, 50f);

        [Header("Profondità Preferita")]
        [SerializeField, Tooltip("Profondità preferita (Y) per questa creatura")]
        private float preferredDepth = -5f;

        [SerializeField, Tooltip("Tolleranza prima di correggere la profondità")]
        [Range(0.5f, 5f)]
        private float depthTolerance = 2f;

        [Header("Pesi Steering")]
        [SerializeField, Tooltip("Peso per l'obstacle avoidance")]
        [Range(0f, 5f)]
        private float obstacleAvoidanceWeight = 2f;

        [SerializeField, Tooltip("Peso per il mantenimento dei bounds")]
        [Range(0f, 3f)]
        private float boundsWeight = 1.5f;

        [SerializeField, Tooltip("Peso per il mantenimento della profondità")]
        [Range(0f, 2f)]
        private float depthWeight = 0.8f;

        // Componenti
        private Rigidbody rb;
        private EntityStatus entityStatus;
        private ObstacleAvoidance obstacleAvoidance;
        private Transform cachedTransform;

        // Stato movimento
        private Vector3 currentVelocity;
        private Vector3 targetDirection;
        private float currentSpeed;
        private bool isAccelerating;
        private float wanderAngle;

        // Proprietà pubbliche
        public Vector3 CurrentVelocity => currentVelocity;
        public float CurrentSpeed => currentSpeed;
        public bool IsAccelerating => isAccelerating;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            entityStatus = GetComponent<EntityStatus>();
            obstacleAvoidance = GetComponent<ObstacleAvoidance>();
            cachedTransform = transform;

            // Configura Rigidbody per movimento 3D fluido
            rb.useGravity = false;
            rb.linearDamping = 1f;
            rb.angularDamping = 5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // Inizializza wander angle casuale
            wanderAngle = Random.Range(0f, 360f);
        }

        private void FixedUpdate()
        {
            if (!entityStatus.IsAlive)
            {
                // Ferma il movimento se morto
                rb.linearVelocity = Vector3.zero;
                return;
            }

            currentVelocity = rb.linearVelocity;
        }

        /// <summary>
        /// Muove l'entità verso un target usando steering behaviors.
        /// </summary>
        /// <param name="targetPosition">Posizione del target</param>
        /// <param name="useAcceleration">Se true, usa speedFlee e consuma stamina</param>
        public void MoveTowards(Vector3 targetPosition, bool useAcceleration = false)
        {
            if (!entityStatus.IsAlive) return;

            CreatureProfile profile = entityStatus.Profile;
            float desiredSpeed = profile.speedBase;
            isAccelerating = false;

            // Tenta di usare accelerazione se richiesto
            if (useAcceleration)
            {
                if (entityStatus.TryUseStamina(profile.staminaCostPerSecond, Time.fixedDeltaTime))
                {
                    desiredSpeed = profile.speedFlee;
                    isAccelerating = true;
                }
                else
                {
                    // Stamina esaurita, usa velocità base
                    desiredSpeed = profile.speedBase;
                }
            }

            // Calcola forza di steering principale (Seek)
            Vector3 seekForce = SteeringBehaviors.Seek(
                cachedTransform.position,
                currentVelocity,
                targetPosition,
                desiredSpeed,
                profile.maxSteerForce
            );

            // Applica movimento con forze combinate
            ApplySteeringForces(seekForce, desiredSpeed);
        }

        /// <summary>
        /// Allontana l'entità da un target (Flee).
        /// </summary>
        /// <param name="threatPosition">Posizione della minaccia</param>
        /// <param name="useAcceleration">Se true, usa speedFlee e consuma stamina</param>
        public void FleeFrom(Vector3 threatPosition, bool useAcceleration = true)
        {
            if (!entityStatus.IsAlive) return;

            CreatureProfile profile = entityStatus.Profile;
            float desiredSpeed = profile.speedBase;
            isAccelerating = false;

            // Tenta di usare accelerazione
            if (useAcceleration)
            {
                if (entityStatus.TryUseStamina(profile.staminaCostPerSecond, Time.fixedDeltaTime))
                {
                    desiredSpeed = profile.speedFlee;
                    isAccelerating = true;
                }
            }

            // Calcola forza di steering (Flee)
            Vector3 fleeForce = SteeringBehaviors.Flee(
                cachedTransform.position,
                currentVelocity,
                threatPosition,
                desiredSpeed,
                profile.maxSteerForce
            );

            ApplySteeringForces(fleeForce, desiredSpeed);
        }

        /// <summary>
        /// Movimento di vagabondaggio casuale (Wander).
        /// </summary>
        public void Wander()
        {
            if (!entityStatus.IsAlive) return;

            CreatureProfile profile = entityStatus.Profile;

            // Calcola forza di wander
            Vector3 wanderForce = SteeringBehaviors.Wander(
                currentVelocity.magnitude > 0.1f ? currentVelocity : cachedTransform.forward,
                ref wanderAngle,
                wanderDistance,
                wanderRadius,
                wanderJitter,
                profile.maxSteerForce
            );

            ApplySteeringForces(wanderForce, profile.speedBase);
        }

        /// <summary>
        /// Riposo - l'entità si muove lentamente e recupera stamina.
        /// </summary>
        public void Rest()
        {
            if (!entityStatus.IsAlive) return;

            // Recupera stamina durante il riposo
            entityStatus.RecoverStamina(Time.fixedDeltaTime);

            // Movimento minimo con wander molto lento
            CreatureProfile profile = entityStatus.Profile;
            float restSpeed = profile.speedBase * 0.3f; // 30% della velocità base

            Vector3 wanderForce = SteeringBehaviors.Wander(
                currentVelocity.magnitude > 0.1f ? currentVelocity : cachedTransform.forward,
                ref wanderAngle,
                wanderDistance * 0.5f,
                wanderRadius * 0.5f,
                wanderJitter * 0.3f,
                profile.maxSteerForce * 0.3f
            );

            ApplySteeringForces(wanderForce, restSpeed);
        }

        /// <summary>
        /// Applica le forze di steering combinate con obstacle avoidance.
        /// </summary>
        private void ApplySteeringForces(Vector3 primaryForce, float desiredSpeed)
        {
            CreatureProfile profile = entityStatus.Profile;

            // Calcola forze secondarie
            Vector3 avoidanceForce = obstacleAvoidance.CalculateAvoidanceForce(currentVelocity);
            Vector3 boundsForce = Vector3.zero;
            Vector3 depthForce = Vector3.zero;

            if (useBounds)
            {
                boundsForce = SteeringBehaviors.StayInBounds(
                    cachedTransform.position,
                    boundsCenter,
                    boundsSize,
                    profile.maxSteerForce
                );
            }

            // Mantenimento profondità
            float depthCorrection = SteeringBehaviors.MaintainDepth(
                cachedTransform.position.y,
                preferredDepth,
                depthTolerance,
                profile.maxSteerForce
            );
            depthForce = Vector3.up * depthCorrection;

            // Combina tutte le forze con pesi
            var forces = new (Vector3 force, float weight)[]
            {
                (primaryForce, 1f),
                (avoidanceForce, obstacleAvoidanceWeight),
                (boundsForce, boundsWeight),
                (depthForce, depthWeight)
            };

            Vector3 totalForce = SteeringBehaviors.CombineForces(forces, profile.maxSteerForce);

            // Applica forza al Rigidbody
            if (totalForce.magnitude > 0.01f)
            {
                rb.AddForce(totalForce, ForceMode.Acceleration);
            }

            // Limita la velocità massima
            if (rb.linearVelocity.magnitude > desiredSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * desiredSpeed;
            }

            currentSpeed = rb.linearVelocity.magnitude;

            // Ruota verso la direzione di movimento
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                RotateTowards(rb.linearVelocity.normalized);
            }
        }

        /// <summary>
        /// Ruota l'entità verso una direzione specifica.
        /// </summary>
        private void RotateTowards(Vector3 direction)
        {
            if (direction.magnitude < 0.01f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            if (useSmoothMovement)
            {
                cachedTransform.rotation = Quaternion.Slerp(
                    cachedTransform.rotation,
                    targetRotation,
                    entityStatus.Profile.rotationSpeed * Time.fixedDeltaTime / rotationSmoothing
                );
            }
            else
            {
                cachedTransform.rotation = Quaternion.RotateTowards(
                    cachedTransform.rotation,
                    targetRotation,
                    entityStatus.Profile.rotationSpeed * Time.fixedDeltaTime
                );
            }
        }

        /// <summary>
        /// Ferma completamente il movimento.
        /// </summary>
        public void Stop()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            currentSpeed = 0f;
            isAccelerating = false;
        }

        /// <summary>
        /// Imposta la profondità preferita.
        /// </summary>
        public void SetPreferredDepth(float depth)
        {
            preferredDepth = depth;
        }

        /// <summary>
        /// Imposta i bounds di movimento.
        /// </summary>
        public void SetBounds(Vector3 center, Vector3 size)
        {
            boundsCenter = center;
            boundsSize = size;
        }

        #region Debug Visualization

        private void OnDrawGizmosSelected()
        {
            if (useBounds)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
                Gizmos.DrawWireCube(boundsCenter, boundsSize);
            }

            // Mostra profondità preferita
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
            Vector3 depthIndicator = transform.position;
            depthIndicator.y = preferredDepth;
            Gizmos.DrawWireSphere(depthIndicator, 0.5f);
        }

        #endregion
    }
}

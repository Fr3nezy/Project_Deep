using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Una creatura del pool: percezione del sub, macchina a stati e moto cinematico (niente Rigidbody, niente
    /// collider). Non ha un Update suo: la fa avanzare il <see cref="CreatureManager"/>, che decide in base al
    /// livello di dettaglio quando ragiona (<c>think</c>) e quando lancia i raggi (<c>probe</c>). Il movimento
    /// invece avanza a ogni frame ed è solo matematica sui valori dell'ultimo ragionamento e degli ultimi raggi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Creature : MonoBehaviour
    {
        // Inclinazione massima della rotta: i pesci nuotano quasi in piano anche quando cambiano quota.
        private const float MaxClimb = 0.5f;
        private const float FloorProbeDistance = 100f;

        // Buffer condiviso da tutte le creature: il manager le fa avanzare una alla volta sul main thread.
        private static readonly Collider[] OverlapBuffer = new Collider[4];

        [Tooltip("Animator con la clip di nuoto del modello provvisorio (opzionale). La clip accelera e rallenta " +
                 "con la velocità della creatura. Verrà sostituito dalla catena di joint procedurale.")]
        [SerializeField] private Animator swimAnimator;

        [Tooltip("Velocità della clip di nuoto alla velocità di crociera; scala in proporzione alla velocità attuale.")]
        [SerializeField, Min(0f)] private float swimAnimationSpeed = 1f;

        private CreatureManager manager;
        private CreatureProfile profile;
        private Transform cachedTransform;

        private Vector3 home;
        private float leashRadius;
        private float ceilingY;
        private float noiseSeed;

        private Vector3 heading;
        private float speed;
        private CreatureState state;
        private float lastPerceivedTime;
        private Vector3 threatPosition;
        private StimulusKind perceived;

        // Risultati degli ultimi raggi, riusati finché il livello di dettaglio non ne lancia altri.
        private float floorY = float.NaN;
        private Vector3 avoidDirection;
        private float avoidWeight;

        /// <summary>Profilo con cui la creatura è stata presa dal pool.</summary>
        public CreatureProfile Profile => profile;

        /// <summary>Comportamento attivo.</summary>
        public CreatureState State => state;

        /// <summary>Livello di dettaglio assegnato dal manager nell'ultimo frame.</summary>
        public CreatureLod Lod { get; internal set; }

        /// <summary>Tipo dello stimolo percepito nell'ultimo ragionamento; None se nessuno.</summary>
        public StimulusKind Perceived => perceived;

        /// <summary>Velocità attuale (m/s) lungo la rotta.</summary>
        public float Speed => speed;

        /// <summary>Indice nella lista delle creature attive del manager; -1 se è nel pool.</summary>
        internal int ActiveIndex { get; set; } = -1;

        /// <summary>Numero progressivo usato per sfasare i ragionamenti delle creature nei frame.</summary>
        internal int Slot { get; set; }

        /// <summary>Prepara la creatura appena uscita dal pool.</summary>
        internal void OnSpawn(CreatureManager owner, CreatureProfile creatureProfile, Vector3 homePosition, float leash,
            float ceiling)
        {
            manager = owner;
            profile = creatureProfile;
            cachedTransform = transform;
            home = homePosition;
            leashRadius = leash;
            ceilingY = ceiling;
            noiseSeed = Random.value * 1000f;

            heading = cachedTransform.forward;
            speed = profile.cruiseSpeed * 0.5f;
            state = CreatureState.Wander;
            lastPerceivedTime = float.NegativeInfinity;
            perceived = StimulusKind.None;
            floorY = float.NaN;
            avoidWeight = 0f;
            Lod = CreatureLod.Near;
        }

        /// <summary>
        /// Un passo di simulazione. <paramref name="think"/>: aggiorna percezione e stato;
        /// <paramref name="probe"/>: lancia i raggi di ostacoli e fondale.
        /// </summary>
        internal void Tick(float deltaTime, bool think, bool probe)
        {
            if (think) Think();
            if (probe) Probe();
            Move(deltaTime);
        }

        private void Think()
        {
            perceived = Perceive(cachedTransform.position, out Vector3 source);

            // Reazione provvisoria (task 2 di M2): qualunque stimolo percepito fa scappare. Le reazioni per tipo
            // e intensità (avvicinarsi alla luce debole, fuggire dal flash) arrivano con la task 4.
            if (perceived != StimulusKind.None)
            {
                lastPerceivedTime = Time.time;
                threatPosition = source;
                state = CreatureState.Flee;
            }
            else if (state == CreatureState.Flee && Time.time - lastPerceivedTime > profile.calmDelay)
            {
                state = CreatureState.Wander;
            }
        }

        /// <summary>
        /// Sceglie fra gli stimoli del frame quello percepito più forte, cioè il più vicino in proporzione alla
        /// sua portata (scalata dalla sensibilità della specie). Una luce conta solo se la creatura è nel fascio
        /// o nell'alone e se fra la sorgente e la creatura non ci sono ostacoli; il rumore passa attraverso tutto.
        /// Il raggio di linea di vista parte solo quando i controlli economici sono passati.
        /// </summary>
        private StimulusKind Perceive(Vector3 position, out Vector3 source)
        {
            source = default;
            StimulusKind best = StimulusKind.None;
            float bestScore = float.MaxValue;
            IReadOnlyList<CreatureStimulus> stimuli = manager.Stimuli;

            for (int i = 0; i < stimuli.Count; i++)
            {
                CreatureStimulus stimulus = stimuli[i];
                float sensitivity = stimulus.Kind == StimulusKind.Light
                    ? profile.lightSensitivity
                    : profile.hearingSensitivity;
                float reach = stimulus.Radius * sensitivity;
                if (reach <= 0f) continue;

                Vector3 fromSource = position - stimulus.Position;
                float sqrDistance = fromSource.sqrMagnitude;
                if (sqrDistance > reach * reach) continue;

                float distance = Mathf.Sqrt(sqrDistance);
                float score = distance / reach;
                if (score >= bestScore) continue;

                if (stimulus.Kind == StimulusKind.Light && distance > 0.01f)
                {
                    Vector3 direction = fromSource / distance;
                    bool inHalo = distance <= stimulus.HaloRadius * sensitivity;
                    if (!inHalo && Vector3.Dot(stimulus.Direction, direction) < stimulus.ConeCos) continue;
                    if (Physics.Raycast(stimulus.Position, direction, distance - profile.bodyRadius,
                            manager.ObstacleMask, QueryTriggerInteraction.Ignore))
                        continue;
                }

                best = stimulus.Kind;
                bestScore = score;
                source = stimulus.Position;
            }

            return best;
        }

        private void Probe()
        {
            Vector3 position = cachedTransform.position;
            LayerMask mask = manager.ObstacleMask;

            if (Physics.Raycast(position, Vector3.down, out RaycastHit floorHit, FloorProbeDistance, mask,
                    QueryTriggerInteraction.Ignore))
            {
                floorY = floorHit.point.y;
            }
            else if (Physics.Raycast(position + Vector3.up * FloorProbeDistance, Vector3.down, out floorHit,
                         FloorProbeDistance, mask, QueryTriggerInteraction.Ignore))
            {
                // Paracadute: niente fondale sotto ma una superficie sopra, quindi la creatura è finita sotto il
                // terreno. La riporta all'altezza preferita sopra la prima superficie vista dall'alto.
                floorY = floorHit.point.y;
                position.y = floorY + profile.heightAboveFloor;
                cachedTransform.position = position;
            }

            // Lo SphereCast ignora i collider in cui parte già immerso: se il corpo è dentro un ostacolo (per
            // esempio nell'involucro di un collider convesso) esce allontanandosi dal suo centro, verso l'alto.
            int overlaps = Physics.OverlapSphereNonAlloc(position, profile.bodyRadius, OverlapBuffer, mask,
                QueryTriggerInteraction.Ignore);
            if (overlaps > 0)
            {
                Vector3 away = position - OverlapBuffer[0].bounds.center;
                away.y = Mathf.Max(away.y, 0f);
                avoidDirection = (away.normalized + Vector3.up).normalized;
                avoidWeight = 1f;
                return;
            }

            float lookDistance = profile.bodyRadius + Mathf.Max(1f, speed * profile.lookAhead);
            if (Physics.SphereCast(position, profile.bodyRadius, heading, out RaycastHit hit, lookDistance, mask,
                    QueryTriggerInteraction.Ignore))
            {
                // Scivola lungo la superficie e se ne stacca un po', solo in orizzontale: la quota la decide
                // HeightCorrection, così un pendio sopra il soffitto diventa una parete da aggirare invece di una
                // rampa. Con l'ostacolo di fronte la proiezione è quasi nulla, allora gira di lato.
                Vector3 normal = new Vector3(hit.normal.x, 0f, hit.normal.z);
                Vector3 slide = Vector3.ProjectOnPlane(new Vector3(heading.x, 0f, heading.z), normal);
                if (slide.sqrMagnitude < 0.01f) slide = Vector3.Cross(Vector3.up, normal);
                avoidDirection = slide.normalized + normal.normalized * 0.5f;
                if (avoidDirection.sqrMagnitude < 0.0001f) avoidDirection = new Vector3(heading.x, 0f, heading.z);
                avoidDirection.Normalize();
                avoidWeight = 1f - hit.distance / lookDistance;
            }
            else
            {
                avoidWeight = 0f;
            }
        }

        private void Move(float deltaTime)
        {
            Vector3 position = cachedTransform.position;
            float targetSpeed;
            Vector3 desired;

            if (state == CreatureState.Flee)
            {
                targetSpeed = profile.fleeSpeed;
                desired = position - threatPosition;
            }
            else
            {
                targetSpeed = profile.cruiseSpeed;
                float noise = Mathf.PerlinNoise(noiseSeed, Time.time * profile.wanderFrequency) * 2f - 1f;
                desired = Quaternion.AngleAxis(noise * profile.wanderTurn * deltaTime, Vector3.up) * heading;
                desired = PullTowardsHome(position, desired);
            }

            desired.y = 0f;
            if (desired.sqrMagnitude < 0.0001f) desired = new Vector3(heading.x, 0f, heading.z);
            desired.Normalize();
            desired.y = Mathf.Clamp(HeightCorrection(position.y), -MaxClimb, MaxClimb);

            // Vicino a un ostacolo rallenta e vira più forte, così non ci entra mentre gira.
            float turnRate = profile.turnRate;
            if (avoidWeight > 0f)
            {
                // Lo scarto agisce sul piano; la quota resta quella decisa da HeightCorrection, più la spinta
                // verso l'alto del recupero quando la creatura è dentro un ostacolo.
                float climb = desired.y;
                desired = Vector3.Lerp(new Vector3(desired.x, 0f, desired.z), avoidDirection, avoidWeight);
                desired.y = climb + avoidDirection.y * avoidWeight;
                targetSpeed *= 1f - 0.6f * avoidWeight;
                turnRate *= 1f + 2f * avoidWeight;
            }

            heading = Vector3.RotateTowards(heading, desired.normalized, turnRate * Mathf.Deg2Rad * deltaTime, 0f);
            heading.y = Mathf.Clamp(heading.y, -MaxClimb, MaxClimb);
            heading.Normalize();
            speed = Mathf.MoveTowards(speed, targetSpeed, profile.acceleration * deltaTime);

            position += heading * (speed * deltaTime);
            if (!float.IsNaN(floorY)) position.y = Mathf.Max(position.y, floorY + profile.bodyRadius);
            cachedTransform.SetPositionAndRotation(position, Quaternion.LookRotation(heading));

            if (swimAnimator != null)
                swimAnimator.speed = swimAnimationSpeed * Mathf.Max(0.3f, speed / profile.cruiseSpeed);
        }

        /// <summary>Oltre il guinzaglio la rotta piega verso casa, tanto più quanto più si è lontani.</summary>
        private Vector3 PullTowardsHome(Vector3 position, Vector3 desired)
        {
            Vector3 toHome = home - position;
            toHome.y = 0f;
            float distance = toHome.magnitude;
            if (distance <= leashRadius) return desired;

            float pull = Mathf.Clamp01((distance - leashRadius) / (leashRadius * 0.5f + 0.01f));
            return Vector3.Lerp(desired.normalized, toHome / distance, pull);
        }

        /// <summary>
        /// Componente verticale della rotta: resta all'altezza preferita sopra il fondale, ma mai sopra il soffitto
        /// del nido. Sopra il soffitto scende sempre, anche dentro la tolleranza.
        /// </summary>
        private float HeightCorrection(float y)
        {
            float target = float.IsNaN(floorY) ? y : floorY + profile.heightAboveFloor;
            target = Mathf.Min(target, ceilingY);
            float error = target - y;
            if (y <= ceilingY && Mathf.Abs(error) <= profile.heightTolerance) return 0f;
            return error * 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            if (profile == null) return;
            Vector3 position = transform.position;

            Gizmos.color = state == CreatureState.Flee ? Color.red : Color.cyan;
            Gizmos.DrawLine(position, position + heading * Mathf.Max(1f, speed));
            if (state == CreatureState.Flee)
            {
                Gizmos.color = perceived == StimulusKind.Light ? Color.yellow : new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(position, threatPosition);
            }
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(home, leashRadius);
            if (avoidWeight > 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(position, position + avoidDirection * 2f);
            }
        }
    }
}

using System;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Predatore stealth (viperfish della chiusa 2): vede le luci del sub solo nel suo cono visivo e con la linea di
    /// vista libera, e decide un livello d'allerta (<see cref="StealthAlert"/>). In Neutrale lascia ragionare la
    /// <see cref="Creature"/> da sola (vaga attorno al nido, caccia); in Indaga e Attacco, e mentre si allontana dopo
    /// aver perso interesse, la guida con <see cref="Creature.Guide"/>. <see cref="Creature.Tick"/> chiama
    /// <see cref="Step"/> a ogni frame, con il flag di ragionamento del LOD del manager.
    /// Spec: docs/specs/ViperfishStealth.md. Attacco provvisorio: insegue il casco senza carica né morso (passo 4).
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Creature))]
    public sealed class CreatureStealth : MonoBehaviour
    {
        [Header("Vista")]
        [Tooltip("Semi-angolo del cono visivo attorno al muso (°).")]
        [SerializeField, Range(5f, 180f)] private float viewHalfAngle = 60f;
        [Tooltip("Portata massima a cui vede una luce del sub (m); sotto vale la portata dello stimolo.")]
        [SerializeField, Min(0f)] private float maxSightRange = 25f;
        [Tooltip("Portata a cui vede il corpo del sub muoversi (m).")]
        [SerializeField, Min(0f)] private float motionSightRange = 12f;
        [Tooltip("Velocità del sub (m/s) oltre la quale conta come movimento.")]
        [SerializeField, Min(0f)] private float motionThreshold = 0.3f;

        [Header("Indagine")]
        [Tooltip("Secondi senza stimoli validi dopo cui perde interesse e torna Neutrale.")]
        [SerializeField, Min(0f)] private float giveUpTime = 5f;
        [Tooltip("Distanza (m) a cui gira attorno al punto indagato.")]
        [SerializeField, Min(0.5f)] private float inspectDistance = 2f;
        [Tooltip("Velocità (m/s) verso il punto indagato.")]
        [SerializeField, Min(0.1f)] private float investigateSpeed = 1.8f;
        [Tooltip("Distanza minima (m) dal punto indagato a cui si allontana dopo aver perso interesse.")]
        [SerializeField, Min(0f)] private float leaveDistance = 10f;

        [Header("Attacco")]
        [Tooltip("Velocità (m/s) verso il sub in Attacco.")]
        [SerializeField, Min(0.1f)] private float approachSpeed = 3.5f;
        [Tooltip("Secondi di Attacco senza riuscire a caricare dopo cui torna a indagare.")]
        [SerializeField, Min(0f)] private float attackGiveUpTime = 8f;
        [Tooltip("Altezza del casco sopra i piedi del sub (m).")]
        [SerializeField, Min(0f)] private float helmetHeight = 1.3f;

        // Allontanamento: durata massima; distanza orizzontale a cui il punto è raggiunto.
        private const float LeaveMaxTime = 15f;
        private const float LeaveArriveDistance = 1.5f;
        // Il raggio di vista si ferma prima della sorgente: la cella in mano è sul layer Default (obstacleMask).
        private const float SightRayMargin = 0.5f;
        // Punto del corpo del sub controllato per il movimento (m sopra i piedi).
        private const float TorsoHeight = 1f;
        // Passo angolare del giro attorno al punto indagato (°).
        private const float OrbitStep = 40f;
        // Quota del giro sopra il punto indagato (m).
        private const float InspectLift = 0.5f;

        private Creature creature;
        private CreatureManager manager;
        private Transform cachedTransform;
        private Vector3 home;
        private float leashRadius;

        private StealthAlert alert;
        private Vector3 lastSeen;
        private float lastStimulusTime;
        private float attackStartTime;
        private bool leaving;
        private Vector3 leavePoint;
        private float leaveStartTime;
        private float orbitSign = 1f;

        // Movimento del sub: ultimo campione e velocità misurata sul tempo reale fra due ragionamenti.
        private bool hasDiverSample;
        private Vector3 lastDiverPosition;
        private float lastDiverSampleTime;
        private float diverSpeed;

        /// <summary>Livello d'allerta attuale.</summary>
        public StealthAlert Alert => alert;

        /// <summary>Ultimo punto in cui ha visto una luce del sub (il punto indagato).</summary>
        public Vector3 LastSeenPoint => lastSeen;

        /// <summary>A ogni cambio di livello d'allerta.</summary>
        public event Action<StealthAlert> OnAlertChanged;

        /// <summary>Prepara il driver alla creatura appena uscita dal pool.</summary>
        internal void Begin(Creature owner, CreatureManager creatureManager, Vector3 homePosition, float leash)
        {
            creature = owner;
            manager = creatureManager;
            cachedTransform = transform;
            home = homePosition;
            leashRadius = leash;
            alert = StealthAlert.Neutral;
            lastSeen = homePosition;
            lastStimulusTime = float.NegativeInfinity;
            attackStartTime = 0f;
            leaving = false;
            orbitSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            hasDiverSample = false;
            diverSpeed = 0f;
        }

        /// <summary>Un passo: percezione e scelte nei frame di ragionamento, guida del nuoto a ogni frame.</summary>
        internal void Step(float deltaTime, bool think)
        {
            if (creature == null || manager == null) return;
            float now = Time.time;
            if (think) Think(now);

            switch (alert)
            {
                case StealthAlert.Investigate:
                    creature.Guide(InvestigateTarget(), investigateSpeed, CreatureState.Stalk, 0f, lastSeen);
                    break;
                case StealthAlert.Attack:
                    // shortcut: insegue il casco; avvicinamento frontale, carica e morso arrivano col passo 4 della spec.
                    Vector3 helmet = manager.PlayerPosition + Vector3.up * helmetHeight;
                    creature.Guide(helmet, approachSpeed, CreatureState.Stalk, 1f, helmet);
                    break;
                default:
                    if (!leaving) break;
                    Vector3 toLeave = leavePoint - cachedTransform.position;
                    toLeave.y = 0f;
                    if (toLeave.sqrMagnitude < LeaveArriveDistance * LeaveArriveDistance ||
                        now - leaveStartTime > LeaveMaxTime)
                    {
                        leaving = false;
                        creature.Release();
                    }
                    else creature.Guide(leavePoint, creature.Profile.cruiseSpeed, CreatureState.Wander, 0f, null);
                    break;
            }
        }

        private void Think(float now)
        {
            SampleDiver(now);
            if (TrySeeLight(out Vector3 point))
            {
                lastSeen = point;
                lastStimulusTime = now;
                if (alert == StealthAlert.Neutral) SetAlert(StealthAlert.Investigate);
            }

            if (alert == StealthAlert.Investigate)
            {
                if (DiverMovingInSight())
                {
                    SetAlert(StealthAlert.Attack);
                    attackStartTime = now;
                }
                else if (now - lastStimulusTime > giveUpTime) StartLeaving(now);
            }
            else if (alert == StealthAlert.Attack)
            {
                float far = motionSightRange * 2f;
                if (!manager.HasPlayer ||
                    (manager.PlayerPosition - cachedTransform.position).sqrMagnitude > far * far)
                    StartLeaving(now);
                else if (now - attackStartTime > attackGiveUpTime)
                {
                    lastSeen = manager.PlayerPosition;
                    lastStimulusTime = now;
                    SetAlert(StealthAlert.Investigate);
                }
            }
        }

        private void SetAlert(StealthAlert next)
        {
            if (next == alert) return;
            alert = next;
            leaving = false;
            OnAlertChanged?.Invoke(next);
        }

        /// <summary>
        /// La luce del sub più vicina che vede: sorgente nel cono, entro la portata dello stimolo (tagliata a
        /// <c>maxSightRange</c>) e con la linea di vista libera. Il rumore non conta.
        /// </summary>
        private bool TrySeeLight(out Vector3 point)
        {
            point = default;
            var stimuli = manager.Stimuli;
            CreatureProfile profile = creature.Profile;
            Vector3 eye = Eye();
            Vector3 forward = cachedTransform.forward;
            float minCos = Mathf.Cos(viewHalfAngle * Mathf.Deg2Rad);
            float best = float.PositiveInfinity;
            for (int i = 0; i < stimuli.Count; i++)
            {
                CreatureStimulus stimulus = stimuli[i];
                if (!stimulus.FromPlayer || !stimulus.IsLight) continue;
                if (stimulus.OnlySpecies != null && !ReferenceEquals(stimulus.OnlySpecies, profile)) continue;
                Vector3 toSource = stimulus.Position - eye;
                float distance = toSource.magnitude;
                float reach = Mathf.Min(stimulus.Radius * profile.lightSensitivity, maxSightRange);
                if (distance > reach || distance >= best) continue;
                if (!InView(forward, toSource, distance, minCos) || !LineClear(eye, toSource, distance)) continue;
                best = distance;
                point = stimulus.Position;
            }
            return best < float.PositiveInfinity;
        }

        /// <summary>True se il corpo del sub è nel cono, entro <c>motionSightRange</c>, a vista e si muove.</summary>
        private bool DiverMovingInSight()
        {
            if (!manager.HasPlayer || diverSpeed <= motionThreshold) return false;
            Vector3 eye = Eye();
            Vector3 toTorso = manager.PlayerPosition + Vector3.up * TorsoHeight - eye;
            float distance = toTorso.magnitude;
            return distance <= motionSightRange &&
                   InView(cachedTransform.forward, toTorso, distance, Mathf.Cos(viewHalfAngle * Mathf.Deg2Rad)) &&
                   LineClear(eye, toTorso, distance);
        }

        // Posizione dei piedi del sub, non la camera: girare la camera non è movimento. Tempo reale fra due
        // ragionamenti, così il LOD (un ragionamento ogni 4 o 16 frame) non inventa movimento.
        private void SampleDiver(float now)
        {
            if (!manager.HasPlayer)
            {
                hasDiverSample = false;
                diverSpeed = 0f;
                return;
            }
            Vector3 position = manager.PlayerPosition;
            diverSpeed = hasDiverSample && now > lastDiverSampleTime
                ? (position - lastDiverPosition).magnitude / (now - lastDiverSampleTime)
                : 0f;
            lastDiverPosition = position;
            lastDiverSampleTime = now;
            hasDiverSample = true;
        }

        /// <summary>
        /// Perde interesse: torna Neutrale e nuota verso un punto a <c>leaveDistance</c> dal punto indagato, verso
        /// casa (oltre casa se il punto le è vicino), dentro il guinzaglio del nido.
        /// </summary>
        private void StartLeaving(float now)
        {
            SetAlert(StealthAlert.Neutral);
            Vector3 direction = home - lastSeen;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1f)
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle.normalized;
                direction = new Vector3(random.x, 0f, random.y);
            }
            direction.Normalize();
            Vector3 point = lastSeen + direction * leaveDistance;
            Vector3 fromHome = point - home;
            fromHome.y = 0f;
            if (fromHome.magnitude > leashRadius) point = home + fromHome.normalized * (leashRadius * 0.8f);
            point.y = home.y;
            leavePoint = point;
            leaving = true;
            leaveStartTime = now;
        }

        /// <summary>
        /// Punto verso cui nuotare in Indaga: lontano, il punto a <c>inspectDistance</c> dal punto indagato sulla sua
        /// strada; vicino, un passo del giro attorno al punto.
        /// </summary>
        private Vector3 InvestigateTarget()
        {
            Vector3 away = cachedTransform.position - lastSeen;
            away.y = 0f;
            float distance = away.magnitude;
            Vector3 direction = distance > 0.01f ? away / distance : -cachedTransform.forward;
            if (distance <= inspectDistance + 1f)
                direction = Quaternion.AngleAxis(orbitSign * OrbitStep, Vector3.up) * direction;
            return lastSeen + direction * inspectDistance + Vector3.up * InspectLift;
        }

        private Vector3 Eye() => cachedTransform.position + cachedTransform.forward * creature.Profile.bodyRadius;

        private static bool InView(Vector3 forward, Vector3 toTarget, float distance, float minCos) =>
            distance < 1e-3f || Vector3.Dot(forward, toTarget) / distance >= minCos;

        private bool LineClear(Vector3 eye, Vector3 toTarget, float distance) =>
            distance <= SightRayMargin || !Physics.Raycast(eye, toTarget / distance, distance - SightRayMargin,
                manager.ObstacleMask, QueryTriggerInteraction.Ignore);

        private void OnDrawGizmosSelected()
        {
            Transform t = transform;
            Gizmos.color = alert == StealthAlert.Attack ? Color.red
                : alert == StealthAlert.Investigate ? new Color(1f, 0.65f, 0f) : new Color(0.2f, 0.5f, 1f);
            Vector3 origin = t.position;
            foreach (Vector3 axis in new[] { t.up, -t.up, t.right, -t.right })
            {
                Vector3 edge = Quaternion.AngleAxis(viewHalfAngle, Vector3.Cross(t.forward, axis)) * t.forward;
                Gizmos.DrawLine(origin, origin + edge * motionSightRange);
            }
            if (!Application.isPlaying) return;
            if (alert != StealthAlert.Neutral)
            {
                Gizmos.DrawWireSphere(lastSeen, 0.4f);
                Gizmos.DrawLine(origin, lastSeen);
            }
            else if (leaving)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(leavePoint, 0.6f);
                Gizmos.DrawLine(origin, leavePoint);
            }
        }
    }
}

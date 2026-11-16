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
        [SerializeField, Min(0.5f)] private float inspectDistance = 5.5f;
        [Tooltip("Velocità (m/s) verso il punto indagato.")]
        [SerializeField, Min(0.1f)] private float investigateSpeed = 1.2f;
        [Tooltip("Distanza minima (m) dal punto indagato a cui si allontana dopo aver perso interesse.")]
        [SerializeField, Min(0f)] private float leaveDistance = 10f;

        [Header("Attacco")]
        [Tooltip("Velocità (m/s) verso il sub in Attacco.")]
        [SerializeField, Min(0.1f)] private float approachSpeed = 3.5f;
        [Tooltip("Secondi di Attacco senza riuscire a caricare dopo cui torna a indagare.")]
        [SerializeField, Min(0f)] private float attackGiveUpTime = 8f;
        [Tooltip("Altezza del casco sopra i piedi del sub (m).")]
        [SerializeField, Min(0f)] private float helmetHeight = 1.3f;
        [Tooltip("Semi-angolo (°) del cono frontale del sub da cui può partire la carica.")]
        [SerializeField, Range(5f, 120f)] private float frontAngle = 45f;
        [Tooltip("Distanza (m) davanti al casco a cui si posiziona prima di caricare.")]
        [SerializeField, Min(1f)] private float frontDistance = 3.5f;
        [Tooltip("Durata della carica (s) prima dello scatto.")]
        [SerializeField, Min(0.1f)] private float windupTime = 0.8f;
        [Tooltip("Velocità dello scatto verso il casco (m/s).")]
        [SerializeField, Min(0.5f)] private float strikeSpeed = 6f;
        [Tooltip("Durata massima dello scatto (s) prima di tornare in Avvicinamento.")]
        [SerializeField, Min(0.1f)] private float strikeMaxDuration = 1.5f;
        [Tooltip("Secondi senza poter riattaccare dopo una respinta con il flash pieno.")]
        [SerializeField, Min(0f)] private float repelCooldown = 15f;
        [Tooltip("Secondi senza poter riattaccare dopo un morso.")]
        [SerializeField, Min(0f)] private float biteCooldown = 20f;
        [Tooltip("Durata della fuga (s) dopo morso o respinta.")]
        [SerializeField, Min(0.5f)] private float fleeDuration = 3f;

        // Fuga: distanza dal casco del punto di fuga (m) e direzioni provate, in gradi dalla direzione opposta al sub.
        private const float FleeDistance = 8f;
        private static readonly float[] FleeAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f };
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

        private enum AttackPhase { Approach, Windup, Strike }

        private StealthAlert alert;
        private AttackPhase attackPhase;
        private Vector3 lastSeen;
        private float lastStimulusTime;
        private float attackStartTime;
        private float phaseStartTime;
        private float noAttackUntil;
        private bool fleeing;
        private float fleeEndTime;
        private Vector3 fleeTarget;
        private Vector3 approachTarget;
        private bool hasLastFreeApproach;
        private Vector3 lastFreeApproach;
        private bool missingCameraWarned;
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
            attackPhase = AttackPhase.Approach;
            lastSeen = homePosition;
            lastStimulusTime = float.NegativeInfinity;
            attackStartTime = 0f;
            phaseStartTime = 0f;
            noAttackUntil = 0f;
            fleeing = false;
            fleeEndTime = 0f;
            hasLastFreeApproach = false;
            missingCameraWarned = false;
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
                    if ((attackPhase == AttackPhase.Windup || attackPhase == AttackPhase.Strike) &&
                        CheckFullFlashRepel())
                    {
                        StartFlee(now, repelCooldown);
                        break;
                    }
                    StepAttack(now);
                    break;
                default:
                    if (fleeing)
                    {
                        if (now >= fleeEndTime)
                        {
                            fleeing = false;
                            lastSeen = manager.PlayerPosition + Vector3.up * helmetHeight;
                            StartLeaving(now);
                        }
                        else creature.Guide(fleeTarget, creature.Profile.fleeSpeed, CreatureState.Flee, 0f, null);
                        break;
                    }
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
            if (fleeing) return;

            if (TrySeeLight(out Vector3 point))
            {
                lastSeen = point;
                lastStimulusTime = now;
                if (alert == StealthAlert.Neutral) SetAlert(StealthAlert.Investigate);
            }

            if (alert == StealthAlert.Investigate)
            {
                if (now >= noAttackUntil && DiverMovingInSight())
                {
                    SetAlert(StealthAlert.Attack);
                    attackPhase = AttackPhase.Approach;
                    phaseStartTime = now;
                    attackStartTime = now;
                    hasLastFreeApproach = false;
                }
                else if (now - lastStimulusTime > giveUpTime) StartLeaving(now);
            }
            else if (alert == StealthAlert.Attack)
            {
                float far = motionSightRange * 2f;
                if (!manager.HasPlayer ||
                    (manager.PlayerPosition - cachedTransform.position).sqrMagnitude > far * far)
                    StartLeaving(now);
                else if (attackPhase == AttackPhase.Approach && now - attackStartTime > attackGiveUpTime)
                {
                    lastSeen = manager.PlayerPosition;
                    lastStimulusTime = now;
                    SetAlert(StealthAlert.Investigate);
                }
            }
        }

        private void StepAttack(float now)
        {
            Vector3 helmet = manager.PlayerPosition + Vector3.up * helmetHeight;
            Vector3 pos = cachedTransform.position;

            switch (attackPhase)
            {
                case AttackPhase.Approach:
                {
                    Vector3 toFish = pos - helmet;
                    toFish.y = 0f;
                    float horizDist = toFish.magnitude;
                    Vector3 toFishDir = horizDist > 0.01f ? toFish / horizDist : cachedTransform.forward;

                    Vector3 look;
                    if (manager.ViewCamera != null)
                    {
                        look = manager.ViewCamera.transform.forward;
                        look.y = 0f;
                        look = look.sqrMagnitude > 0.001f ? look.normalized : toFishDir;
                    }
                    else
                    {
                        if (!missingCameraWarned)
                        {
                            missingCameraWarned = true;
                            Debug.LogWarning("[CreatureStealth] ViewCamera mancante su CreatureManager: uso direzione sub->pesce.", this);
                        }
                        look = toFishDir;
                    }

                    approachTarget = FindApproachTarget(helmet, toFishDir, look, pos);
                    creature.Guide(approachTarget, approachSpeed, CreatureState.Stalk, 1f, helmet);

                    Vector3 eye = Eye();
                    Vector3 toHelmet = helmet - eye;
                    float distToHelmet = toHelmet.magnitude;
                    if (Vector3.Angle(look, toFishDir) <= frontAngle &&
                        horizDist <= frontDistance + 1f &&
                        LineClear(eye, toHelmet, distToHelmet))
                    {
                        attackPhase = AttackPhase.Windup;
                        phaseStartTime = now;
                        attackStartTime = now;
                    }
                    break;
                }
                case AttackPhase.Windup:
                {
                    Vector3 toHelmet = helmet - pos;
                    Vector3 step = toHelmet.sqrMagnitude > 0.001f ? toHelmet.normalized * 0.3f : cachedTransform.forward * 0.3f;
                    creature.Guide(pos + step, 0.3f, CreatureState.Stalk, 1f, helmet);
                    if (now - phaseStartTime >= windupTime)
                    {
                        attackPhase = AttackPhase.Strike;
                        phaseStartTime = now;
                    }
                    break;
                }
                case AttackPhase.Strike:
                {
                    creature.Guide(helmet, strikeSpeed, CreatureState.Strike, 1f, helmet,
                        personalSpace: false, ignoreCeiling: true);
                    // Dal centro del corpo: la bocca è avanti di bodyRadius. Con il solo biteReach lo scatto che
                    // arriva di sbieco passava a 0.6 m dal casco senza mordere e gli girava attorno.
                    if (Vector3.Distance(pos, helmet) < creature.Profile.biteReach + creature.Profile.bodyRadius)
                    {
                        creature.BiteDiverGuided();
                        StartFlee(now, biteCooldown);
                    }
                    else if (now - phaseStartTime >= strikeMaxDuration)
                    {
                        attackPhase = AttackPhase.Approach;
                        phaseStartTime = now;
                        attackStartTime = now;
                    }
                    break;
                }
            }
        }

        private Vector3 FindApproachTarget(Vector3 helmet, Vector3 toFishDir, Vector3 look, Vector3 currentPos)
        {
            float delta = Vector3.SignedAngle(toFishDir, look, Vector3.up);
            float step = Mathf.Abs(delta) > 150f ? orbitSign * 60f : Mathf.Clamp(delta, -60f, 60f);
            Vector3 desiredDir = Quaternion.AngleAxis(step, Vector3.up) * toFishDir;
            Vector3 candidate = helmet + desiredDir * frontDistance;
            if (!Physics.Linecast(helmet, candidate, manager.ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                lastFreeApproach = candidate;
                hasLastFreeApproach = true;
                return candidate;
            }

            float[] distScales = { 1f, 0.75f, 0.5f };
            float[] angleOffsets = { 20f, -20f, 40f, -40f };
            foreach (float scale in distScales)
            {
                float d = frontDistance * scale;
                foreach (float angle in angleOffsets)
                {
                    Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * look;
                    Vector3 alt = helmet + dir * d;
                    if (!Physics.Linecast(helmet, alt, manager.ObstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        lastFreeApproach = alt;
                        hasLastFreeApproach = true;
                        return alt;
                    }
                }
            }

            return hasLastFreeApproach ? lastFreeApproach : currentPos;
        }

        private bool CheckFullFlashRepel()
        {
            var stimuli = manager.Stimuli;
            Vector3 pos = cachedTransform.position;
            for (int i = 0; i < stimuli.Count; i++)
            {
                CreatureStimulus s = stimuli[i];
                if (s.Category != StimulusCategory.Flash || !s.FromPlayer || !s.FullFlash) continue;
                if ((s.Position - pos).sqrMagnitude <= s.Radius * s.Radius) return true;
            }
            return false;
        }

        private void StartFlee(float now, float cooldown)
        {
            SetAlert(StealthAlert.Neutral);
            noAttackUntil = now + cooldown;
            fleeing = true;
            fleeEndTime = now + fleeDuration;
            Vector3 helmet = manager.PlayerPosition + Vector3.up * helmetHeight;
            Vector3 away = cachedTransform.position - helmet;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -cachedTransform.forward;
            away.y = 0f;
            away.Normalize();
            // Lontano dal casco; se in quella direzione c'è roccia entro la fuga, la prima direzione libera più vicina
            // (l'evitamento lo farebbe girare e tornare verso il sub).
            fleeTarget = helmet + away * FleeDistance;
            Vector3 from = cachedTransform.position;
            foreach (float angle in FleeAngles)
            {
                Vector3 candidate = helmet + Quaternion.AngleAxis(angle, Vector3.up) * away * FleeDistance;
                if (Physics.Linecast(from, candidate, manager.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
                fleeTarget = candidate;
                break;
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
            Vector3 forward = ViewAxis(eye);
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
                   InView(ViewAxis(eye), toTorso, distance, Mathf.Cos(viewHalfAngle * Mathf.Deg2Rad)) &&
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

        // Asse del cono visivo: il muso, ma in Indaga gli occhi seguono il punto indagato mentre il corpo ci gira
        // attorno di traverso; col muso la lampada uscirebbe dal cono a ogni giro e perderebbe interesse.
        private Vector3 ViewAxis(Vector3 eye)
        {
            if (alert != StealthAlert.Investigate) return cachedTransform.forward;
            Vector3 toPoint = lastSeen - eye;
            return toPoint.sqrMagnitude > 0.01f ? toPoint.normalized : cachedTransform.forward;
        }

        private Vector3 Eye() =>cachedTransform.position + cachedTransform.forward * creature.Profile.bodyRadius;

        private static bool InView(Vector3 forward, Vector3 toTarget, float distance, float minCos) =>
            distance < 1e-3f || Vector3.Dot(forward, toTarget) / distance >= minCos;

        private bool LineClear(Vector3 eye, Vector3 toTarget, float distance) =>
            distance <= SightRayMargin || !Physics.Raycast(eye, toTarget / distance, distance - SightRayMargin,
                manager.ObstacleMask, QueryTriggerInteraction.Ignore);

        private void OnDrawGizmosSelected()
        {
            Transform t = transform;
            if (alert == StealthAlert.Attack)
            {
                Gizmos.color = attackPhase == AttackPhase.Windup ? Color.yellow
                    : attackPhase == AttackPhase.Strike ? Color.magenta : Color.red;
            }
            else
            {
                Gizmos.color = alert == StealthAlert.Investigate ? new Color(1f, 0.65f, 0f) : new Color(0.2f, 0.5f, 1f);
            }
            Vector3 origin = t.position;
            foreach (Vector3 axis in new[] { t.up, -t.up, t.right, -t.right })
            {
                Vector3 edge = Quaternion.AngleAxis(viewHalfAngle, Vector3.Cross(t.forward, axis)) * t.forward;
                Gizmos.DrawLine(origin, origin + edge * motionSightRange);
            }
            if (!Application.isPlaying) return;
            if (alert == StealthAlert.Attack && attackPhase == AttackPhase.Approach)
            {
                Gizmos.DrawWireSphere(approachTarget, 0.35f);
                Gizmos.DrawLine(origin, approachTarget);
            }
            else if (alert != StealthAlert.Neutral)
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

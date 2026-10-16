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

        [Tooltip("Renderer con lo shader Deeplonauts/Fish (opzionale), uno per livello di LOD: il nuoto è un'onda " +
                 "nello shader e la creatura passa a tutti fase, ampiezza e curva della virata a ogni frame in cui " +
                 "almeno uno è visibile.")]
        [SerializeField] private Renderer[] swimRenderers;

        [Tooltip("Colpi di coda al secondo alla velocità di crociera (Hz).")]
        [SerializeField, Min(0f)] private float swimFrequency = 2.5f;

        [Tooltip("Velocità di virata (°/s) a cui il corpo è piegato del tutto (_SwimBend = ±1).")]
        [SerializeField, Min(1f)] private float fullBendTurnRate = 120f;

        private static readonly int SwimPhaseId = Shader.PropertyToID("_SwimPhase");
        private static readonly int SwimAmountId = Shader.PropertyToID("_SwimAmount");
        private static readonly int SwimBendId = Shader.PropertyToID("_SwimBend");
        private static readonly int GlowBoostId = Shader.PropertyToID("_GlowBoost");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        // Durata del lampo come stimolo per le altre creature, come il flash della torcia del sub.
        private const float AlarmPulseDuration = 0.4f;
        private const float StartleBoost = 6f;
        // Banco: peso della separazione (sempre più forte delle altre regole) e vicini considerati al massimo.
        private const float SeparationWeight = 2f;
        private const int MaxNeighbours = 8;
        // Scivolata diretta lontano dai compagni troppo vicini (m/s a spinta 1): con virate lente la sola rotta non
        // basta a staccare due pesci paralleli.
        private const float SeparationSlip = 0.6f;
        // Corpo del sub per lo spazio personale: segmento verticale dai piedi (posizione del PlayerCapsule) alla
        // testa; scivolata massima fuori dallo spazio personale (m/s).
        private const float DiverHeight = 1.5f;
        private const float DiverClearanceSlip = 1.5f;
        // Predatore: centro dell'orbita all'altezza del busto, oscillazione di quota attorno al centro (m), secondi
        // per cui uno stimolo del sub conta ancora come presente, intervallo dei cambi di verso dell'orbita (s).
        private const float StalkCentreHeight = 1f;
        private const float StalkHeightSwing = 1f;
        private const float StalkStimulusMemory = 0.5f;
        private const float OrbitFlipMin = 8f;
        private const float OrbitFlipMax = 20f;
        // Morso: punto del sub da colpire (casco, sopra i piedi), durata massima dello slancio, aggressività dopo uno
        // slancio a vuoto, distanza orizzontale dal busto entro cui parte (in multipli di strikeRadius).
        private const float StrikeTargetHeight = 1.3f;
        private const float StrikeMaxDuration = 1.5f;
        private const float MissedStrikeAggression = 0.7f;
        private const float StrikeStartFactor = 1.3f;
        // Caccia: durata massima di un inseguimento, pausa dopo una preda persa, intervallo del rumore che avverte
        // le prede, distanza di cattura in frazione di biteReach.
        private const float HuntMaxDuration = 8f;
        private const float HuntGiveUpRest = 4f;
        private const float PreyAlertInterval = 0.15f;
        private const float CatchFactor = 0.8f;
        private static MaterialPropertyBlock swimBlock;

        private float swimPhase;
        private float swimBend;
        private float startleTime = float.NegativeInfinity;
        // Dissolvenza d'entrata (paracadute del nido): inizio e durata; durata 0 = già tutta visibile.
        private float fadeStartTime;
        private float fadeDuration;

        // Regole di banco dell'ultimo ragionamento: spinta di separazione, direzione verso il baricentro dei
        // compagni (lunga al massimo 1), rotta media dei compagni.
        private Vector3 separationSteer;
        private Vector3 cohesionSteer;
        private Vector3 alignmentSteer;

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
        private float lastFearTime;
        private float lastInterestTime;
        private Vector3 threatPosition;
        private CreatureStimulus interest;

        // Posto personale in Inspect, relativo alla sorgente: frazioni del semi-angolo del fascio (yaw, pitch),
        // direzione per le luci omnidirezionali, fattore sulla distanza preferita, prossimo cambio di posto.
        private float spotYaw;
        private float spotPitch;
        private Vector3 spotDirection;
        private float spotDistanceFactor;
        private float nextRepositionTime;
        private StimulusCategory perceived;

        // Predatore: aggressività 0..1, verso dell'orbita (+1 antiorario visto dall'alto, -1 orario), prossimo
        // cambio di verso.
        private float aggression;
        private float orbitSign = 1f;
        private float nextOrbitFlipTime;
        private float lastOrbitFlipTime;

        // Morso e caccia: inizio dello slancio, primo istante in cui può rimordere, ultimo morso (sub o preda),
        // preda inseguita, inizio dell'inseguimento, fine della sazietà, prossimo rumore per le prede.
        private float strikeStartTime;
        private float strikeCooldownUntil;
        private float lastBiteTime = float.NegativeInfinity;
        private Creature preyTarget;
        private float huntStartTime;
        private float satedUntil;
        private float nextPreyAlertTime;

        // Risultati degli ultimi raggi, riusati finché il livello di dettaglio non ne lancia altri.
        private float floorY = float.NaN;
        private Vector3 avoidDirection;
        private float avoidWeight;
        // Ultima posizione in cui il corpo non toccava ostacoli, per uscirne dalla parte da cui è entrato.
        private Vector3 lastFreePosition;
        private bool hasFreePosition;

        /// <summary>Profilo con cui la creatura è stata presa dal pool.</summary>
        public CreatureProfile Profile => profile;

        /// <summary>Comportamento attivo.</summary>
        public CreatureState State => state;

        /// <summary>Livello di dettaglio assegnato dal manager nell'ultimo frame.</summary>
        public CreatureLod Lod { get; internal set; }

        /// <summary>Categoria dello stimolo che ha deciso la reazione nell'ultimo ragionamento; None se nessuno.</summary>
        public StimulusCategory Perceived => perceived;

        /// <summary>Velocità attuale (m/s) lungo la rotta.</summary>
        public float Speed => speed;

        /// <summary>Rotta attuale (normalizzata).</summary>
        public Vector3 Heading => heading;

        /// <summary>
        /// Aggressività verso il sub, da 0 a 1: sale con la sua luce e il suo rumore mentre la creatura gli gira
        /// attorno, scende al buio. Stringe l'orbita e apre la mascella. Sempre 0 per chi non è un predatore.
        /// </summary>
        public float Aggression => aggression;

        /// <summary>Istante (Time.time) dell'ultimo morso, al sub o a una preda; -infinito se non ha mai morso.</summary>
        public float LastBiteTime => lastBiteTime;

        /// <summary>Preda inseguita in <c>Hunt</c>; null negli altri stati.</summary>
        public Creature Prey => preyTarget;

        /// <summary>
        /// Punto che la creatura sta guardando, per gli occhi: il casco del sub in <c>Stalk</c> e <c>Strike</c>, la
        /// preda in <c>Hunt</c>, la sorgente di luce in <c>Inspect</c>. False negli altri stati.
        /// </summary>
        public bool TryGetFocus(out Vector3 point)
        {
            switch (state)
            {
                case CreatureState.Stalk:
                case CreatureState.Strike:
                    if (manager != null && manager.HasPlayer)
                    {
                        point = manager.PlayerPosition + Vector3.up * StrikeTargetHeight;
                        return true;
                    }
                    break;
                case CreatureState.Hunt:
                    if (preyTarget != null && preyTarget.ActiveIndex >= 0)
                    {
                        point = preyTarget.transform.position;
                        return true;
                    }
                    break;
                case CreatureState.Inspect:
                    point = interest.Position;
                    return true;
            }
            point = default;
            return false;
        }

        /// <summary>Compagni della stessa specie visti nell'ultimo ragionamento (0 per le specie solitarie).</summary>
        public int NeighbourCount { get; private set; }

        /// <summary>
        /// True se la creatura può comparire in dissolvenza: servono i renderer del nuoto con lo shader
        /// Deeplonauts/Fish, che legge <c>_Fade</c>.
        /// </summary>
        public bool CanFadeIn => swimRenderers != null && swimRenderers.Length > 0;

        /// <summary>
        /// Fa comparire la creatura con una dissolvenza a retino lunga <paramref name="duration"/> secondi. Senza
        /// <see cref="CanFadeIn"/> non ha effetto.
        /// </summary>
        public void FadeIn(float duration)
        {
            fadeStartTime = Time.time;
            fadeDuration = Mathf.Max(0f, duration);
        }

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
            lastFearTime = float.NegativeInfinity;
            lastInterestTime = float.NegativeInfinity;
            perceived = StimulusCategory.None;
            floorY = float.NaN;
            avoidWeight = 0f;
            hasFreePosition = false;
            Lod = CreatureLod.Near;
            swimPhase = Random.value * 2f * Mathf.PI;
            swimBend = 0f;
            startleTime = float.NegativeInfinity;
            fadeDuration = 0f;
            separationSteer = cohesionSteer = alignmentSteer = Vector3.zero;
            NeighbourCount = 0;
            aggression = 0f;
            orbitSign = Random.value < 0.5f ? -1f : 1f;
            FlipOrbit(Time.time, false);
            strikeCooldownUntil = 0f;
            lastBiteTime = float.NegativeInfinity;
            preyTarget = null;
            satedUntil = 0f;
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
            float now = Time.time;
            StimulusReaction reaction = Perceive(cachedTransform.position, out CreatureStimulus chosen);
            perceived = reaction == StimulusReaction.Ignore ? StimulusCategory.None : chosen.Category;

            if (reaction == StimulusReaction.Flee)
            {
                lastFearTime = now;
                threatPosition = chosen.Position;
                if (state != CreatureState.Flee) Startle(now);
                state = CreatureState.Flee;
            }
            else if (reaction == StimulusReaction.Approach)
            {
                lastInterestTime = now;
                interest = chosen;
                // La paura vince: chi sta scappando si avvicina solo dopo essersi calmato. Il predatore che gira
                // attorno al sub, si lancia o caccia non va a osservare: lo stimolo gli fa salire l'aggressività
                // (UpdateAggression).
                if (state == CreatureState.Wander)
                {
                    state = CreatureState.Inspect;
                    PickSpot(now);
                }
            }

            if (state == CreatureState.Inspect && now >= nextRepositionTime) PickSpot(now);
            if (profile.schoolRadius > 0f) School();

            if (state == CreatureState.Flee && now - lastFearTime > profile.calmDelay)
            {
                bool stillCurious = now - lastInterestTime <= profile.calmDelay;
                state = stillCurious ? CreatureState.Inspect : CreatureState.Wander;
                if (stillCurious) PickSpot(now);
            }
            else if (state == CreatureState.Inspect && now - lastInterestTime > profile.calmDelay)
                state = CreatureState.Wander;

            ThinkPredator(now);
        }

        /// <summary>
        /// Stati del predatore, dopo la percezione (che può sempre mandarlo in fuga, anche a metà slancio: il flash
        /// della torcia salva il sub all'ultimo). In ordine: fine dello slancio a vuoto, fine della caccia, orbita
        /// attorno al sub se è a portata, slancio ad aggressività piena, caccia se c'è una preda vicina.
        /// </summary>
        private void ThinkPredator(float now)
        {
            if (state == CreatureState.Strike && now - strikeStartTime > StrikeMaxDuration)
            {
                // Il sub si è spostato: torna a girargli attorno, ancora arrabbiato.
                aggression = Mathf.Min(aggression, MissedStrikeAggression);
                state = CreatureState.Stalk;
            }

            if (state == CreatureState.Hunt && !PreyStillValid(now))
            {
                preyTarget = null;
                satedUntil = Mathf.Max(satedUntil, now + HuntGiveUpRest);
                state = CreatureState.Wander;
            }

            // Finché il sub è a portata gli gira attorno, anche al buio. Esce oltre il 120% della portata, così
            // non entra ed esce di continuo sul bordo.
            if (profile.stalkRange > 0f && (state == CreatureState.Wander || state == CreatureState.Stalk ||
                                            state == CreatureState.Inspect))
            {
                float range = profile.stalkRange * (state == CreatureState.Stalk ? 1.2f : 1f);
                bool inRange = manager.HasPlayer &&
                               (manager.PlayerPosition - cachedTransform.position).sqrMagnitude < range * range;
                if (inRange) state = CreatureState.Stalk;
                else if (state == CreatureState.Stalk) state = CreatureState.Wander;
            }

            if (state == CreatureState.Stalk && aggression >= 0.999f && now >= strikeCooldownUntil)
            {
                Vector3 toTorso = manager.PlayerPosition + Vector3.up * StalkCentreHeight - cachedTransform.position;
                toTorso.y = 0f;
                float reach = profile.strikeRadius * StrikeStartFactor;
                if (toTorso.sqrMagnitude < reach * reach)
                {
                    state = CreatureState.Strike;
                    strikeStartTime = now;
                }
            }

            if (profile.prey != null && now >= satedUntil && aggression <= profile.huntMaxAggression &&
                (state == CreatureState.Wander || state == CreatureState.Stalk))
            {
                Creature prey = manager.FindNearest(profile.prey, cachedTransform.position, profile.huntRange);
                if (prey != null)
                {
                    preyTarget = prey;
                    huntStartTime = now;
                    state = CreatureState.Hunt;
                }
            }
        }

        /// <summary>
        /// La preda c'è ancora (non è tornata nel pool), è della specie giusta, non si è allontanata oltre una volta e
        /// mezza <c>huntRange</c>, l'inseguimento non dura da troppo e il sub non ha fatto salire troppo l'aggressività.
        /// </summary>
        private bool PreyStillValid(float now)
        {
            if (preyTarget == null || preyTarget.ActiveIndex < 0 || !ReferenceEquals(preyTarget.Profile, profile.prey))
                return false;
            if (now - huntStartTime > HuntMaxDuration || aggression > profile.huntMaxAggression)
                return false;
            float giveUp = profile.huntRange * 1.5f;
            return (preyTarget.transform.position - cachedTransform.position).sqrMagnitude < giveUp * giveUp;
        }

        /// <summary>
        /// Morso al sub: lo segnala al manager (che lo passa al sub), poi scappa con lo scatto della fuga, perde
        /// tutta l'aggressività e non può rilanciarsi per <c>strikeCooldown</c> secondi.
        /// </summary>
        private void BiteDiver(float now)
        {
            lastBiteTime = now;
            manager.ReportBite(this);
            threatPosition = manager.PlayerPosition + Vector3.up * StrikeTargetHeight;
            lastFearTime = now;
            state = CreatureState.Flee;
            startleTime = now;
            aggression = 0f;
            strikeCooldownUntil = now + profile.strikeCooldown;
        }

        /// <summary>Preda presa: torna nel pool con il suo lampo d'allarme e il predatore resta sazio.</summary>
        private void CatchPrey(float now)
        {
            lastBiteTime = now;
            manager.Eat(preyTarget);
            preyTarget = null;
            satedUntil = now + profile.satiation;
            state = CreatureState.Wander;
        }

        /// <summary>
        /// Regole di banco con i compagni della stessa specie entro <c>schoolRadius</c>: separazione da chi è
        /// troppo vicino, coesione verso il baricentro, allineamento alla rotta media. Le spinte vengono applicate
        /// in <see cref="Move"/> secondo lo stato.
        /// </summary>
        private void School()
        {
            Vector3 position = cachedTransform.position;
            int count = manager.Flock(this, position, profile.schoolRadius, profile.separationDistance, MaxNeighbours,
                out separationSteer, out Vector3 centre, out Vector3 alignment);
            NeighbourCount = count;
            if (count == 0)
            {
                cohesionSteer = alignmentSteer = Vector3.zero;
                return;
            }
            cohesionSteer = Vector3.ClampMagnitude((centre / count - position) / profile.schoolRadius, 1f);
            alignmentSteer = alignment.sqrMagnitude > 1e-4f ? alignment.normalized : Vector3.zero;
        }

        /// <summary>
        /// Inizio della fuga: scatto (virata e accelerazione moltiplicate per qualche decimo di secondo, da cui la
        /// piega a C del corpo) e lampo dei fotofori. Se la specie ha un <c>alarmRadius</c>, il lampo diventa uno
        /// stimolo Flash per le altre creature: l'allarme si propaga nel banco, ogni pesce lampeggia una volta sola
        /// quando entra in fuga.
        /// </summary>
        private void Startle(float now)
        {
            startleTime = now;
            // Un predatore spaventato (il flash della torcia) perde metà dell'aggressività: accecarlo fa guadagnare tempo.
            aggression *= 0.5f;
            if (profile.startleGlow > 0f && profile.alarmRadius > 0f)
            {
                CreatureStimulus alarm = CreatureStimulus.PointLight(cachedTransform.position, profile.alarmRadius,
                    StimulusCategory.Flash, false);
                alarm.Emitter = this;
                manager.AddPulse(alarm, AlarmPulseDuration);
            }
        }

        /// <summary>
        /// Valuta gli stimoli del frame. Per ognuno: portata = <c>Radius</c> × sensibilità della specie; una luce
        /// conta solo se la creatura è nel fascio o nell'alone e se fra la sorgente e la creatura non ci sono
        /// ostacoli, il rumore passa attraverso tutto. Fra gli stimoli percepiti a cui la specie reagisce sceglie
        /// il più forte (il più vicino in proporzione alla portata), con la fuga che vince sull'avvicinamento.
        /// Il raggio di linea di vista parte solo quando i controlli economici sono passati.
        /// </summary>
        private StimulusReaction Perceive(Vector3 position, out CreatureStimulus chosen)
        {
            chosen = default;
            StimulusReaction best = StimulusReaction.Ignore;
            float bestScore = float.MaxValue;
            IReadOnlyList<CreatureStimulus> stimuli = manager.Stimuli;

            for (int i = 0; i < stimuli.Count; i++)
            {
                CreatureStimulus stimulus = stimuli[i];
                if (ReferenceEquals(stimulus.Emitter, this)) continue;
                // Un predatore non si spaventa per il lampo d'allarme delle sue prede: è quello che sta cacciando.
                if (profile.prey != null && stimulus.Emitter != null &&
                    ReferenceEquals(stimulus.Emitter.Profile, profile.prey))
                    continue;
                StimulusReaction reaction = profile.ReactionTo(stimulus.Category);
                if (reaction == StimulusReaction.Ignore) continue;
                // Uno stimolo che attira non può battere uno che spaventa già trovato.
                if (reaction == StimulusReaction.Approach && best == StimulusReaction.Flee) continue;

                float sensitivity = stimulus.IsLight ? profile.lightSensitivity : profile.hearingSensitivity;
                float reach = stimulus.Radius * sensitivity;
                if (reach <= 0f) continue;

                Vector3 fromSource = position - stimulus.Position;
                float sqrDistance = fromSource.sqrMagnitude;
                if (sqrDistance > reach * reach) continue;

                float distance = Mathf.Sqrt(sqrDistance);
                float score = distance / reach;
                bool upgradesToFlee = reaction == StimulusReaction.Flee && best != StimulusReaction.Flee;
                if (!upgradesToFlee && score >= bestScore) continue;

                if (stimulus.IsLight && distance > 0.01f)
                {
                    Vector3 direction = fromSource / distance;
                    bool inHalo = distance <= stimulus.HaloRadius * sensitivity;
                    if (!inHalo && Vector3.Dot(stimulus.Direction, direction) < stimulus.ConeCos) continue;
                    if (Physics.Raycast(stimulus.Position, direction, distance - profile.bodyRadius,
                            manager.ObstacleMask, QueryTriggerInteraction.Ignore))
                        continue;
                }

                best = reaction;
                bestScore = score;
                chosen = stimulus;
            }

            return best;
        }

        /// <summary>
        /// Sceglie un nuovo posto da cui osservare la sorgente: dentro il fascio (fino all'80% del semi-angolo) per
        /// una luce direzionale, su un guscio sopra la sorgente per una luce omnidirezionale, a una distanza fra il
        /// 75% e il 130% di quella preferita. Il posto è relativo alla sorgente, quindi la segue se si muove.
        /// </summary>
        private void PickSpot(float now)
        {
            spotYaw = Random.Range(-0.8f, 0.8f);
            spotPitch = Random.Range(-0.8f, 0.8f);
            Vector2 flat = Random.insideUnitCircle.normalized;
            spotDirection = new Vector3(flat.x, Random.Range(0.15f, 0.8f), flat.y).normalized;
            spotDistanceFactor = Random.Range(0.75f, 1.3f);
            float interval = profile.inspectRepositionInterval;
            nextRepositionTime = now + Random.Range(interval * 0.5f, interval * 1.5f);
        }

        /// <summary>Posizione attuale del posto personale rispetto alla sorgente che la creatura osserva.</summary>
        private Vector3 SpotPosition()
        {
            bool strong = interest.Category == StimulusCategory.StrongLight || interest.Category == StimulusCategory.Flash;
            float distance = profile.inspectDistance * spotDistanceFactor * (strong ? profile.strongLightDistanceScale : 1f);

            Vector3 direction;
            if (interest.ConeCos <= -1f || interest.Direction.sqrMagnitude < 0.5f)
            {
                direction = spotDirection;
            }
            else
            {
                float halfAngle = Mathf.Acos(Mathf.Clamp(interest.ConeCos, -1f, 1f)) * Mathf.Rad2Deg;
                direction = Quaternion.LookRotation(interest.Direction) *
                            Quaternion.Euler(spotPitch * halfAngle, spotYaw * halfAngle, 0f) * Vector3.forward;
            }
            return interest.Position + direction * distance;
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

            // Lo SphereCast ignora i collider in cui parte già immerso. Se il corpo tocca un ostacolo torna
            // all'ultima posizione libera e riparte da lì, dalla parte da cui è entrato: spingerlo sempre in alto
            // lo faceva attraversare le sporgenze da sotto, sparendo nella roccia e riapparendo sopra. Senza una
            // posizione libera (appena uscito dal pool dentro un ostacolo) esce allontanandosi dal centro, in alto.
            int overlaps = Physics.OverlapSphereNonAlloc(position, profile.bodyRadius, OverlapBuffer, mask,
                QueryTriggerInteraction.Ignore);
            if (overlaps > 0)
            {
                Vector3 away;
                if (hasFreePosition)
                {
                    away = lastFreePosition - position;
                    position = lastFreePosition;
                    cachedTransform.position = position;
                    if (away.sqrMagnitude < 1e-6f) away = -heading;
                }
                else
                {
                    away = position - OverlapBuffer[0].bounds.center;
                    away.y = Mathf.Max(away.y, 0f);
                    away = away.normalized + Vector3.up;
                }
                avoidDirection = away.normalized;
                avoidWeight = 1f;
                return;
            }
            lastFreePosition = position;
            hasFreePosition = true;

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

                // Predatore: se l'orbita lo porta contro la roccia inverte il verso, invece di scivolare lungo la
                // parete verso il sub. In un corridoio fa avanti e indietro sul lato libero, alla sua distanza.
                if (state == CreatureState.Stalk && avoidWeight > 0.5f && Time.time - lastOrbitFlipTime > 2f)
                    FlipOrbit(Time.time, true);
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

            float targetY = float.IsNaN(floorY) ? position.y : floorY + profile.heightAboveFloor;
            if (profile.stalkRange > 0f) UpdateAggression(deltaTime);

            bool lunging = false;
            if (state == CreatureState.Strike)
            {
                // Dritto al casco del sub, a bocca spalancata, senza spazio personale.
                Vector3 target = manager.PlayerPosition + Vector3.up * StrikeTargetHeight;
                Vector3 toTarget = target - position;
                desired = toTarget;
                targetY = target.y;
                targetSpeed = profile.strikeSpeed;
                lunging = true;
                if (toTarget.magnitude < profile.biteReach) BiteDiver(Time.time);
            }
            else if (state == CreatureState.Hunt && preyTarget != null && preyTarget.ActiveIndex >= 0)
            {
                Vector3 target = preyTarget.transform.position;
                Vector3 toPrey = target - position;
                float distance = toPrey.magnitude;
                desired = toPrey;
                targetY = target.y;
                lunging = distance < profile.lungeDistance;
                targetSpeed = lunging ? profile.strikeSpeed : profile.cruiseSpeed * 1.5f;
                // Le prede lo sentono solo quando è vicino: un rumore breve che le fa scappare.
                if (profile.preyAlertRadius > 0f && Time.time >= nextPreyAlertTime)
                {
                    CreatureStimulus alert = CreatureStimulus.Noise(position, profile.preyAlertRadius, false);
                    alert.Emitter = this;
                    manager.AddPulse(alert, PreyAlertInterval * 1.5f);
                    nextPreyAlertTime = Time.time + PreyAlertInterval;
                }
                if (distance < profile.biteReach * CatchFactor) CatchPrey(Time.time);
            }
            else if (state == CreatureState.Stalk)
            {
                desired = StalkHeading(position, out targetY);
                // Più è aggressivo, più rallenta: si avvicina strisciando invece di girare di corsa.
                targetSpeed = profile.cruiseSpeed * Mathf.Lerp(1f, 0.6f, aggression);
            }
            else if (state == CreatureState.Flee)
            {
                targetSpeed = profile.fleeSpeed;
                desired = (position - threatPosition).normalized;
                // Il banco scappa compatto: stessa direzione dei compagni, senza urtarsi.
                desired += alignmentSteer * profile.alignmentWeight + separationSteer * SeparationWeight;
            }
            else if (state == CreatureState.Inspect)
            {
                // Va al suo posto rallentando; arrivato, gira il muso verso la luce e ci scivola contro piano, così
                // si allontana dal posto e ci torna: l'andirivieni di un pesce attirato dalla luce, non un'orbita.
                Vector3 spot = SpotPosition();
                if (!float.IsNaN(floorY)) spot.y = Mathf.Max(spot.y, floorY + profile.bodyRadius * 2f);
                Vector3 toSpot = spot - position;
                float spotDistance = toSpot.magnitude;
                Vector3 toLight = interest.Position - position;
                float arrive = Mathf.Clamp01(spotDistance / 1f);
                desired = Vector3.Lerp(toLight.normalized, toSpot / Mathf.Max(spotDistance, 0.01f), arrive);
                targetSpeed = profile.cruiseSpeed * Mathf.Clamp(spotDistance / 1.5f, 0.2f, 1f);
                // Ognuno resta al suo posto, ma si scosta da chi gli finisce addosso, anche in quota.
                desired += separationSteer * SeparationWeight;
                targetY = spot.y;
            }
            else
            {
                targetSpeed = profile.cruiseSpeed;
                float noise = Mathf.PerlinNoise(noiseSeed, Time.time * profile.wanderFrequency) * 2f - 1f;
                desired = Quaternion.AngleAxis(noise * profile.wanderTurn * deltaTime, Vector3.up) * heading;
                desired = PullTowardsHome(position, desired);
                desired += cohesionSteer * profile.cohesionWeight + alignmentSteer * profile.alignmentWeight +
                           separationSteer * SeparationWeight;
                // Il baricentro dei compagni sposta anche la quota, entro i limiti di fondale e soffitto.
                targetY += cohesionSteer.y * profile.schoolRadius * profile.cohesionWeight * 0.5f;
            }

            // Spazio personale del sub: nessuna creatura gli passa attraverso. Senza, i pesci attirati dalla torcia
            // scivolavano fino alla lampada sul casco e attraversavano la camera (pop sul piano vicino).
            // Nel morso no: deve arrivare a contatto.
            Vector3 diverPush = state == CreatureState.Strike ? Vector3.zero : DiverPush(position);
            desired += diverPush * 4f;
            targetY += diverPush.y * profile.diverClearance;

            float dy = targetY - position.y;
            float horizontal = new Vector2(desired.x, desired.z).magnitude;
            desired.y = 0f;
            if (desired.sqrMagnitude < 0.0001f) desired = new Vector3(heading.x, 0f, heading.z);
            desired.Normalize();
            // Nello slancio punta dritto al bersaglio anche in quota (niente tolleranza): la pendenza è dislivello su
            // distanza orizzontale, sempre entro MaxClimb.
            desired.y = lunging
                ? Mathf.Clamp(dy / Mathf.Max(horizontal, 0.3f), -MaxClimb, MaxClimb)
                : Mathf.Clamp(HeightCorrection(position.y, targetY), -MaxClimb, MaxClimb);

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

            // Scatto di fuga: per startleDuration secondi vira e accelera molto più in fretta.
            // Lo slancio usa lo stesso scatto: virata e accelerazione moltiplicate, corpo che si piega di più.
            bool startled = Time.time - startleTime < profile.startleDuration || lunging;
            float acceleration = profile.acceleration;
            if (startled)
            {
                turnRate *= StartleBoost;
                acceleration *= StartleBoost;
            }

            Vector3 previousHeading = heading;
            heading = Vector3.RotateTowards(heading, desired.normalized, turnRate * Mathf.Deg2Rad * deltaTime, 0f);
            heading.y = Mathf.Clamp(heading.y, -MaxClimb, MaxClimb);
            heading.Normalize();
            speed = Mathf.MoveTowards(speed, targetSpeed, acceleration * deltaTime);

            position += heading * (speed * deltaTime) + separationSteer * (SeparationSlip * deltaTime) +
                        diverPush * (DiverClearanceSlip * deltaTime);
            if (!float.IsNaN(floorY)) position.y = Mathf.Max(position.y, floorY + profile.bodyRadius);
            cachedTransform.SetPositionAndRotation(position, Quaternion.LookRotation(heading));

            if (swimAnimator != null)
                swimAnimator.speed = swimAnimationSpeed * Mathf.Max(0.3f, speed / profile.cruiseSpeed);
            if (swimRenderers != null && swimRenderers.Length > 0)
                UpdateSwim(previousHeading, deltaTime, startled);
        }

        /// <summary>
        /// Nuoto nello shader: la fase avanza con la velocità (più veloce = colpi di coda più rapidi e ampi),
        /// la curva segue la virata orizzontale (corpo a C, coda verso l'interno della curva come sull'arco
        /// percorso). Durante lo scatto la piega può arrivare a 1.6 e cambia in fretta: è la C della fuga dei pesci.
        /// Il lampo dei fotofori sale in 0.05 s e si spegne in <c>startleGlowDuration</c>, tremolando.
        /// Scrive il blocco solo se il renderer è visibile; la fase avanza comunque, così non salta quando torna
        /// in vista.
        /// </summary>
        private void UpdateSwim(Vector3 previousHeading, float deltaTime, bool startled)
        {
            float pace = speed / profile.cruiseSpeed;
            swimPhase += swimFrequency * Mathf.Clamp(pace, 0.35f, 3f) * 2f * Mathf.PI * deltaTime;
            if (swimPhase > 1000f) swimPhase -= 2f * Mathf.PI * 159f;

            float yawRate = deltaTime > 0f
                ? Vector3.SignedAngle(new Vector3(previousHeading.x, 0f, previousHeading.z),
                    new Vector3(heading.x, 0f, heading.z), Vector3.up) / deltaTime
                : 0f;
            float maxBend = startled ? 1.6f : 1f;
            float targetBend = Mathf.Clamp(yawRate / fullBendTurnRate, -maxBend, maxBend);
            swimBend = Mathf.MoveTowards(swimBend, targetBend, (startled ? 20f : 4f) * deltaTime);

            float glow = 0f;
            float sinceStartle = Time.time - startleTime;
            if (profile.startleGlow > 0f && sinceStartle < profile.startleGlowDuration)
            {
                float fade = 1f - sinceStartle / profile.startleGlowDuration;
                float flicker = 0.75f + 0.25f * Mathf.Sin(sinceStartle * 40f + swimPhase);
                glow = profile.startleGlow * Mathf.Clamp01(sinceStartle / 0.05f) * fade * fade * flicker;
            }

            bool visible = false;
            for (int i = 0; i < swimRenderers.Length; i++) visible |= swimRenderers[i].isVisible;
            // In dissolvenza scrive anche da non visibile: appena uscita dal pool isVisible è ancora falso e il
            // primo frame userebbe il blocco della vita precedente, tutto opaco.
            bool fading = fadeDuration > 0f && Time.time - fadeStartTime < fadeDuration;
            if (!visible && !fading) return;
            swimBlock ??= new MaterialPropertyBlock();
            swimBlock.SetFloat(SwimPhaseId, swimPhase);
            swimBlock.SetFloat(SwimAmountId, Mathf.Clamp(0.6f + 0.4f * pace, 0.5f, 1.4f));
            swimBlock.SetFloat(SwimBendId, swimBend);
            swimBlock.SetFloat(GlowBoostId, glow);
            swimBlock.SetFloat(FadeId, fadeDuration > 0f ? Mathf.Clamp01((Time.time - fadeStartTime) / fadeDuration) : 1f);
            for (int i = 0; i < swimRenderers.Length; i++) swimRenderers[i].SetPropertyBlock(swimBlock);
        }

        /// <summary>
        /// Spinta fuori dallo spazio personale del sub: direzione dal punto più vicino del corpo (segmento dai piedi
        /// alla testa) alla creatura, lunga da 0 al bordo dello spazio a 1 sul corpo. Zero se fuori o senza sub.
        /// </summary>
        private Vector3 DiverPush(Vector3 position)
        {
            float clearance = profile.diverClearance;
            if (clearance <= 0f || !manager.HasPlayer) return Vector3.zero;
            Vector3 feet = manager.PlayerPosition;
            Vector3 closest = new Vector3(feet.x, Mathf.Clamp(position.y, feet.y, feet.y + DiverHeight), feet.z);
            Vector3 away = position - closest;
            float distance = away.magnitude;
            if (distance >= clearance) return Vector3.zero;
            if (distance < 1e-4f) return Vector3.up;
            return away / distance * (1f - distance / clearance);
        }

        /// <summary>
        /// Aggressività del predatore: in <c>Stalk</c> e in <c>Hunt</c>, finché percepisce la luce o il rumore del sub,
        /// sale di <c>aggressionRate</c> al secondo per la luce forte e il flash, di metà per la luce debole e il
        /// rumore; altrimenti scende di <c>aggressionDecay</c>. Gli stimoli che non vengono dal sub non contano. In
        /// caccia conta anche lei: il sub che illumina il predatore mentre caccia lo distoglie dalla preda.
        /// </summary>
        private void UpdateAggression(float deltaTime)
        {
            bool provoked = (state == CreatureState.Stalk || state == CreatureState.Hunt) && interest.FromPlayer &&
                            Time.time - lastInterestTime <= StalkStimulusMemory;
            float rate = -profile.aggressionDecay;
            if (provoked)
            {
                bool strong = interest.Category == StimulusCategory.StrongLight ||
                              interest.Category == StimulusCategory.Flash;
                rate = profile.aggressionRate * (strong ? 1f : 0.5f);
            }
            aggression = Mathf.Clamp01(aggression + rate * deltaTime);
        }

        /// <summary>
        /// Rotta dell'orbita attorno al sub: tangente al cerchio attorno al busto più una correzione verso il raggio,
        /// che va da <c>stalkRadius</c> al buio a <c>strikeRadius</c> ad aggressività piena e respira del ±20% con
        /// un rumore lento. Il verso si inverte ogni 8-20 s. La quota oscilla di ±1 m attorno al busto, mai sotto
        /// due raggi del corpo dal fondale.
        /// </summary>
        private Vector3 StalkHeading(Vector3 position, out float targetY)
        {
            float now = Time.time;
            if (now >= nextOrbitFlipTime) FlipOrbit(now, true);

            Vector3 centre = manager.PlayerPosition + Vector3.up * StalkCentreHeight;
            Vector3 outward = position - centre;
            outward.y = 0f;
            float distance = outward.magnitude;
            outward = distance > 0.01f ? outward / distance : new Vector3(-heading.x, 0f, -heading.z).normalized;

            float breathing = Mathf.PerlinNoise(noiseSeed, now * 0.1f) * 2f - 1f;
            float radius = Mathf.Lerp(profile.stalkRadius, profile.strikeRadius, aggression) * (1f + 0.2f * breathing);
            Vector3 tangent = Vector3.Cross(Vector3.up, outward) * orbitSign;

            float bob = Mathf.PerlinNoise(noiseSeed + 50f, now * 0.07f) * 2f - 1f;
            targetY = centre.y + bob * StalkHeightSwing;
            if (!float.IsNaN(floorY)) targetY = Mathf.Max(targetY, floorY + profile.bodyRadius * 2f);

            return tangent + outward * Mathf.Clamp((radius - distance) * 0.5f, -1.5f, 1.5f);
        }

        /// <summary>Inverte (se richiesto) il verso dell'orbita e programma il prossimo cambio fra 8 e 20 s.</summary>
        private void FlipOrbit(float now, bool invert)
        {
            if (invert) orbitSign = -orbitSign;
            lastOrbitFlipTime = now;
            nextOrbitFlipTime = now + Random.Range(OrbitFlipMin, OrbitFlipMax);
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
        /// Componente verticale della rotta verso <paramref name="targetY"/> (di solito l'altezza preferita sopra il
        /// fondale, in Inspect la quota della sorgente), ma mai sopra il soffitto del nido. Sopra il soffitto scende
        /// sempre, anche dentro la tolleranza.
        /// </summary>
        private float HeightCorrection(float y, float targetY)
        {
            float target = Mathf.Min(targetY, ceilingY);
            float error = target - y;
            if (y <= ceilingY && Mathf.Abs(error) <= profile.heightTolerance) return 0f;
            return error * 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            if (profile == null) return;
            Vector3 position = transform.position;

            Gizmos.color = state == CreatureState.Flee ? Color.red
                : state == CreatureState.Inspect ? Color.green
                : state == CreatureState.Stalk ? new Color(1f, 0.3f, 0.6f)
                : state == CreatureState.Strike ? new Color(1f, 0f, 0.2f)
                : state == CreatureState.Hunt ? new Color(1f, 0.6f, 0f)
                : Color.cyan;
            Gizmos.DrawLine(position, position + heading * Mathf.Max(1f, speed));
            if (state == CreatureState.Strike && manager != null && manager.HasPlayer)
                Gizmos.DrawLine(position, manager.PlayerPosition + Vector3.up * StrikeTargetHeight);
            if (state == CreatureState.Hunt && preyTarget != null && preyTarget.ActiveIndex >= 0)
                Gizmos.DrawLine(position, preyTarget.transform.position);
            if (state == CreatureState.Stalk && manager != null && manager.HasPlayer)
            {
                // Orbita attuale attorno al busto del sub, più rossa con l'aggressività.
                Gizmos.color = Color.Lerp(new Color(1f, 0.3f, 0.6f, 0.5f), Color.red, aggression);
                Vector3 centre = manager.PlayerPosition + Vector3.up * StalkCentreHeight;
                float radius = Mathf.Lerp(profile.stalkRadius, profile.strikeRadius, aggression);
                Vector3 previous = centre + Vector3.forward * radius;
                for (int i = 1; i <= 32; i++)
                {
                    float angle = i / 32f * 2f * Mathf.PI;
                    Vector3 next = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }
            }
            if (state == CreatureState.Flee)
            {
                Gizmos.color = perceived == StimulusCategory.Noise ? new Color(1f, 0.5f, 0f) : Color.yellow;
                Gizmos.DrawLine(position, threatPosition);
            }
            else if (state == CreatureState.Inspect)
            {
                Gizmos.color = Color.green;
                Vector3 spot = SpotPosition();
                Gizmos.DrawLine(position, spot);
                Gizmos.DrawWireSphere(spot, 0.15f);
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawLine(spot, interest.Position);
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

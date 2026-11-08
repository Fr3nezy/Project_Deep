using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>Fase dell'agguato di <see cref="CreatureAmbush"/>.</summary>
    public enum AmbushPhase
    {
        /// <summary>Ferma sotto il fango, invisibile: ascolta.</summary>
        Buried,
        /// <summary>Si sposta sotto il fango verso un punto davanti alla camera del sub.</summary>
        Burrow,
        /// <summary>Preavviso prima dello scatto (se alertDuration > 0).</summary>
        Alert,
        /// <summary>Scatto coreografato dritto in camera, morso all'arrivo e tenuta a fauci aperte prima della ritirata.</summary>
        Lunge,
        /// <summary>Nuota piano rasente il fondo verso un nuovo posto nella zona.</summary>
        Return,
        /// <summary>Si tuffa nel fango in avanti, finché tutto il corpo è sotto.</summary>
        Bury
    }

    /// <summary>
    /// Predatore all'agguato (lucertola abissale): sta insabbiata e invisibile, sente solo il rumore del sub e scatta
    /// fulminea verso la camera non appena il rumore è dentro la soglia d'attacco. L'attacco è coreografato esattamente
    /// a centro schermo con arrivo a distanza ravvicinata fuori dal near plane, morso all'arrivo e jumpscare audio 2D.
    /// Emette indizi sonori localizzabili a fasce di distanza (tocchi sordi lontani, fruscio continuo vicino). Se non è
    /// nel centro dell'inquadratura non scatta, ma si sposta sotto il fango davanti alla camera.
    /// Guida la <see cref="Creature"/> al posto di Think e Move: <see cref="Creature.Tick"/> chiama <see cref="Step"/>
    /// con la cadenza del LOD del manager. Suoni e particelle sono di debug.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Creature))]
    public sealed class CreatureAmbush : MonoBehaviour
    {
        [Header("Agguato")]
        [Tooltip("Distanza orizzontale massima (m) fra il punto di emersione e il punto rumoroso per scattare.")]
        [SerializeField, Min(0.5f)] private float strikeRange = 4.5f;

        [Tooltip("Secondi fra l'allerta e lo scatto: 0 = scatto immediato appena sente il rumore a portata.")]
        [SerializeField, Min(0f)] private float alertDuration = 0f;

        [Tooltip("Altezza di fallback (m) sopra la posizione del sub a cui mira lo scatto se manca la camera (la testa).")]
        [SerializeField, Min(0f)] private float lungeTargetHeight = 1.48f;

        [Tooltip("Distanza (m) davanti alla camera a cui si arresta la testa del predatore all'arrivo dello scatto.")]
        [SerializeField, Min(0.3f)] private float faceDistance = 0.7f;

        [Tooltip("Secondi di tenuta della testa davanti alla camera con le fauci spalancate prima del ritorno.")]
        [SerializeField, Min(0f)] private float faceHold = 0.35f;

        [Tooltip("Intervallo orizzontale e verticale (viewport 0..1) considerato centrale per l'emersione.")]
        [SerializeField] private Vector2 viewportCenterRange = new Vector2(0.2f, 0.8f);

        [Tooltip("Distanza (m) della bocca davanti alla radice (la nuca), per il morso.")]
        [SerializeField, Min(0f)] private float mouthOffset = 0.45f;

        [Tooltip("Secondi per cui ricorda l'ultimo punto rumoroso: se in questo tempo il sub la porta nell'inquadratura girandosi, scatta lì.")]
        [SerializeField, Min(0f)] private float noiseMemory = 4f;

        [Header("Fasce indizi audio")]
        [Tooltip("Distanza massima (m) entro cui emette occasionali tocchi sordi nella sabbia.")]
        [SerializeField, Min(5f)] private float farClueDistance = 11f;

        [Tooltip("Intervallo (s) fra i tocchi di sabbia nella fascia lontana (minimo, massimo).")]
        [SerializeField] private Vector2 tapInterval = new Vector2(3f, 6f);

        [Tooltip("Distanza massima (m) entro cui si sente il fruscio continuo leggerissimo di sabbia.")]
        [SerializeField, Min(1f)] private float nearClueDistance = 6.5f;

        [Header("Sotto il fango")]
        [Tooltip("Profondità (m) della radice sotto il fondale da insabbiata.")]
        [SerializeField, Min(0.1f)] private float buryDepth = 1.2f;

        [Tooltip("Layer del fango: si insabbia solo dove il fondale colpito è di questi layer (non sulle rocce).")]
        [SerializeField] private LayerMask mudMask = 1 << 10;

        [Tooltip("Velocità (m/s) sotto il fango. Più del passo del sub (1.6), se no non gli arriva mai davanti.")]
        [SerializeField, Min(0.1f)] private float burrowSpeed = 3.5f;

        [Tooltip("Distanza (m) davanti alla camera del sub in cui si riposiziona.")]
        [SerializeField, Min(0.5f)] private float aheadDistance = 3f;

        [Tooltip("Secondi minimi fra due avvisi (rumore senza posto valido).")]
        [SerializeField, Min(0f)] private float warnCooldown = 6f;

        [Tooltip("Secondi del tuffo nel fango dopo il ritorno.")]
        [SerializeField, Min(0.1f)] private float buryDuration = 2.5f;

        [Tooltip("Raggio (m) in cui sceglie il nuovo posto dopo lo scatto.")]
        [SerializeField, Min(0.5f)] private float returnRadius = 4f;

        [Header("Effetti e suoni (debug)")]
        [Tooltip("Materiale delle particelle di sedimento. Vuoto = URP Particles/Unlit creato a runtime (solo editor).")]
        [SerializeField] private Material puffMaterial;

        [Tooltip("Suoni sintetici di debug per gli indizi e l'attacco.")]
        [SerializeField] private bool debugSounds = true;

        [Header("Suoni di design")]
        [Tooltip("Suono dello scatto (sibilo, risucchio, implosione, schiocco), 2D a pieno volume. Vuoto = jumpscare sintetico.")]
        [SerializeField] private AudioClip attackClip;

        [Tooltip("Grugniti della vescica natatoria, uno a caso al posto dei tocchi della fascia lontana. Vuoto = tocchi sintetici.")]
        [SerializeField] private AudioClip[] growlClips;

        [Tooltip("Volume dei grugniti (0-1).")]
        [SerializeField, Range(0f, 1f)] private float growlVolume = 0.6f;

        private const float ArriveDistance = 0.3f;
        private const float RidgePuffInterval = 0.35f;
        private const float BurrowMaxTime = 8f;
        private const float ReturnMaxTime = 15f;
        private const float ReturnClimbSpeed = 0.5f;

        private Creature creature;
        private CreatureManager manager;
        private CreatureProfile profile;
        private SwimChain chain;
        private Renderer[] bodyRenderers;
        private Vector3 home;
        private float leash;

        private AmbushPhase phase;
        private float phaseStart;
        private Vector3 position;
        private Vector3 heading = Vector3.forward;
        private float speed;
        private float floorY;
        private bool visible = true;
        private float hideAt = float.PositiveInfinity;

        private Vector3 noisePoint;
        private float noiseTime = float.NegativeInfinity;
        private float cooldownUntil;
        private float warnUntil;
        private float nextTapTime;
        private float nextRidgePuff;
        private Vector3 target;
        private Vector3 lungeStartPos;
        private float lungeDuration;
        private Vector3 diveTarget;

        private float DiveLength => (chain != null ? chain.ChainLength : 2f) * 1.1f;
        private bool bitten;
        private float alertFromY;
        private float buryFromY;

        private ParticleSystem fx;
        private AudioSource loopSource;
        private AudioSource jumpscareSource;
        private static AudioClip tapClip, rustleClip, jumpscareClip, warnClip, puffClip;

        /// <summary>Fase attuale.</summary>
        public AmbushPhase Phase => phase;

        /// <summary>Ultimo punto rumoroso sentito (piedi del sub).</summary>
        public Vector3 LastNoisePoint => noisePoint;

        /// <summary>Istante dell'ultimo rumore sentito; -infinito se nessuno.</summary>
        public float LastNoiseTime => noiseTime;

        /// <summary>Portata massima indizio lontano (m).</summary>
        public float FarClueDistance => farClueDistance;

        /// <summary>Portata massima fruscio continuo vicino (m).</summary>
        public float NearClueDistance => nearClueDistance;

        /// <summary>Distanza d'arresto della testa davanti alla camera (m).</summary>
        public float FaceDistance => faceDistance;

        /// <summary>Secondi di tenuta della testa a fauci aperte davanti alla camera.</summary>
        public float FaceHold => faceHold;

        /// <summary>Intervallo centrale di viewport per l'emersione (0..1).</summary>
        public Vector2 ViewportCenterRange => viewportCenterRange;

        private void Awake()
        {
            chain = GetComponent<SwimChain>();
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
            CreateDebugOutput();
        }

        private void OnDisable()
        {
            if (loopSource != null) loopSource.Stop();
            if (jumpscareSource != null) jumpscareSource.Stop();
        }

        private void OnDestroy()
        {
            if (fx != null) Destroy(fx.gameObject);
        }

        /// <summary>Chiamata da <see cref="Creature.OnSpawn"/>: sceglie il posto e la insabbia subito.</summary>
        internal void Begin(Creature owner, CreatureManager creatureManager, Vector3 homePosition, float leashRadius)
        {
            creature = owner;
            manager = creatureManager;
            profile = owner.Profile;
            home = homePosition;
            leash = leashRadius;
            noiseTime = float.NegativeInfinity;
            cooldownUntil = warnUntil = 0f;
            float now = Time.time;
            nextTapTime = now + Random.Range(tapInterval.x, tapInterval.y);

            Vector3 spot = TryFloor(transform.position, out Vector3 floor) && InZone(floor) ? floor : RandomSpot(home, leash);
            floorY = spot.y;
            position = spot + Vector3.down * buryDepth;
            heading = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            SetVisible(false);
            Enter(AmbushPhase.Buried, now);
            Write();
        }

        /// <summary>Un passo: ascolto nei frame di ragionamento, poi la fase.</summary>
        internal void Step(float deltaTime, bool think)
        {
            float now = Time.time;
            if (think) Listen(now);

            switch (phase)
            {
                case AmbushPhase.Buried: StepBuried(now, think); break;
                case AmbushPhase.Burrow: StepBurrow(now, think, deltaTime); break;
                case AmbushPhase.Alert: StepAlert(now, deltaTime); break;
                case AmbushPhase.Lunge: StepLunge(now, deltaTime); break;
                case AmbushPhase.Return: StepReturn(now, deltaTime); break;
                case AmbushPhase.Bury: StepBury(now, deltaTime); break;
            }

            if (visible && now >= hideAt) SetVisible(false);
            Write();
        }

        // shortcut: sente solo il rumore del sub (FromPlayer); con la dimostrazione dell'isopode dovrà sentire anche
        // le creature, ma il viperfish che caccia emette rumore ogni 0.15 s e la farebbe scattare di continuo.
        private void Listen(float now)
        {
            Vector3 ear = new Vector3(position.x, floorY, position.z);
            IReadOnlyList<CreatureStimulus> stimuli = manager.Stimuli;
            for (int i = 0; i < stimuli.Count; i++)
            {
                CreatureStimulus s = stimuli[i];
                if (s.Category != StimulusCategory.Noise || !s.FromPlayer) continue;
                float reach = s.Radius * profile.hearingSensitivity;
                if (FlatDistance(s.Position, ear) > reach) continue;
                noisePoint = s.Position;
                noiseTime = now;
            }
        }

        private bool Heard(float now) => now - noiseTime <= noiseMemory;

        private void StepBuried(float now, bool think)
        {
            speed = 0f;
            Vector3 emerge = new Vector3(position.x, floorY, position.z);

            UpdateBuriedAudio(now, emerge);

            if (!think || !Heard(now) || now < cooldownUntil) return;

            if (CanStrikeFrom(emerge))
            {
                TriggerAttack(now);
                return;
            }

            // Se l'emersione non è visibile nel centro dell'inquadratura o il percorso è ostruito, cerca un posto valido davanti
            if (!IsVisibleInView(emerge) || !PathClear(emerge, GetLungeTargetHead()))
            {
                if (TryPlaceAhead(out Vector3 ahead) && FlatDistance(ahead, emerge) > 1f)
                {
                    target = ahead;
                    Enter(AmbushPhase.Burrow, now);
                    return;
                }
                if (now >= warnUntil)
                {
                    // Rumore alle spalle senza posto valido davanti al sub: avviso secco di debug
                    warnUntil = now + warnCooldown;
                    Puff(emerge, 30);
                    Play(warnClip, emerge, 0.4f);
                }
            }
        }

        private void UpdateBuriedAudio(float now, Vector3 emerge)
        {
            if (!debugSounds) return;

            float playerDist = float.PositiveInfinity;
            if (manager != null && manager.HasPlayer)
            {
                playerDist = FlatDistance(emerge, manager.PlayerPosition);
            }

            // Fascia vicina: fruscio di sabbia sottilissimo continuo
            if (playerDist <= nearClueDistance)
            {
                if (loopSource != null)
                {
                    if (!loopSource.isPlaying) loopSource.Play();
                    loopSource.volume = 0.22f;
                }
            }
            else
            {
                if (loopSource != null && loopSource.isPlaying)
                    loopSource.Stop();
            }

            // Fascia lontana: tocchi sordi occasionali nella sabbia
            if (playerDist <= farClueDistance)
            {
                if (now >= nextTapTime)
                {
                    nextTapTime = now + Random.Range(tapInterval.x, tapInterval.y);
                    if (growlClips != null && growlClips.Length > 0)
                        AudioSource.PlayClipAtPoint(growlClips[Random.Range(0, growlClips.Length)], emerge, growlVolume);
                    else
                        Play(tapClip, emerge, 0.3f);
                }
            }
        }

        private void TriggerAttack(float now)
        {
            if (loopSource != null) loopSource.Stop();
            if (alertDuration <= 0.01f)
                Enter(AmbushPhase.Lunge, now);
            else
                Enter(AmbushPhase.Alert, now);
        }

        private void StepBurrow(float now, bool think, float deltaTime)
        {
            speed = burrowSpeed;
            if (think && Heard(now) && TryPlaceAhead(out Vector3 ahead) && FlatDistance(ahead, target) > 1.5f)
                target = ahead;

            Vector3 flat = target - position;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance > 1e-4f) heading = flat / distance;
            position += heading * Mathf.Min(distance, burrowSpeed * deltaTime);
            if (TryFloor(position, out Vector3 floor, false)) floorY = floor.y;
            position.y = floorY - buryDepth;

            // Scia sotto il fango: lieve sbuffo in superficie
            if (now >= nextRidgePuff)
            {
                nextRidgePuff = now + RidgePuffInterval;
                Puff(new Vector3(position.x, floorY, position.z), 4);
            }

            if (distance <= ArriveDistance || now - phaseStart > BurrowMaxTime)
            {
                Enter(AmbushPhase.Buried, now);
                Vector3 emerge = new Vector3(position.x, floorY, position.z);
                if (Heard(now) && CanStrikeFrom(emerge)) TriggerAttack(now);
            }
        }

        private void StepAlert(float now, float deltaTime)
        {
            speed = 0f;
            Vector3 toNoise = noisePoint - position;
            toNoise.y = 0f;
            if (toNoise.sqrMagnitude > 1e-4f)
                heading = Vector3.RotateTowards(heading, toNoise.normalized, 4f * Mathf.PI * deltaTime, 0f);

            float t = Mathf.Clamp01((now - phaseStart) / Mathf.Max(alertDuration * 0.7f, 0.01f));
            position.y = Mathf.Lerp(alertFromY, floorY + 0.1f, Mathf.SmoothStep(0f, 1f, t));
            if (now - phaseStart >= alertDuration) Enter(AmbushPhase.Lunge, now);
        }

        private void StepLunge(float now, float deltaTime)
        {
            float elapsed = now - phaseStart;
            Vector3 targetHead = GetLungeTargetHead();
            Vector3 headPos;

            if (elapsed < lungeDuration)
            {
                float t = Mathf.Clamp01(elapsed / lungeDuration);
                headPos = Vector3.Lerp(lungeStartPos, targetHead, t);
                speed = profile.strikeSpeed;
            }
            else
            {
                headPos = targetHead;
                speed = 0f;
                if (!bitten)
                {
                    bitten = true;
                    Puff(headPos, 35); // nuvola di fango attorno alla testa all'arrivo
                    creature.MarkBite(now);
                    if (manager.HasPlayer) manager.ReportBite(creature);
                }
            }

            Camera cam = manager != null ? manager.ViewCamera : null;
            Vector3 eye = cam != null ? cam.transform.position
                : (manager != null && manager.HasPlayer ? manager.PlayerPosition + Vector3.up * lungeTargetHeight : headPos + Vector3.forward);
            Vector3 toEye = eye - headPos;
            heading = toEye.sqrMagnitude > 1e-4f ? toEye.normalized : (cam != null ? -cam.transform.forward : Vector3.forward);
            position = headPos - heading * mouthOffset;

            if (elapsed >= lungeDuration + faceHold)
            {
                cooldownUntil = now + (bitten ? profile.strikeCooldown : profile.strikeCooldown * 0.5f);
                Enter(AmbushPhase.Return, now);
            }
        }

        private void StepReturn(float now, float deltaTime)
        {
            speed = profile.cruiseSpeed;
            Vector3 flat = target - position;
            flat.y = 0f;
            float distance = flat.magnitude;
            Vector3 desired = distance > 1e-4f ? flat / distance : new Vector3(heading.x, 0f, heading.z).normalized;
            heading = Vector3.RotateTowards(heading, desired, profile.turnRate * Mathf.Deg2Rad * deltaTime, 0f);
            Vector3 move = new Vector3(heading.x, 0f, heading.z);
            // shortcut: nessun evitamento degli ostacoli al ritorno, le zone di fango vanno tenute sgombre; se serve,
            // riusare il probe di Creature.
            position += (move.sqrMagnitude > 1e-6f ? move.normalized : desired) * (speed * deltaTime);
            if (TryFloor(position, out Vector3 floor, false)) floorY = floor.y;
            position.y = Mathf.MoveTowards(position.y, floorY + profile.heightAboveFloor, ReturnClimbSpeed * deltaTime);

            if (distance <= 0.5f || now - phaseStart > ReturnMaxTime) Enter(AmbushPhase.Bury, now);
        }

        private void StepBury(float now, float deltaTime)
        {
            // Tuffo in avanti fino al posto scelto (fango): la radice percorre almeno la lunghezza del corpo, così la
            // catena la segue sotto il fango.
            speed = DiveLength / buryDuration;
            Vector3 forward = diveTarget - position;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : new Vector3(heading.x, 0f, heading.z);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            position += forward * Mathf.Min(speed * deltaTime, FlatDistance(position, diveTarget));
            if (TryFloor(position, out Vector3 floor, false)) floorY = floor.y;
            float t = Mathf.Clamp01((now - phaseStart) / buryDuration);
            position.y = Mathf.Lerp(buryFromY, floorY - buryDepth, Mathf.SmoothStep(0f, 1f, t));
            heading = Vector3.Slerp(forward, (forward + Vector3.down).normalized, 1f - t);
            if (t >= 1f)
            {
                heading = forward;
                hideAt = now + 0.3f;
                Enter(AmbushPhase.Buried, now);
            }
        }

        private Vector3 GetLungeTargetHead()
        {
            Camera cam = manager != null ? manager.ViewCamera : null;
            if (cam != null)
                return cam.transform.position + cam.transform.forward * faceDistance;
            if (manager != null && manager.HasPlayer)
                return manager.PlayerPosition + Vector3.up * lungeTargetHeight;
            return noisePoint + Vector3.up * lungeTargetHeight;
        }

        private void Enter(AmbushPhase next, float now)
        {
            phase = next;
            phaseStart = now;
            Vector3 surface = new Vector3(position.x, floorY, position.z);
            if (loopSource != null && next != AmbushPhase.Burrow && next != AmbushPhase.Buried)
                loopSource.Stop();

            switch (next)
            {
                case AmbushPhase.Buried:
                    break;
                case AmbushPhase.Burrow:
                    nextRidgePuff = now;
                    if (loopSource != null && debugSounds)
                    {
                        loopSource.volume = 0.2f;
                        if (!loopSource.isPlaying) loopSource.Play();
                    }
                    break;
                case AmbushPhase.Alert:
                    SetVisible(true);
                    alertFromY = position.y;
                    Puff(surface, 25);
                    Play(warnClip, surface, 0.5f);
                    break;
                case AmbushPhase.Lunge:
                    bitten = false;
                    SetVisible(true);
                    lungeStartPos = surface;
                    Vector3 targetHead = GetLungeTargetHead();
                    float distToTarget = Vector3.Distance(lungeStartPos, targetHead);
                    lungeDuration = Mathf.Max(0.2f, distToTarget / profile.strikeSpeed);
                    Camera camEye = manager != null ? manager.ViewCamera : null;
                    Vector3 eyePt = camEye != null ? camEye.transform.position : targetHead;
                    Vector3 initToEye = eyePt - lungeStartPos;
                    heading = initToEye.sqrMagnitude > 1e-4f ? initToEye.normalized : (camEye != null ? -camEye.transform.forward : Vector3.forward);
                    position = lungeStartPos - heading * mouthOffset;
                    speed = profile.strikeSpeed;
                    Puff(surface, 50); // nuvola grande di sedimento all'emersione fulminea
                    if (attackClip != null && jumpscareSource != null)
                        jumpscareSource.PlayOneShot(attackClip, 1.0f); // suono di design, anche senza debugSounds
                    else if (debugSounds && jumpscareClip != null && jumpscareSource != null)
                        jumpscareSource.PlayOneShot(jumpscareClip, 1.0f); // jumpscare 2D a pieno volume
                    break;
                case AmbushPhase.Return:
                    noiseTime = float.NegativeInfinity;
                    diveTarget = RandomSpot(position, returnRadius);
                    Vector3 approach = position - diveTarget;
                    approach.y = 0f;
                    approach = approach.sqrMagnitude > 1e-4f ? approach.normalized : -heading;
                    target = diveTarget + approach * DiveLength;
                    break;
                case AmbushPhase.Bury:
                    buryFromY = position.y;
                    Puff(surface, 15);
                    Play(puffClip, surface, 0.04f); // reinsabbiamento quasi inudibile
                    break;
            }

            CreatureState mapped = next == AmbushPhase.Lunge ? CreatureState.Strike
                : next == AmbushPhase.Alert ? CreatureState.Stalk : CreatureState.Wander;
            creature.SetDriven(mapped, speed, heading);
        }

        private void Write()
        {
            Vector3 look = heading.sqrMagnitude > 1e-6f ? heading : Vector3.forward;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(look, Vector3.up));
            CreatureState mapped = phase == AmbushPhase.Lunge ? CreatureState.Strike
                : phase == AmbushPhase.Alert ? CreatureState.Stalk : CreatureState.Wander;
            creature.SetDriven(mapped, speed, heading);
        }

        // Scatto possibile: punto rumoroso entro strikeRange, emersione centrale e percorso libero.
        private bool CanStrikeFrom(Vector3 emerge)
        {
            Vector3 targetHead = GetLungeTargetHead();
            return FlatDistance(emerge, noisePoint) <= strikeRange &&
                   IsVisibleInView(emerge) &&
                   PathClear(emerge, targetHead);
        }

        private bool InCentralViewport(Vector3 worldPoint)
        {
            Camera cam = manager != null ? manager.ViewCamera : null;
            if (cam == null) return false;
            Vector3 vp = cam.WorldToViewportPoint(worldPoint);
            if (vp.z <= cam.nearClipPlane) return false;
            return vp.x >= viewportCenterRange.x && vp.x <= viewportCenterRange.y &&
                   vp.y >= viewportCenterRange.x && vp.y <= viewportCenterRange.y;
        }

        private bool IsVisibleInView(Vector3 floorPoint)
        {
            Camera cam = manager != null ? manager.ViewCamera : null;
            if (cam == null) return false;
            // Il punto di emersione deve trovarsi nel viewport centrale (x e y tra 0.2 e 0.8)
            if (!InCentralViewport(floorPoint)) return false;
            // Linea di vista libera dalla camera fino a poco sopra l'emersione
            return !manager.IsHidden(floorPoint + Vector3.up * 0.2f, 0.15f);
        }

        private bool PathClear(Vector3 emerge, Vector3 targetHead)
        {
            Vector3 from = emerge + Vector3.up * 0.4f;
            Vector3 delta = targetHead - from;
            float distance = delta.magnitude;
            if (distance < 1e-3f) return true;
            return !Physics.SphereCast(from, profile.bodyRadius * 0.5f, delta / distance, out _, distance,
                manager.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        // Posto davanti alla camera del sub nel campo visivo centrale, nella zona e con lo scatto libero.
        private bool TryPlaceAhead(out Vector3 spot)
        {
            spot = default;
            Camera view = manager != null ? manager.ViewCamera : null;
            if (view == null || !manager.HasPlayer) return false;
            Vector3 forward = view.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return false;
            forward.Normalize();
            Vector3 player = manager.PlayerPosition;
            Vector3 targetHead = GetLungeTargetHead();
            for (int i = 0; i < AheadAngles.Length; i++)
            {
                Vector3 candidate = player + Quaternion.Euler(0f, AheadAngles[i], 0f) * forward * aheadDistance;
                if (TryFloor(candidate, out Vector3 floor) && InZone(floor) && IsVisibleInView(floor) &&
                    PathClear(floor, targetHead))
                {
                    spot = floor;
                    return true;
                }
            }
            return false;
        }

        private static readonly float[] AheadAngles = { 0f, 25f, -25f, 45f, -45f };

        private bool InZone(Vector3 point)
        {
            if (BurrowZone.Active.Count > 0) return BurrowZone.AnyContains(point);
            return FlatDistance(point, home) <= leash;
        }

        private Vector3 RandomSpot(Vector3 centre, float radius)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = centre + new Vector3(offset.x, 0f, offset.y);
                if (TryFloor(candidate, out Vector3 floor) && InZone(floor)) return floor;
            }
            return TryFloor(centre, out Vector3 below, false) ? below : centre;
        }

        // Raggio verso il basso da sopra la posizione: da sotto il fango un raggio che parte dalla radice non trova il fondale.
        // needMud: per scegliere un posto conta solo il fango (mudMask); per seguire il fondale mentre si muove va
        // bene qualsiasi superficie.
        private bool TryFloor(Vector3 point, out Vector3 floor, bool needMud = true)
        {
            Vector3 origin = new Vector3(point.x, point.y + buryDepth + 4f, point.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 40f, manager.ObstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                floor = hit.point;
                return !needMud || (mudMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            }
            floor = point;
            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void SetVisible(bool show)
        {
            visible = show;
            hideAt = float.PositiveInfinity;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null) bodyRenderers[i].enabled = show;
        }

        private void Puff(Vector3 at, int count)
        {
            if (fx == null) return;
            var emit = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            fx.Emit(emit, count);
        }

        private void Play(AudioClip clip, Vector3 at, float volume)
        {
            if (debugSounds && clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
        }

        private void CreateDebugOutput()
        {
            var fxObject = new GameObject(name + "_AmbushDebugFX");
            fx = fxObject.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = fx.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startColor = Color.white;
            main.gravityModifier = 0.02f;
            main.maxParticles = 500;
            ParticleSystem.EmissionModule emission = fx.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = fx.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule fade = fx.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule size = fx.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2f));
            var particleRenderer = fxObject.GetComponent<ParticleSystemRenderer>();
            Material material = puffMaterial;
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null) material = CreateSoftParticleMaterial(shader);
            }
            particleRenderer.sharedMaterial = material;

            if (tapClip == null)
            {
                tapClip = Synth("Ambush_Tap", 0.2f, 55f, 30f, 0.65f, 0.04f);
                rustleClip = SynthRustle("Ambush_Rustle", 1.2f);
                jumpscareClip = SynthJumpscare("Ambush_Jumpscare", 1.0f);
                warnClip = Synth("Ambush_Warn", 0.7f, 40f, 32f, 0.35f, 0.25f);
                puffClip = Synth("Ambush_Puff", 0.3f, 70f, 45f, 0.7f, 0.08f);
            }

            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.clip = rustleClip;
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.spatialBlend = 1f;
            loopSource.rolloffMode = AudioRolloffMode.Linear;
            loopSource.minDistance = 1f;
            loopSource.maxDistance = nearClueDistance + 1f;
            loopSource.volume = 0.22f;

            jumpscareSource = gameObject.AddComponent<AudioSource>();
            jumpscareSource.playOnAwake = false;
            jumpscareSource.spatialBlend = 0f; // 2D jumpscare in-ear
            jumpscareSource.volume = 1f;
        }

        private static Material CreateSoftParticleMaterial(Shader shader)
        {
            const int sizePx = 32;
            var texture = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < sizePx; y++)
            for (int x = 0; x < sizePx; x++)
            {
                float dx = (x + 0.5f) / sizePx * 2f - 1f, dy = (y + 0.5f) / sizePx * 2f - 1f;
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
            texture.Apply();

            var material = new Material(shader) { mainTexture = texture, color = new Color(0.32f, 0.28f, 0.24f, 1f) };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(0.32f, 0.28f, 0.24f, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        // Suono di debug: tono che scivola da f0 a f1 mescolato a rumore filtrato, attacco rapido e decadimento.
        private static AudioClip Synth(string clipName, float length, float f0, float f1, float noise, float decay)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * length);
            var data = new float[samples];
            float phaseAcc = 0f, filtered = 0f, peak = 0f;
            var random = new System.Random(clipName.GetHashCode());
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                phaseAcc += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t / length) / rate;
                filtered += ((float)random.NextDouble() * 2f - 1f - filtered) * 0.08f;
                float envelope = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t / decay) * Mathf.Clamp01((length - t) / 0.02f);
                float value = envelope * ((1f - noise) * Mathf.Sin(phaseAcc) + noise * filtered * 4f);
                data[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            if (peak > 0f)
                for (int i = 0; i < samples; i++) data[i] *= 0.9f / peak;
            AudioClip clip = AudioClip.Create(clipName, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Fruscio leggero continuo di sabbia per il loop da insabbiata / burrow, con crossfade per loop pulito.
        private static AudioClip SynthRustle(string clipName, float length)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * length);
            var data = new float[samples];
            float filtered = 0f;
            var random = new System.Random(clipName.GetHashCode());
            for (int i = 0; i < samples; i++)
            {
                float rnd = (float)random.NextDouble() * 2f - 1f;
                filtered += (rnd - filtered) * 0.05f;
                data[i] = filtered * 0.35f;
            }
            int cross = Mathf.CeilToInt(rate * 0.1f);
            for (int i = 0; i < cross; i++)
            {
                float t = i / (float)cross;
                data[i] = Mathf.Lerp(data[samples - cross + i], data[i], t);
            }
            AudioClip clip = AudioClip.Create(clipName, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Suono sintetico di jumpscare: transiente d'impatto secco, stridio/ruggito FM che sale e boato sub-bass.
        private static AudioClip SynthJumpscare(string clipName, float length = 1.0f)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * length);
            var data = new float[samples];
            float phaseCarrier = 0f;
            float phaseMod = 0f;
            float phaseSub = 0f;
            float filteredNoise = 0f;
            float peak = 0f;
            var random = new System.Random(clipName.GetHashCode());

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float progress = t / length;

                // Carrier chirp 260Hz -> 1050Hz
                float fCarrier = Mathf.Lerp(260f, 1050f, Mathf.Pow(progress, 0.7f));
                // Modulatore FM 110Hz -> 240Hz
                float fMod = Mathf.Lerp(110f, 240f, progress);
                phaseMod += 2f * Mathf.PI * fMod / rate;
                float mod = Mathf.Sin(phaseMod) * 3.5f;

                phaseCarrier += 2f * Mathf.PI * (fCarrier + mod * 120f) / rate;
                float screech = Mathf.Sin(phaseCarrier);

                // Sub bass boom 90Hz -> 38Hz
                float fSub = Mathf.Lerp(90f, 38f, Mathf.Sqrt(progress));
                phaseSub += 2f * Mathf.PI * fSub / rate;
                float sub = Mathf.Sin(phaseSub) * 1.2f;

                // Rumore roco filtrato
                float rnd = (float)random.NextDouble() * 2f - 1f;
                filteredNoise += (rnd - filteredNoise) * 0.25f;

                // Transiente secco iniziale nei primi 40 ms
                float snap = t < 0.04f ? (1f - t / 0.04f) * rnd * 2f : 0f;

                // Inviluppo: attacco rapido (3 ms), corpo pieno, decadimento graduale
                float env = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-t * 2.2f) * Mathf.Clamp01((length - t) / 0.05f);

                float sample = snap * 1.5f + env * (screech * 0.6f + sub * 0.7f + filteredNoise * 0.5f);
                data[i] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            if (peak > 0f)
            {
                float inv = 0.95f / peak;
                for (int i = 0; i < samples; i++) data[i] *= inv;
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || creature == null) return;
            Vector3 surface = new Vector3(position.x, floorY, position.z);
            Gizmos.color = phase switch
            {
                AmbushPhase.Buried => new Color(0.55f, 0.4f, 0.25f),
                AmbushPhase.Burrow => Color.yellow,
                AmbushPhase.Alert => new Color(1f, 0.5f, 0f),
                AmbushPhase.Lunge => Color.red,
                _ => Color.cyan
            };
            Gizmos.DrawWireSphere(surface, 0.4f);
            Gizmos.DrawLine(surface, surface + Vector3.up * 2f);
            if (Heard(Time.time))
            {
                Gizmos.DrawLine(surface + Vector3.up * 0.5f, noisePoint);
                Gizmos.DrawWireSphere(noisePoint, 0.25f);
            }
            if (phase == AmbushPhase.Burrow || phase == AmbushPhase.Return) Gizmos.DrawLine(surface, target);
            if (phase == AmbushPhase.Lunge)
            {
                Vector3 targetHead = GetLungeTargetHead();
                Gizmos.DrawLine(lungeStartPos, targetHead);
                Gizmos.DrawWireSphere(targetHead, 0.2f);
            }

            // Cerchi fasce di distanza
            DrawGizmoCircle(surface, strikeRange, new Color(1f, 0.2f, 0.2f, 0.6f));
            DrawGizmoCircle(surface, nearClueDistance, new Color(1f, 0.9f, 0.2f, 0.4f));
            DrawGizmoCircle(surface, farClueDistance, new Color(0.3f, 0.8f, 1f, 0.3f));
        }

        private static void DrawGizmoCircle(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * 2f * Mathf.PI / segments, a1 = (i + 1) * 2f * Mathf.PI / segments;
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                    center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }
        }
    }
}

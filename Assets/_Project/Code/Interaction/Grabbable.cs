using System.Collections.Generic;
using Deeploration.Creatures;
using UnityEngine;

namespace Deeploration.Interaction
{
    /// <summary>
    /// Oggetto fisico che il diver trascina col crosshair (vedi <see cref="PlayerGrab"/>). Sott'acqua: gravità ridotta
    /// e molto attrito, collisione continua, nessuna collisione con la capsula del diver. Se ha un bagliore
    /// (la cella nucleare, Cherenkov) passa da celeste spento ad azzurro acceso quando allerta le creature
    /// (<see cref="AlertsCreatures"/>); lo stimolo per le creature cresce con la velocità.
    /// Va sulla radice dell'oggetto, accanto al Rigidbody; la mesh in un figlio Visual.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Grabbable : MonoBehaviour, IInteractable, IStimulusSource
    {
        /// <summary>Come si muove l'oggetto mentre PlayerGrab lo trascina.</summary>
        public enum GrabMode
        {
            /// <summary>Tirato dal punto afferrato: ruota appeso e può capovolgersi.</summary>
            Free,
            /// <summary>Tirato dal baricentro e tenuto dritto da una molla di rotazione; gira libero solo attorno all'asse verticale.</summary>
            KeepUpright
        }

        [Tooltip("Id confrontato da GrabSocket.acceptedItemId.")]
        [SerializeField] private string itemId = "power_cell";
        [SerializeField] private string promptText = "Tieni premuto il tasto sinistro per trascinare";

        [Header("Presa")]
        [Tooltip("Free: appeso al punto afferrato. KeepUpright: resta dritto mentre è trascinato.")]
        [SerializeField] private GrabMode grabMode = GrabMode.Free;
        [Tooltip("Rigidità della molla che lo tiene dritto (pulsazione, rad/s). Solo KeepUpright.")]
        [SerializeField, Min(0f)] private float uprightFrequency = 8f;

        [Header("Fisica in acqua")]
        [Tooltip("Frazione della gravità che agisce sull'oggetto (1 = come fuori dall'acqua).")]
        [SerializeField, Range(0f, 1f)] private float gravityScale = 0.3f;
        [SerializeField, Min(0f)] private float waterDrag = 3f;
        [SerializeField, Min(0f)] private float waterAngularDrag = 3f;

        [Tooltip("Metri sotto la posizione iniziale oltre i quali l'oggetto torna al punto di partenza (caduto in un crepaccio o attraverso il terreno).")]
        [SerializeField, Min(1f)] private float fallResetDepth = 40f;

        [Tooltip("Secondi per scivolare sullo snap dello slot all'aggancio. 0 = di scatto.")]
        [SerializeField, Min(0f)] private float socketBlendTime = 0.35f;

        // Il bagliore segue AlertsCreatures, non la velocità: acceso solo quando le creature lo vedono come opera
        // del sub, così il giocatore legge la regola dello stealth dal colore.
        [Header("Bagliore (opzionale)")]
        [SerializeField] private Light glowLight;
        [Tooltip("Colore e intensità della luce quando l'oggetto non allerta le creature (ferma, assestata, nell'alloggiamento).")]
        [SerializeField] private Color calmLightColor = new Color(0.55f, 0.8f, 1f);
        [SerializeField, Min(0f)] private float calmLightIntensity = 0.6f;
        [Tooltip("Colore e intensità della luce quando allerta le creature (in mano o mossa dal sub).")]
        [SerializeField] private Color alertLightColor = new Color(0.1f, 0.45f, 1f);
        [SerializeField, Min(0f)] private float alertLightIntensity = 4f;

        [Tooltip("Renderer col materiale CherenkovGlow: _Color passa dal colore calmo a quello d'allerta. Opzionale.")]
        [SerializeField] private Renderer glowRenderer;
        [Tooltip("_Color HDR del materiale a riposo: moltiplica la mappa d'emissione (ciano). Bianco tenue = celeste spento.")]
        [SerializeField, ColorUsage(false, true)] private Color calmGlowColor = new Color(1f, 1.3f, 1.3f);
        [Tooltip("_Color HDR in allerta: blu dominante = azzurro saturo e acceso.")]
        [SerializeField, ColorUsage(false, true)] private Color alertGlowColor = new Color(0.5f, 4f, 8f);
        [Tooltip("Secondi per passare da un bagliore all'altro (costante di tempo).")]
        [SerializeField, Min(0.01f)] private float glowBlendTime = 0.2f;
        // A riposo la cella accesa sembrava spenta fra i riflessi delle scariche (playtest 2026-11-23): il bagliore calmo pulsa.
        [Tooltip("Ampiezza della pulsazione del bagliore calmo: intensità fra (1 - x) e (1 + x). 0 = fisso.")]
        [SerializeField, Range(0f, 1f)] private float calmPulseDepth = 0.6f;
        [Tooltip("Pulsazioni al secondo del bagliore calmo.")]
        [SerializeField, Min(0f)] private float calmPulseRate = 0.6f;

        [Header("Ronzio (solo con glowRenderer)")]
        [Tooltip("Loop del ronzio della cella accesa. Vuoto = ronzio sintetico a 60 Hz.")]
        [SerializeField] private AudioClip humClip;
        [Tooltip("Volume a riposo (segue la pulsazione) e in allerta. 0 a riposo = nessun ronzio.")]
        [SerializeField, Range(0f, 1f)] private float humVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float humAlertVolume = 0.6f;
        [Tooltip("Distanza (m) oltre la quale il ronzio non si sente più.")]
        [SerializeField, Min(1f)] private float humMaxDistance = 8f;

        [Tooltip("Portata dello stimolo di luce da fermo (m), per una creatura con sensibilità 1. 0 = nessuno stimolo.")]
        [SerializeField, Min(0f)] private float stimulusReach = 6f;
        [Tooltip("Metri di portata aggiunti per ogni m/s di velocità.")]
        [SerializeField, Min(0f)] private float stimulusPerSpeed = 6f;
        [Tooltip("Velocità (m/s) oltre la quale l'oggetto lasciato andare conta ancora come mosso dal sub.")]
        [SerializeField, Min(0f)] private float stimulusMoveThreshold = 0.2f;
        [Tooltip("Secondi dopo il rilascio in cui il movimento dell'oggetto conta come opera del sub.")]
        [SerializeField, Min(0f)] private float movedByPlayerWindow = 10f;
        private float lastHeldTime = float.NegativeInfinity;

        private Rigidbody body;
        private CreatureManager creatureManager;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private float glowBlend;
        private MaterialPropertyBlock glowBlock;
        private AudioSource hum;
        private static AudioClip synthHum;
        private CharacterController playerCollider;
        private bool ignoringPlayer;

        private Transform socketTarget;
        private Vector3 socketFromPosition;
        private Quaternion socketFromRotation;
        private Quaternion socketToRotation;
        private float socketStartTime;

        public string ItemId => itemId;
        public Rigidbody Body => body;
        public bool IsHeld { get; internal set; }
        public bool IsSocketed { get; private set; }
        /// <summary>Slot che tiene l'oggetto, o null. Lo scrive <see cref="GrabSocket"/>.</summary>
        public GrabSocket Holder { get; internal set; }
        public string PromptText => promptText;
        public bool KeepsUpright => grabMode == GrabMode.KeepUpright;

        /// <summary>
        /// Vero quando le creature contano l'oggetto come opera del sub: in mano, o ancora in movimento entro
        /// movedByPlayerWindow dal rilascio (non quando si assesta da solo né nell'alloggiamento).
        /// </summary>
        public bool AlertsCreatures => IsHeld || (!IsSocketed && Speed > stimulusMoveThreshold &&
                                                   Time.time - lastHeldTime < movedByPlayerWindow);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = waterDrag;
            body.angularDamping = waterAngularDrag;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            startPosition = transform.position;
            startRotation = transform.rotation;
            if (glowRenderer != null) glowBlock = new MaterialPropertyBlock();
            // Solo le celle accese hanno glowRenderer: la scarica resta muta.
            if (glowRenderer != null && humVolume > 0f)
            {
                if (humClip == null && synthHum == null) synthHum = SynthHum();
                hum = gameObject.AddComponent<AudioSource>();
                hum.clip = humClip != null ? humClip : synthHum;
                hum.loop = true;
                hum.spatialBlend = 1f;
                hum.rolloffMode = AudioRolloffMode.Linear;
                hum.minDistance = 0.5f;
                hum.maxDistance = humMaxDistance;
                hum.volume = 0f;
                hum.time = Random.Range(0f, hum.clip.length * 0.9f);
                hum.Play();
            }
        }

        private void Start()
        {
            PlayerGrab grab = FindAnyObjectByType<PlayerGrab>();
            if (grab != null) playerCollider = grab.GetComponent<CharacterController>();
        }

        // La capsula del diver è una testa volante senza massa: gli oggetti la ignorano. Unity dimentica
        // IgnoreCollision quando un collider si spegne (seduto nella capsula, uscita, respawn), quindi si riapplica.
        private void RefreshPlayerIgnore()
        {
            bool on = playerCollider != null && playerCollider.enabled;
            if (on && !ignoringPlayer)
                foreach (Collider own in GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(playerCollider, own);
            ignoringPlayer = on;
        }

        private void OnEnable()
        {
            creatureManager = FindAnyObjectByType<CreatureManager>();
            if (creatureManager != null && stimulusReach > 0f) creatureManager.AddSource(this);
        }

        private void OnDisable()
        {
            if (creatureManager != null) creatureManager.RemoveSource(this);
        }

        private void FixedUpdate()
        {
            RefreshPlayerIgnore();
            if (socketTarget != null) BlendIntoSocket();
            if (body.isKinematic) return;
            body.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
            if (IsHeld && KeepsUpright) AlignUp(Vector3.up, uprightFrequency);

            if (transform.position.y < startPosition.y - fallResetDepth)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = startPosition;
                body.rotation = startRotation;
            }
        }

        private void Update()
        {
            if (IsHeld) lastHeldTime = Time.time;
            glowBlend = Mathf.Lerp(glowBlend, AlertsCreatures ? 1f : 0f, 1f - Mathf.Exp(-Time.deltaTime / glowBlendTime));
            float pulse = 1f + calmPulseDepth * Mathf.Sin(Time.time * calmPulseRate * 2f * Mathf.PI);
            if (glowLight != null)
            {
                glowLight.color = Color.Lerp(calmLightColor, alertLightColor, glowBlend);
                glowLight.intensity = Mathf.Lerp(calmLightIntensity * pulse, alertLightIntensity, glowBlend);
            }
            if (glowRenderer != null)
            {
                glowRenderer.GetPropertyBlock(glowBlock);
                // SetVector e non SetColor: in Linear SetColor eleva a 2.2 e i valori HDR esplodono (come CreatureGlow).
                glowBlock.SetVector(ColorId, Color.Lerp(calmGlowColor * pulse, alertGlowColor, glowBlend));
                glowRenderer.SetPropertyBlock(glowBlock);
            }
            if (hum != null) hum.volume = Mathf.Lerp(humVolume * (0.6f + 0.4f * pulse), humAlertVolume, glowBlend);
        }

        // Ronzio elettrico: 60 Hz con armoniche e un filo di fruscio, 1 s esatto così il loop non ha salti.
        private static AudioClip SynthHum()
        {
            const int rate = 44100;
            var data = new float[rate];
            var random = new System.Random(91);
            float peak = 0f, lp = 0f;
            for (int i = 0; i < rate; i++)
            {
                float t = i / (float)rate;
                lp += (((float)random.NextDouble() * 2f - 1f) - lp) * 0.05f;
                float value = Mathf.Sin(2f * Mathf.PI * 60f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 120f * t) +
                              0.3f * Mathf.Sin(2f * Mathf.PI * 180f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 300f * t) + lp * 0.4f;
                data[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            for (int i = 0; i < rate; i++) data[i] *= 0.9f / peak;
            AudioClip clip = AudioClip.Create("Cell_Hum", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private float Speed => body.isKinematic ? 0f : body.linearVelocity.magnitude;

        /// <summary>
        /// Blocca l'oggetto nello slot: diventa cinematico, non si trascina più e scivola sullo snap in socketBlendTime.
        /// Allinea solo l'asse su a quello dello snap e tiene la rotazione attorno all'asse, così non gira su se stesso.
        /// </summary>
        public void Socket(Transform snapPoint)
        {
            IsSocketed = true;
            body.isKinematic = true;
            socketTarget = snapPoint;
            socketFromPosition = body.position;
            socketFromRotation = body.rotation;
            socketToRotation = Quaternion.FromToRotation(socketFromRotation * Vector3.up, snapPoint.up) * socketFromRotation;
            socketStartTime = Time.time;
            if (socketBlendTime <= 0f) BlendIntoSocket();
        }

        /// <summary>Libera l'oggetto dallo slot: torna dinamico e trascinabile. Lo chiama <see cref="GrabSocket.Eject"/>.</summary>
        public void Unsocket()
        {
            IsSocketed = false;
            Holder = null;
            socketTarget = null;
            body.isKinematic = false;
        }

        private void BlendIntoSocket()
        {
            float t = socketBlendTime <= 0f ? 1f : Mathf.Clamp01((Time.time - socketStartTime) / socketBlendTime);
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 position = Vector3.Lerp(socketFromPosition, socketTarget.position, k);
            Quaternion rotation = Quaternion.Slerp(socketFromRotation, socketToRotation, k);
            if (t >= 1f)
            {
                // A fine corsa scrive anche il transform: un rigidbody cinematico interpolato altrimenti resta indietro di un frame.
                transform.SetPositionAndRotation(position, rotation);
                socketTarget = null;
            }
            body.MovePosition(position);
            body.MoveRotation(rotation);
        }

        /// <summary>
        /// Molla smorzata (critica) che porta l'asse su dell'oggetto verso <paramref name="up"/> per un passo fisico.
        /// Non tocca la rotazione attorno a <paramref name="up"/>. Da chiamare in FixedUpdate; nulla se cinematico.
        /// </summary>
        public void AlignUp(Vector3 up, float frequency)
        {
            if (body.isKinematic || frequency <= 0f) return;
            Vector3 axis = Vector3.Cross(transform.up, up);
            float angle = Vector3.Angle(transform.up, up) * Mathf.Deg2Rad;
            Vector3 spring = axis.sqrMagnitude > 1e-8f ? axis.normalized * (angle * frequency * frequency) : Vector3.zero;
            Vector3 tilting = Vector3.ProjectOnPlane(body.angularVelocity, up);
            body.AddTorque(spring - tilting * (2f * frequency), ForceMode.Acceleration);
        }

        public bool CanInteract(GameObject user) => !IsSocketed;

        // Il trascinamento lo fa PlayerGrab col tasto sinistro: E mostra solo il prompt.
        public void Interact(GameObject user) { }

        public void EmitStimuli(List<CreatureStimulus> output)
        {
            if (!isActiveAndEnabled || stimulusReach <= 0f) return;
            // Il manager chiama EmitStimuli a ogni frame: l'ultimo frame tenuto è l'istante del rilascio.
            if (IsHeld) lastHeldTime = Time.time;
            output.Add(CreatureStimulus.PointLight(transform.position, stimulusReach + Speed * stimulusPerSpeed,
                StimulusCategory.WeakLight, AlertsCreatures));
        }
    }
}

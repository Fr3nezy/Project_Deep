using System.Collections.Generic;
using Deeploration.Creatures;
using UnityEngine;

namespace Deeploration.Interaction
{
    /// <summary>
    /// Oggetto fisico che il diver trascina col crosshair (vedi <see cref="PlayerGrab"/>). Sott'acqua: gravità ridotta
    /// e molto attrito, collisione continua, nessuna collisione con la capsula del diver. Se ha una luce di bagliore
    /// (la cella nucleare, Cherenkov) la luce e lo stimolo per le creature crescono con la velocità.
    /// Va sulla radice dell'oggetto, accanto al Rigidbody; la mesh in un figlio Visual.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Grabbable : MonoBehaviour, IInteractable, IStimulusSource
    {
        [Tooltip("Id confrontato da GrabSocket.acceptedItemId.")]
        [SerializeField] private string itemId = "power_cell";
        [SerializeField] private string promptText = "Tieni premuto il tasto sinistro per trascinare";

        [Header("Fisica in acqua")]
        [Tooltip("Frazione della gravità che agisce sull'oggetto (1 = come fuori dall'acqua).")]
        [SerializeField, Range(0f, 1f)] private float gravityScale = 0.3f;
        [SerializeField, Min(0f)] private float waterDrag = 3f;
        [SerializeField, Min(0f)] private float waterAngularDrag = 3f;

        [Tooltip("Metri sotto la posizione iniziale oltre i quali l'oggetto torna al punto di partenza (caduto in un crepaccio o attraverso il terreno).")]
        [SerializeField, Min(1f)] private float fallResetDepth = 40f;

        [Tooltip("Secondi per scivolare sullo snap dello slot all'aggancio. 0 = di scatto.")]
        [SerializeField, Min(0f)] private float socketBlendTime = 0.35f;

        [Header("Bagliore (opzionale)")]
        [SerializeField] private Light glowLight;
        [Tooltip("Intensità aggiunta alla luce per ogni m/s di velocità.")]
        [SerializeField, Min(0f)] private float glowPerSpeed = 1.5f;

        [Tooltip("Renderer col materiale CherenkovGlow: _Intensity cresce con la velocità come la luce. Opzionale.")]
        [SerializeField] private Renderer glowRenderer;
        [Tooltip("Frazione dell'intensità del materiale aggiunta per ogni m/s di velocità.")]
        [SerializeField, Min(0f)] private float glowRendererPerSpeed = 0.5f;

        [Tooltip("Portata dello stimolo di luce da fermo (m), per una creatura con sensibilità 1. 0 = nessuno stimolo.")]
        [SerializeField, Min(0f)] private float stimulusReach = 6f;
        [Tooltip("Metri di portata aggiunti per ogni m/s di velocità.")]
        [SerializeField, Min(0f)] private float stimulusPerSpeed = 6f;

        private Rigidbody body;
        private CreatureManager creatureManager;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private float baseGlow;
        private float baseRendererGlow;
        private MaterialPropertyBlock glowBlock;
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
        public string PromptText => promptText;

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
            if (glowLight != null) baseGlow = glowLight.intensity;
            if (glowRenderer != null)
            {
                glowBlock = new MaterialPropertyBlock();
                baseRendererGlow = glowRenderer.sharedMaterial.GetFloat(IntensityId);
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
            if (glowLight != null) glowLight.intensity = baseGlow + Speed * glowPerSpeed;
            if (glowRenderer != null)
            {
                glowRenderer.GetPropertyBlock(glowBlock);
                glowBlock.SetFloat(IntensityId, baseRendererGlow * (1f + Speed * glowRendererPerSpeed));
                glowRenderer.SetPropertyBlock(glowBlock);
            }
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

        public bool CanInteract(GameObject user) => !IsSocketed;

        // Il trascinamento lo fa PlayerGrab col tasto sinistro: E mostra solo il prompt.
        public void Interact(GameObject user) { }

        public void EmitStimuli(List<CreatureStimulus> output)
        {
            if (!isActiveAndEnabled || stimulusReach <= 0f) return;
            output.Add(CreatureStimulus.PointLight(transform.position, stimulusReach + Speed * stimulusPerSpeed,
                StimulusCategory.WeakLight, IsHeld));
        }
    }
}

using Deeploration.Interaction;
using UnityEngine;

namespace Deeploration.Player
{
    /// <summary>
    /// Mirino del casco: un punto sempre visibile e tenue, più un anello che si apre quando sotto il centro dello
    /// sguardo c'è qualcosa da usare ([E] di <see cref="PlayerInteraction"/>) o da trascinare (<see cref="PlayerGrab"/>),
    /// e resta pieno mentre si trascina. Sono quad 3D davanti alla camera, colorati con un MaterialPropertyBlock.
    /// </summary>
    public sealed class InteractionCrosshair : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private PlayerGrab grab;

        [SerializeField] private Renderer dot;
        [SerializeField] private Renderer ring;

        [SerializeField] private Color color = new Color(1f, 0.6f, 0.15f, 1f);
        [Tooltip("Colore quando l'oggetto trascinato è allineato in uno slot (GrabSocket.IsAligned).")]
        [SerializeField] private Color alignedColor = new Color(0.3f, 1f, 0.45f, 1f);
        [SerializeField, Range(0f, 1f)] private float idleDotAlpha = 0.35f;
        [Tooltip("Scala dell'anello senza bersaglio, frazione di quella piena.")]
        [SerializeField, Range(0f, 1f)] private float ringIdleScale = 0.4f;
        [Tooltip("Velocità di apertura e chiusura, frazione al secondo.")]
        [SerializeField, Min(0f)] private float response = 10f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock block;
        private Vector3 ringFullScale;
        private float amount;
        private GrabSocket[] sockets;
        private Color current;

        public bool HasTarget { get; private set; }

        private void Awake()
        {
            if (interaction == null) interaction = GetComponentInParent<PlayerInteraction>();
            if (grab == null) grab = GetComponentInParent<PlayerGrab>();
            block = new MaterialPropertyBlock();
            if (ring != null) ringFullScale = ring.transform.localScale;
            sockets = FindObjectsByType<GrabSocket>(FindObjectsSortMode.None);
            current = color;
        }

        private void LateUpdate()
        {
            HasTarget = (interaction != null && interaction.HasTarget) ||
                        (grab != null && (grab.Held != null || grab.FindTarget(out _) != null));
            amount = Mathf.MoveTowards(amount, HasTarget ? 1f : 0f, response * Time.deltaTime);

            bool aligned = false;
            if (grab != null && grab.Held != null)
                foreach (GrabSocket socket in sockets)
                    if (socket != null && socket.IsAligned) { aligned = true; break; }
            current = aligned ? alignedColor : color;

            SetColor(dot, Mathf.Lerp(idleDotAlpha, 1f, amount));
            if (ring != null)
            {
                ring.transform.localScale = ringFullScale * Mathf.Lerp(ringIdleScale, 1f, amount);
                SetColor(ring, amount);
                ring.enabled = amount > 0.001f;
            }
        }

        private void SetColor(Renderer target, float alpha)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(current.r, current.g, current.b, current.a * alpha));
            target.SetPropertyBlock(block);
        }
    }
}

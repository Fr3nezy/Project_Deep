using Deeploration.Interaction;
using UnityEngine;

namespace Deeploration.Player
{
    /// <summary>
    /// Mirino del casco: un punto sempre visibile e tenue, un anello esagonale che si apre quando sotto il centro
    /// dello sguardo c'è qualcosa da usare ([E] di <see cref="PlayerInteraction"/>) e due ganasce che entrano ai lati
    /// quando c'è qualcosa da trascinare (<see cref="PlayerGrab"/>) e si chiudono sul punto mentre lo si trascina.
    /// Se il bersaglio è sia da usare sia da trascinare vincono le ganasce: insieme all'anello non si leggevano.
    /// Sono quad 3D davanti alla camera, colorati con un MaterialPropertyBlock.
    /// </summary>
    public sealed class InteractionCrosshair : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private PlayerGrab grab;

        [SerializeField] private Renderer dot;
        [SerializeField] private Renderer ring;
        [Tooltip("Ganasce: quad posati nella posa di mira (bersaglio trascinabile), uno per lato del nucleo.")]
        [SerializeField] private Renderer jawLeft;
        [SerializeField] private Renderer jawRight;

        [SerializeField] private Color color = new Color(1f, 0.6f, 0.15f, 1f);
        [Tooltip("Colore quando l'oggetto trascinato è allineato in uno slot (GrabSocket.IsAligned).")]
        [SerializeField] private Color alignedColor = new Color(0.3f, 1f, 0.45f, 1f);
        [SerializeField, Range(0f, 1f)] private float idleDotAlpha = 0.35f;
        [Tooltip("Scala dell'anello senza bersaglio, frazione di quella piena.")]
        [SerializeField, Range(0f, 1f)] private float ringIdleScale = 0.4f;
        [Tooltip("Velocità di apertura e chiusura, frazione al secondo.")]
        [SerializeField, Min(0f)] private float response = 10f;
        [Tooltip("Di quanto le ganasce partono più larghe della posa di mira mentre compaiono, metri locali.")]
        [SerializeField, Min(0f)] private float jawSpread = 0.001f;
        [Tooltip("Di quanto le ganasce si stringono sul nucleo mentre si trascina, metri locali.")]
        [SerializeField, Min(0f)] private float jawClose = 0.001f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock block;
        private Vector3 ringFullScale;
        private float interactAmount;
        private float grabAmount;
        private float heldAmount;
        private Vector3 jawLeftPose;
        private Vector3 jawRightPose;
        private GrabSocket[] sockets;
        private Color current;

        public bool HasTarget { get; private set; }

        private void Awake()
        {
            if (interaction == null) interaction = GetComponentInParent<PlayerInteraction>();
            if (grab == null) grab = GetComponentInParent<PlayerGrab>();
            block = new MaterialPropertyBlock();
            if (ring != null) ringFullScale = ring.transform.localScale;
            if (jawLeft != null) jawLeftPose = jawLeft.transform.localPosition;
            if (jawRight != null) jawRightPose = jawRight.transform.localPosition;
            sockets = FindObjectsByType<GrabSocket>(FindObjectsSortMode.None);
            current = color;
        }

        private void LateUpdate()
        {
            bool interact = interaction != null && interaction.HasTarget;
            bool held = grab != null && grab.Held != null;
            bool grabbable = held || (grab != null && grab.FindTarget(out _) != null);
            HasTarget = interact || grabbable;
            float step = response * Time.deltaTime;
            interactAmount = Mathf.MoveTowards(interactAmount, interact && !grabbable ? 1f : 0f, step);
            grabAmount = Mathf.MoveTowards(grabAmount, grabbable ? 1f : 0f, step);
            heldAmount = Mathf.MoveTowards(heldAmount, held ? 1f : 0f, step);

            bool aligned = false;
            if (held)
                foreach (GrabSocket socket in sockets)
                    if (socket != null && socket.IsAligned) { aligned = true; break; }
            current = aligned ? alignedColor : color;

            SetColor(dot, Mathf.Lerp(idleDotAlpha, 1f, Mathf.Max(interactAmount, grabAmount)));
            if (ring != null)
            {
                ring.transform.localScale = ringFullScale * Mathf.Lerp(ringIdleScale, 1f, interactAmount);
                SetColor(ring, interactAmount);
                ring.enabled = interactAmount > 0.001f;
            }
            // verso l'esterno mentre compaiono, verso il nucleo mentre si trascina
            float jawShift = jawSpread * (1f - grabAmount) - jawClose * heldAmount;
            UpdateJaw(jawLeft, jawLeftPose, -jawShift);
            UpdateJaw(jawRight, jawRightPose, jawShift);
        }

        private void UpdateJaw(Renderer jaw, Vector3 pose, float shiftX)
        {
            if (jaw == null) return;
            jaw.transform.localPosition = pose + new Vector3(shiftX, 0f, 0f);
            SetColor(jaw, grabAmount);
            jaw.enabled = grabAmount > 0.001f;
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

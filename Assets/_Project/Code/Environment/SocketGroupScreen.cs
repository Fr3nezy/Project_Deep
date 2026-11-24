using Deeploration.Interaction;
using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Schermo CRT ambra di un <see cref="SocketGroup"/>, senza testo: un'icona per presa (cella piena se occupata,
    /// sagoma tratteggiata col fulmine che pulsa se vuota, cella barrata che lampeggia per qualche secondo quando la
    /// presa respinge una cella scarica) e l'icona del bersaglio (la Torre), fioca finché il gruppo non è
    /// completo, poi lampeggia e resta accesa. Solo lettura: non tocca prese né gruppo.
    /// </summary>
    public sealed class SocketGroupScreen : MonoBehaviour
    {
        [SerializeField] private SocketGroup group;
        [SerializeField] private Color amber = new Color(1f, 0.6f, 0.15f, 1f);

        [Header("Prese")]
        [Tooltip("Un'icona per presa, nello stesso ordine di SocketGroup.sockets. Le icone in più restano nascoste.")]
        [SerializeField] private Renderer[] slotIcons = new Renderer[0];
        [SerializeField] private Texture2D filledIcon;
        [SerializeField] private Texture2D emptyIcon;
        [Tooltip("Icona mostrata, lampeggiante, quando la presa respinge un oggetto (GrabSocket.onRejected).")]
        [SerializeField] private Texture2D rejectedIcon;
        [SerializeField, Min(0f)] private float rejectedShowTime = 2.5f;
        [Tooltip("Pulsazioni al secondo delle prese vuote.")]
        [SerializeField, Min(0.1f)] private float pulseRate = 1.2f;
        [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.25f;

        [Header("Bersaglio")]
        [SerializeField] private Renderer targetIcon;
        [SerializeField] private Texture2D targetTexture;
        [Tooltip("Alfa dell'icona del bersaglio finché il gruppo non è completo.")]
        [SerializeField, Range(0f, 1f)] private float targetIdleAlpha = 0.2f;
        [SerializeField, Min(0f)] private float blinkDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float blinkRate = 6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private MaterialPropertyBlock block;
        private float completedAt = -1f;
        private float[] rejectedUntil = new float[0];
        private UnityEngine.Events.UnityAction[] rejectHandlers = new UnityEngine.Events.UnityAction[0];

        private void Awake() => block = new MaterialPropertyBlock();

        private void OnEnable()
        {
            if (group == null) return;
            rejectedUntil = new float[group.Total];
            rejectHandlers = new UnityEngine.Events.UnityAction[group.Total];
            for (int i = 0; i < group.Total; i++)
            {
                GrabSocket socket = group.GetSocket(i);
                if (socket == null) continue;
                int index = i;
                rejectHandlers[i] = () => rejectedUntil[index] = Time.time + rejectedShowTime;
                socket.onRejected.AddListener(rejectHandlers[i]);
            }
        }

        private void OnDisable()
        {
            if (group == null) return;
            for (int i = 0; i < rejectHandlers.Length && i < group.Total; i++)
                if (rejectHandlers[i] != null && group.GetSocket(i) != null) group.GetSocket(i).onRejected.RemoveListener(rejectHandlers[i]);
        }

        private void Update()
        {
            if (group == null) return;
            float pulse = Mathf.Lerp(pulseMinAlpha, 1f, 0.5f + 0.5f * Mathf.Cos(Time.time * pulseRate * 2f * Mathf.PI));
            for (int i = 0; i < slotIcons.Length; i++)
            {
                if (slotIcons[i] == null) continue;
                bool exists = i < group.Total && group.GetSocket(i) != null;
                slotIcons[i].enabled = exists;
                if (!exists) continue;
                bool occupied = group.GetSocket(i).IsOccupied;
                if (!occupied && rejectedIcon != null && i < rejectedUntil.Length && Time.time < rejectedUntil[i])
                {
                    bool on = Mathf.Repeat(Time.time * blinkRate, 1f) > 0.35f;
                    SetIcon(slotIcons[i], rejectedIcon, on ? 1f : 0.15f);
                    continue;
                }
                SetIcon(slotIcons[i], occupied ? filledIcon : emptyIcon, occupied ? 1f : pulse);
            }

            if (group.Completed && completedAt < 0f) completedAt = Time.time;
            float alpha = targetIdleAlpha;
            if (completedAt >= 0f)
            {
                bool blinking = Time.time - completedAt < blinkDuration;
                alpha = blinking && Mathf.Repeat((Time.time - completedAt) * blinkRate, 1f) > 0.5f ? 0.15f : 1f;
            }
            SetIcon(targetIcon, targetTexture, alpha);
        }

        private void SetIcon(Renderer target, Texture2D texture, float alpha)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            if (texture != null) block.SetTexture(BaseMapId, texture);
            block.SetColor(BaseColorId, new Color(amber.r, amber.g, amber.b, amber.a * alpha));
            target.SetPropertyBlock(block);
        }
    }
}

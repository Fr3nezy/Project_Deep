using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Icona animata di uno schermo CRT: cambia la _BaseMap del renderer a fotogrammi, in loop, con un
    /// MaterialPropertyBlock (il materiale condiviso non viene toccato). Tempo non scalato, come gli altri schermi.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class ScreenFlipbook : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [Tooltip("Fotogrammi in ordine, bianco su trasparente. Il colore lo dà il materiale.")]
        [SerializeField] private Texture2D[] frames = new Texture2D[0];
        [SerializeField, Min(0.1f)] private float framesPerSecond = 2f;
        [Tooltip("L'ultimo fotogramma resta questi secondi in più prima di ricominciare.")]
        [SerializeField, Min(0f)] private float holdLastSeconds = 1f;

        private Renderer target;
        private MaterialPropertyBlock block;
        private int shown = -1;

        private void Awake()
        {
            target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            if (frames.Length == 0) Debug.LogError("[ScreenFlipbook] Nessun fotogramma.", this);
        }

        private void Update()
        {
            if (frames.Length == 0) return;
            float frameTime = 1f / framesPerSecond;
            float t = Time.unscaledTime % (frames.Length * frameTime + holdLastSeconds);
            int frame = Mathf.Min(frames.Length - 1, (int)(t / frameTime));
            if (frame == shown || frames[frame] == null) return;
            shown = frame;
            target.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, frames[frame]);
            target.SetPropertyBlock(block);
        }
    }
}

using UnityEngine;

namespace Deeploration.Audio
{
    /// <summary>
    /// Effetto sonoro one-shot da collegare a un UnityEvent (Play) o da codice: sceglie a caso uno dei clip,
    /// con un piccolo scarto di pitch. Crea da sé il suo AudioSource; spatialBlend 0 = suono della tuta (2D), 1 = nel mondo (3D).
    /// </summary>
    public sealed class SfxCue : MonoBehaviour
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [Tooltip("Scarto casuale del pitch attorno a 1 (0.05 = ±5%), per non ripetere identico lo stesso clip.")]
        [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.05f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [Tooltip("Distanza oltre cui il suono 3D non si sente più (rolloff lineare).")]
        [SerializeField, Min(1f)] private float maxDistance = 25f;

        private AudioSource source;
        private int last = -1;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
        }

        /// <summary>Suona un clip a caso (mai lo stesso due volte di fila se ce n'è più d'uno).</summary>
        public void Play()
        {
            if (source == null || clips == null || clips.Length == 0) return;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length;
            last = i;
            if (clips[i] == null) return;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clips[i], volume);
        }
    }
}

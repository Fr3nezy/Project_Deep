using UnityEngine;
using UnityEngine.Localization;

namespace Deeploration.Prologue
{
    [CreateAssetMenu(menuName = "Deeploration/Subtitle Line")]
    public sealed class SubtitleLine : ScriptableObject
    {
        public LocalizedString speaker = new LocalizedString();
        public LocalizedString text = new LocalizedString();
        [Tooltip("Voce della battuta (opzionale). La usa HelmetComms; il prologo su Timeline la ignora.")]
        public AudioClip voice;
    }
}

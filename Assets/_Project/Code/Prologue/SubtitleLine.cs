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
        [Tooltip("Suono prima della battuta (opzionale, es. jingle di avvio di H.E.L.M.). HelmetComms lo suona a pannello aperto e aspetta che finisca prima di voce e scrittura.")]
        public AudioClip intro;
    }
}

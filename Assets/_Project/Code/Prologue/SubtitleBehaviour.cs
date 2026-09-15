using System;
using UnityEngine.Playables;

namespace Deeploration.Prologue
{
    [Serializable]
    public sealed class SubtitleBehaviour : PlayableBehaviour
    {
        public SubtitleLine Line { get; set; }
    }
}

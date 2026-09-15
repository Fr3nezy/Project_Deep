using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Deeploration.Prologue
{
    [Serializable]
    public sealed class SubtitleClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private SubtitleLine line;

        public SubtitleLine Line
        {
            get => line;
            set => line = value;
        }

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SubtitleBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Line = line;
            return playable;
        }
    }
}

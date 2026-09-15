using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Deeploration.Prologue
{
    [TrackColor(0.2f, 0.8f, 1f)]
    [TrackClipType(typeof(SubtitleClip))]
    [TrackBindingType(typeof(SubtitlePanel))]
    public sealed class SubtitleTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<SubtitleMixerBehaviour>.Create(graph, inputCount);
        }
    }
}

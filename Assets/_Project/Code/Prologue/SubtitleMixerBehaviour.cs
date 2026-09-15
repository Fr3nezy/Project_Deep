using UnityEngine.Playables;

namespace Deeploration.Prologue
{
    public sealed class SubtitleMixerBehaviour : PlayableBehaviour
    {
        private SubtitleLine currentLine;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var panel = playerData as SubtitlePanel;
            if (panel == null) return;

            int inputCount = playable.GetInputCount();
            SubtitleLine activeLine = null;
            float maxWeight = 0f;

            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight > maxWeight)
                {
                    var inputPlayable = (ScriptPlayable<SubtitleBehaviour>)playable.GetInput(i);
                    var behaviour = inputPlayable.GetBehaviour();
                    if (behaviour != null && behaviour.Line != null)
                    {
                        maxWeight = weight;
                        activeLine = behaviour.Line;
                    }
                }
            }

            if (activeLine != currentLine)
            {
                currentLine = activeLine;
                if (currentLine != null)
                {
                    panel.Show(currentLine);
                }
                else
                {
                    panel.Clear();
                }
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            currentLine = null;
        }
    }
}

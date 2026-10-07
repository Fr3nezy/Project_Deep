#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Deeploration.Editor
{
    /// <summary>
    /// Mostra le maschere di fusione delle rocce (SG_Rock_Blend) come colori, per tarare proxy e parametri:
    /// rosso = contatto col suolo, verde = giuntura con le rocce vicine, blu = sedimento sopra e nelle cavità.
    /// </summary>
    public static class RockBlendDebugMenu
    {
        private const string MenuPath = "Deeplonauts/Debug/Rock Blend Masks";
        private static readonly int DebugId = Shader.PropertyToID("_EnvRockBlendDebug");

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool enabled = Shader.GetGlobalFloat(DebugId) < 0.5f;
            Shader.SetGlobalFloat(DebugId, enabled ? 1f : 0f);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Shader.GetGlobalFloat(DebugId) > 0.5f);
            return true;
        }
    }
}
#endif

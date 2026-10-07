using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Pubblica il materiale del fondale (SG_Terrain_Lit) come proprietà globali degli shader,
    /// così SG_Rock_Blend campiona lo stesso fango, con gli stessi parametri, nelle stesse coordinate del mondo.
    /// Va sul renderer del terreno.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class GroundMaterialPublisher : MonoBehaviour
    {
        private static readonly int GroundBaseMap = Shader.PropertyToID("_GroundBaseMap");
        private static readonly int GroundBumpMap = Shader.PropertyToID("_GroundBumpMap");
        private static readonly int GroundMSMap = Shader.PropertyToID("_GroundMSMap");
        private static readonly int GroundTint = Shader.PropertyToID("_GroundTint");
        private static readonly int GroundUV = Shader.PropertyToID("_GroundUV");
        private static readonly int GroundColorParams = Shader.PropertyToID("_GroundColorParams");
        private static readonly int GroundSurfParams = Shader.PropertyToID("_GroundSurfParams");
        private static readonly int GroundMacro = Shader.PropertyToID("_GroundMacro");

        private Renderer groundRenderer;

        private void OnEnable()
        {
            groundRenderer = GetComponent<Renderer>();
            Publish();
        }

        // Pochi SetGlobal per frame: le rocce restano allineate anche mentre si regola il materiale in editor.
        private void Update()
        {
            Publish();
        }

        private void Publish()
        {
            Material m = groundRenderer != null ? groundRenderer.sharedMaterial : null;
            if (m == null)
            {
                return;
            }

            Shader.SetGlobalTexture(GroundBaseMap, m.GetTexture("_BaseMap"));
            Shader.SetGlobalTexture(GroundBumpMap, m.GetTexture("_BumpMap"));
            Shader.SetGlobalTexture(GroundMSMap, m.GetTexture("_MetallicGlossMap"));
            Shader.SetGlobalColor(GroundTint, m.GetColor("_BaseColor"));

            Vector4 offset = m.GetVector("_Offset");
            Shader.SetGlobalVector(GroundUV, new Vector4(m.GetFloat("_WorldTileSize"), offset.x, offset.y, m.GetFloat("_StochasticTiling")));
            Shader.SetGlobalVector(GroundColorParams, new Vector4(m.GetFloat("_Brightness"), m.GetFloat("_Saturation"), m.GetFloat("_Contrast"), m.GetFloat("_NormalStrength")));
            Shader.SetGlobalVector(GroundSurfParams, new Vector4(m.GetFloat("_SmoothnessMin"), m.GetFloat("_SmoothnessMax"), m.GetFloat("_StochasticSharpness"), m.GetFloat("_MacroStrength")));
            Shader.SetGlobalVector(GroundMacro, new Vector4(m.GetFloat("_MacroScale"), 0f, 0f, 0f));
        }
    }
}

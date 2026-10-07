using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Deeploration.Rendering
{
    /// <summary>
    /// Renderer feature che applica l'override AnalogLook dopo il post-processing.
    /// Si attiva solo se nel Volume stack c'è un AnalogLook con intensità maggiore di zero.
    /// </summary>
    public sealed class AnalogLookFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private Material material;
        private AnalogLookPass pass;

        public override void Create()
        {
            if (shader == null)
            {
                shader = Shader.Find("Hidden/Deeplonauts/AnalogLook");
            }

            if (shader != null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
            }

            pass = new AnalogLookPass(material) { renderPassEvent = passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || !renderingData.cameraData.postProcessEnabled)
            {
                return;
            }

            AnalogLook settings = VolumeManager.instance.stack.GetComponent<AnalogLook>();
            if (settings != null && settings.IsActive())
            {
                renderer.EnqueuePass(pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        private sealed class AnalogLookPass : ScriptableRenderPass
        {
            private static readonly int ParamsId = Shader.PropertyToID("_AnalogParams");
            private static readonly int Params2Id = Shader.PropertyToID("_AnalogParams2");

            private readonly Material material;

            public AnalogLookPass(Material material)
            {
                this.material = material;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                AnalogLook settings = VolumeManager.instance.stack.GetComponent<AnalogLook>();
                UniversalCameraData camera = frameData.Get<UniversalCameraData>();
                // la dimensione del pixel è data a 1080p e scala con l'altezza del target
                float pixel = Mathf.Max(1f, settings.pixelSize.value * camera.cameraTargetDescriptor.height / 1080f);
                material.SetVector(ParamsId, new Vector4(settings.intensity.value, pixel, settings.colorLevels.value, settings.dither.value));
                material.SetVector(Params2Id, new Vector4(settings.scanlines.value, 0f, 0f, 0f));

                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_AnalogLookTarget";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "Analog Look");
                resources.cameraColor = destination;
            }
        }
    }
}

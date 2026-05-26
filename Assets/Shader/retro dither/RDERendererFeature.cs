using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

[DisallowMultipleRendererFeature("RDE Dither Effect")]
public class RDERendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class RDESettings
    {
        [Tooltip("Материал с шейдером Shader Graphs/RDE")]
        public Material material;

        [Range(0f, 4f)]
        public float ditherSpread = 1.0f;

        [Range(2f, 256f)]
        public float colorResolution = 8.0f;

        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public RDESettings settings = new RDESettings();
    private RDERenderPass _pass;

    public override void Create()
    {
        _pass = new RDERenderPass(settings);
        _pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
        {
            Debug.LogWarning("[RDE] Материал не назначен в Renderer Feature!");
            return;
        }
        renderer.EnqueuePass(_pass);
    }

    // ──────────────────────────────────────────────────────────────────
    private class RDERenderPass : ScriptableRenderPass
    {
        private static readonly int PropDitherSpread    = Shader.PropertyToID("_ditherspread");
        private static readonly int PropColorResolution = Shader.PropertyToID("_colorresolution");

        private readonly RDESettings _settings;

        // Имена для RenderGraph handle-ов
        private static readonly string k_TempName = "_RDE_Temp";

        public RDERenderPass(RDESettings settings)
        {
            _settings        = settings;
            profilingSampler = new ProfilingSampler("RDE Dither Pass");
        }

        // ── RenderGraph (Unity 6 / URP 17) ────────────────────────────
        public override void RecordRenderGraph(RenderGraph renderGraph,
                                       ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning("[RDE] Включите Intermediate Texture = Always в URP Renderer.");
                return;
            }

            // Обновляем параметры материала
            _settings.material.SetFloat(PropDitherSpread,    _settings.ditherSpread);
            _settings.material.SetFloat(PropColorResolution, _settings.colorResolution);

            TextureHandle src = resourceData.activeColorTexture;

            // ── Создаём временный буфер ─────────────────────────────────────
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;

            TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, k_TempName, false);

            // Blit src → dst через материал (pass 0)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                      "RDE Dither", out var passData))
            {
                passData.src      = src;
                passData.material = _settings.material;

                builder.UseTexture(src);
                builder.SetRenderAttachment(dst, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.src,
                        new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Blit dst → src обратно (pass 1)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                      "RDE Copy Back", out var passData))
            {
                passData.src      = dst;
                passData.material = _settings.material;

                builder.UseTexture(dst);
                builder.SetRenderAttachment(src, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.src,
                        new Vector4(1, 1, 0, 0), data.material, 1);
                });
            }
        }

        // Данные передаваемые в лямбду рендер-пасса
        private class PassData
        {
            public TextureHandle src;
            public Material      material;
        }
    }
}

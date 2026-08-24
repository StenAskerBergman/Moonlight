using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Moonlight.Rendering
{
    public sealed class UnderwaterTransitionRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        private UnderwaterTransitionPass pass;

        public override void Create()
        {
            pass?.Dispose();
            pass = new UnderwaterTransitionPass(injectionPoint);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType camType = renderingData.cameraData.cameraType;
            if ((camType != CameraType.Game && camType != CameraType.SceneView) || !UnderwaterTransitionState.ShouldRender)
                return;

            renderer.EnqueuePass(pass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            CameraType camType = renderingData.cameraData.cameraType;
            if ((camType != CameraType.Game && camType != CameraType.SceneView) || !UnderwaterTransitionState.ShouldRender)
                return;

            // URP creates camera targets after AddRenderPasses. Accessing the handle
            // here is required in URP 14 and prevents teardown/Editor camera errors.
            pass.SetTarget(renderer.cameraColorTargetHandle);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
        }

        private sealed class UnderwaterTransitionPass : ScriptableRenderPass
        {
            private static readonly int TransitionAmount = Shader.PropertyToID("_TransitionAmount");
            private static readonly int TransitionDirection = Shader.PropertyToID("_TransitionDirection");
            private static readonly int UnderwaterAmount = Shader.PropertyToID("_UnderwaterAmount");
            private static readonly int UnderwaterColor = Shader.PropertyToID("_UnderwaterColor");
            private static readonly int DistortionStrength = Shader.PropertyToID("_DistortionStrength");
            private static readonly int EdgeWidth = Shader.PropertyToID("_EdgeWidth");
            private static readonly int WaterLevel = Shader.PropertyToID("_WaterLevel");

            private static readonly int ShallowWaterColor = Shader.PropertyToID("_ShallowWaterColor");
            private static readonly int DeepWaterColor = Shader.PropertyToID("_DeepWaterColor");
            private static readonly int AbyssalColor = Shader.PropertyToID("_AbyssalColor");
            private static readonly int AbsorptionCoefficients = Shader.PropertyToID("_AbsorptionCoefficients");
            private static readonly int FogDensity = Shader.PropertyToID("_FogDensity");
            private static readonly int DeepDepthThreshold = Shader.PropertyToID("_DeepDepthThreshold");
            private static readonly int AbyssDepthThreshold = Shader.PropertyToID("_AbyssDepthThreshold");
            private static readonly int SunScatteringIntensity = Shader.PropertyToID("_SunScatteringIntensity");
            private static readonly int SunDepthExtinction = Shader.PropertyToID("_SunDepthExtinction");

            private static readonly int CausticsStrength = Shader.PropertyToID("_CausticsStrength");
            private static readonly int CausticsScale = Shader.PropertyToID("_CausticsScale");
            private static readonly int CausticsSpeed = Shader.PropertyToID("_CausticsSpeed");
            private static readonly int CausticsFadeDepth = Shader.PropertyToID("_CausticsFadeDepth");

            private static readonly int MarineSnowIntensity = Shader.PropertyToID("_MarineSnowIntensity");
            private static readonly int MarineSnowScale = Shader.PropertyToID("_MarineSnowScale");
            private static readonly int MarineSnowSpeed = Shader.PropertyToID("_MarineSnowSpeed");

            private static readonly int InverseViewProjection = Shader.PropertyToID("_InverseViewProjection");

            private readonly Material material;
            private RTHandle source;
            private RTHandle temporary;

            public UnderwaterTransitionPass(RenderPassEvent injectionPoint)
            {
                renderPassEvent = injectionPoint;
                var shader = Shader.Find("Hidden/Moonlight/UnderwaterTransition");
                if (shader != null)
                    material = CoreUtils.CreateEngineMaterial(shader);
            }

            public void SetTarget(RTHandle cameraColor) => source = cameraColor;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref temporary, descriptor, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_UnderwaterTransitionTexture");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null || source == null)
                    return;

                material.SetFloat(TransitionAmount, UnderwaterTransitionState.TransitionAmount);
                material.SetFloat(TransitionDirection, UnderwaterTransitionState.Direction);
                material.SetFloat(UnderwaterAmount, UnderwaterTransitionState.UnderwaterAmount);
                material.SetColor(UnderwaterColor, UnderwaterTransitionState.Color);
                material.SetFloat(DistortionStrength, UnderwaterTransitionState.DistortionStrength);
                material.SetFloat(EdgeWidth, UnderwaterTransitionState.EdgeWidth);
                material.SetFloat(WaterLevel, UnderwaterTransitionState.WaterLevel);

                material.SetColor(ShallowWaterColor, UnderwaterTransitionState.ShallowWaterColor);
                material.SetColor(DeepWaterColor, UnderwaterTransitionState.DeepWaterColor);
                material.SetColor(AbyssalColor, UnderwaterTransitionState.AbyssalColor);
                material.SetVector(AbsorptionCoefficients, UnderwaterTransitionState.AbsorptionCoefficients);
                material.SetFloat(FogDensity, UnderwaterTransitionState.FogDensity);
                material.SetFloat(DeepDepthThreshold, UnderwaterTransitionState.DeepDepthThreshold);
                material.SetFloat(AbyssDepthThreshold, UnderwaterTransitionState.AbyssDepthThreshold);
                material.SetFloat(SunScatteringIntensity, UnderwaterTransitionState.SunScatteringIntensity);
                material.SetFloat(SunDepthExtinction, UnderwaterTransitionState.SunDepthExtinction);

                material.SetFloat(CausticsStrength, UnderwaterTransitionState.CausticsStrength);
                material.SetFloat(CausticsScale, UnderwaterTransitionState.CausticsScale);
                material.SetFloat(CausticsSpeed, UnderwaterTransitionState.CausticsSpeed);
                material.SetFloat(CausticsFadeDepth, UnderwaterTransitionState.CausticsFadeDepth);

                material.SetFloat(MarineSnowIntensity, UnderwaterTransitionState.MarineSnowIntensity);
                material.SetFloat(MarineSnowScale, UnderwaterTransitionState.MarineSnowScale);
                material.SetFloat(MarineSnowSpeed, UnderwaterTransitionState.MarineSnowSpeed);

                Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(renderingData.cameraData.GetProjectionMatrix(), false);
                Matrix4x4 viewProj = gpuProj * renderingData.cameraData.GetViewMatrix();
                material.SetMatrix(InverseViewProjection, viewProj.inverse);

                var cmd = CommandBufferPool.Get("Underwater Camera Transition");
                Blitter.BlitCameraTexture(cmd, source, temporary, material, 0);
                Blitter.BlitCameraTexture(cmd, temporary, source);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                temporary?.Release();
                CoreUtils.Destroy(material);
            }
        }
    }
}

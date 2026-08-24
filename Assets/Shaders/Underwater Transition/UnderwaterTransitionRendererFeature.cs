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
            if (renderingData.cameraData.cameraType != CameraType.Game || !UnderwaterTransitionState.ShouldRender)
                return;

            renderer.EnqueuePass(pass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game || !UnderwaterTransitionState.ShouldRender)
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

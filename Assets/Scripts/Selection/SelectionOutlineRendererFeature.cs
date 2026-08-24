using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SelectionOutlineRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public Shader maskShader;
        public Shader outlineShader;

        public Color outlineColor = Color.white;

        [Min(0.001f)]
        public float outlineWidth = 0.05f;

        public RenderPassEvent renderPassEvent =
            RenderPassEvent.AfterRenderingTransparents;
    }

    public Settings settings = new Settings();

    private Material maskMaterial;
    private Material outlineMaterial;
    private SelectionOutlinePass pass;

    public override void Create()
    {
        CoreUtils.Destroy(maskMaterial);
        CoreUtils.Destroy(outlineMaterial);

        if (settings.maskShader != null)
            maskMaterial = CoreUtils.CreateEngineMaterial(settings.maskShader);

        if (settings.outlineShader != null)
            outlineMaterial = CoreUtils.CreateEngineMaterial(settings.outlineShader);

        pass = new SelectionOutlinePass(maskMaterial, outlineMaterial, settings);
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (maskMaterial == null || outlineMaterial == null)
            return;

        // Only the Game view actually needs the selection silhouette; skip preview,
        // reflection-probe and Scene-view cameras so the pass doesn't run twice per
        // frame while the Scene view is open alongside Game view.
        if (renderingData.cameraData.camera.cameraType != CameraType.Game)
            return;

        pass.renderPassEvent = settings.renderPassEvent;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(maskMaterial);
        CoreUtils.Destroy(outlineMaterial);
    }

    private class SelectionOutlinePass : ScriptableRenderPass
    {
        private readonly Material maskMaterial;
        private readonly Material outlineMaterial;
        private readonly Settings settings;

        private readonly List<SelectionOutlineTarget.OutlineRenderer> renderers =
            new List<SelectionOutlineTarget.OutlineRenderer>();

        public SelectionOutlinePass(
            Material maskMaterial,
            Material outlineMaterial,
            Settings settings)
        {
            this.maskMaterial = maskMaterial;
            this.outlineMaterial = outlineMaterial;
            this.settings = settings;
        }

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            SelectionOutlineTarget.CollectRenderers(renderers);

            if (renderers.Count == 0)
                return;

            outlineMaterial.SetColor("_OutlineColor", settings.outlineColor);
            outlineMaterial.SetFloat("_OutlineWidth", settings.outlineWidth);

            CommandBuffer cmd = CommandBufferPool.Get("Selection Outline");

            DrawRenderers(cmd, maskMaterial);
            DrawRenderers(cmd, outlineMaterial);

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

        private void DrawRenderers(CommandBuffer cmd, Material material)
        {
            foreach (SelectionOutlineTarget.OutlineRenderer entry in renderers)
            {
                Renderer renderer = entry.Renderer;

                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                for (int i = 0; i < entry.SubMeshCount; i++)
                    cmd.DrawRenderer(renderer, material, i, 0);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CryingSnow.StackCraft
{
    public class CustomPostProcessFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material effectMaterial;
            public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public Settings settings = new Settings();
        CustomPass customPass;

        public override void Create()
        {
            customPass = new CustomPass(settings.effectMaterial);
            customPass.renderPassEvent = settings.renderEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.effectMaterial == null) return;
            renderer.EnqueuePass(customPass);
        }

        class CustomPass : ScriptableRenderPass
        {
            Material material;
            RenderTargetIdentifier source;

            public CustomPass(Material mat)
            {
                this.material = mat;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("Custom Post Process");

                source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                int tempTextureId = Shader.PropertyToID("_TempTexture");
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

                cmd.GetTemporaryRT(tempTextureId, desc);
                cmd.Blit(source, tempTextureId, material);
                cmd.Blit(tempTextureId, source);
                cmd.ReleaseTemporaryRT(tempTextureId);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}

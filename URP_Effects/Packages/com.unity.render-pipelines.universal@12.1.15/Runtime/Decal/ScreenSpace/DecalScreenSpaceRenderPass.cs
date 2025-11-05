using System.Collections.Generic;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    internal class DecalDrawScreenSpaceSystem : DecalDrawSystem
    {
        public DecalDrawScreenSpaceSystem(DecalEntityManager entityManager) : base("DecalDrawScreenSpaceSystem.Execute", entityManager) { }
        protected override int GetPassIndex(DecalCachedChunk decalCachedChunk) => decalCachedChunk.passIndexScreenSpace;
    }

    internal class DecalScreenSpaceRenderPass : ScriptableRenderPass
    {
        private FilteringSettings m_FilteringSettings;
        private ProfilingSampler m_ProfilingSampler;
        private List<ShaderTagId> m_ShaderTagIdList;
        private DecalDrawScreenSpaceSystem m_DrawSystem;
        private DecalScreenSpaceSettings m_Settings;

        public DecalScreenSpaceRenderPass(DecalScreenSpaceSettings settings, DecalDrawScreenSpaceSystem drawSystem)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
            // 代表需要深度纹理
            ConfigureInput(ScriptableRenderPassInput.Depth); // Require depth

            m_DrawSystem = drawSystem;
            m_Settings = settings;
            m_ProfilingSampler = new ProfilingSampler("Decal Screen Space Render");
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

            m_ShaderTagIdList = new List<ShaderTagId>();

            if (m_DrawSystem == null)
                m_ShaderTagIdList.Add(new ShaderTagId(DecalShaderPassNames.DecalScreenSpaceProjector));
            else
                m_ShaderTagIdList.Add(new ShaderTagId(DecalShaderPassNames.DecalScreenSpaceMesh));
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 对渲染目标应用视口和裁剪区域的缩放和偏移
                RenderingUtils.SetScaleBiasRt(cmd, in renderingData);

                // 重构视图空间法线转换矩阵
                NormalReconstruction.SetupProperties(cmd, renderingData.cameraData);

                // 设置法线的质量
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendLow, m_Settings.normalBlend == DecalNormalBlend.Low);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendMedium, m_Settings.normalBlend == DecalNormalBlend.Medium);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendHigh, m_Settings.normalBlend == DecalNormalBlend.High);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 绘制decal
                m_DrawSystem?.Execute(cmd);

                // 绘制renderer
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
            {
                throw new System.ArgumentNullException("cmd");
            }

            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendLow, false);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendMedium, false);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.DecalNormalBlendHigh, false);
        }
    }
}

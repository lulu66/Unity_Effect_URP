using System;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Renders a shadow map for the main Light.
    /// </summary>
    public class MainLightShadowCasterPass : ScriptableRenderPass
    {
        private static class MainLightShadowConstantBuffer
        {
            public static int _WorldToShadow;
            public static int _ShadowParams;
            public static int _CascadeShadowSplitSpheres0;
            public static int _CascadeShadowSplitSpheres1;
            public static int _CascadeShadowSplitSpheres2;
            public static int _CascadeShadowSplitSpheres3;
            public static int _CascadeShadowSplitSphereRadii;
            public static int _ShadowOffset0;
            public static int _ShadowOffset1;
            public static int _ShadowOffset2;
            public static int _ShadowOffset3;
            public static int _ShadowmapSize;
        }

        const int k_MaxCascades = 4;
        const int k_ShadowmapBufferBits = 16;
        float m_CascadeBorder;
        float m_MaxShadowDistanceSq;
        int m_ShadowCasterCascadesCount;

        RenderTargetHandle m_MainLightShadowmap;
        internal RenderTexture m_MainLightShadowmapTexture;

        Matrix4x4[] m_MainLightShadowMatrices;

        // 存储阴影在级联阴影图集的转换矩阵
        ShadowSliceData[] m_CascadeSlices;

        // 存储级联阴影中每个层级的culling sphere位置和半径
        Vector4[] m_CascadeSplitDistances;

        bool m_CreateEmptyShadowmap;

        ProfilingSampler m_ProfilingSetupSampler = new ProfilingSampler("Setup Main Shadowmap");

        public MainLightShadowCasterPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(MainLightShadowCasterPass));
            renderPassEvent = evt;

            m_MainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            m_CascadeSlices = new ShadowSliceData[k_MaxCascades];
            m_CascadeSplitDistances = new Vector4[k_MaxCascades];

            MainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            MainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_MainLightShadowParams");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
            MainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_MainLightShadowOffset0");
            MainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_MainLightShadowOffset1");
            MainLightShadowConstantBuffer._ShadowOffset2 = Shader.PropertyToID("_MainLightShadowOffset2");
            MainLightShadowConstantBuffer._ShadowOffset3 = Shader.PropertyToID("_MainLightShadowOffset3");
            MainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_MainLightShadowmapSize");

            m_MainLightShadowmap.Init("_MainLightShadowmapTexture");
        }

        public bool Setup(ref RenderingData renderingData)
        {
            using var profScope = new ProfilingScope(null, m_ProfilingSetupSampler);

            if (!renderingData.shadowData.supportsMainLightShadows)
                return SetupForEmptyRendering(ref renderingData);

            // 清空阴影相关的数据：阴影矩阵...
            Clear();

            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return SetupForEmptyRendering(ref renderingData);

            // 获取主光源
            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;

            if (light.shadows == LightShadows.None)
                return SetupForEmptyRendering(ref renderingData);

            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }

            // 这里的bounds不是指物体的包围盒，而是为光源计算的，所有需要投射阴影的物体所分布的空间区域；
            // 如果渲染的物体不在这个包围盒内，则不创建阴影纹理
            Bounds bounds;
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
                return SetupForEmptyRendering(ref renderingData);

            // 获取级联阴影的级数
            m_ShadowCasterCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;

            // 计算级联阴影图集中单个Tile能够使用的最大分辨率
            int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(renderingData.shadowData.mainLightShadowmapWidth,
                renderingData.shadowData.mainLightShadowmapHeight, m_ShadowCasterCascadesCount);

            // 级联阴影图集的宽和高。这里涉及urp的阴影图集的布局策略：
            // URP对主光源的级联阴影图集采用了一种“垂直堆叠”的布局方式，并且预设了最大级联数为4。当级联数为2时，它使用了一种特殊的“并排”布局来优化空间，而级联数为3和4时，则回归到简单的垂直堆叠。
            renderTargetWidth = renderingData.shadowData.mainLightShadowmapWidth;
            renderTargetHeight = (m_ShadowCasterCascadesCount == 2) ?
                renderingData.shadowData.mainLightShadowmapHeight >> 1 :
                renderingData.shadowData.mainLightShadowmapHeight;

            for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
            {
                // 获取级联阴影每个层级的阴影位置和变换矩阵
                bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref renderingData.cullResults, ref renderingData.shadowData,
                    shadowLightIndex, cascadeIndex, renderTargetWidth, renderTargetHeight, shadowResolution, light.shadowNearPlane,
                    out m_CascadeSplitDistances[cascadeIndex], out m_CascadeSlices[cascadeIndex]);

                if (!success)
                    return SetupForEmptyRendering(ref renderingData);
            }

            // 创建级联阴影图集
            m_MainLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(renderTargetWidth, renderTargetHeight, k_ShadowmapBufferBits);

            // 计算最大阴影距离的平方
            m_MaxShadowDistanceSq = renderingData.cameraData.maxShadowDistance * renderingData.cameraData.maxShadowDistance;

            // 定义了阴影级联之间的过渡区域大小，用于平滑不同级联级别之间的切换，避免明显的阴影"跳跃"现象
            m_CascadeBorder = renderingData.shadowData.mainLightShadowCascadeBorder;
            m_CreateEmptyShadowmap = false;
            useNativeRenderPass = true;

            return true;
        }

        /// <summary>
        /// 判断是否创建空纹理(1x1的shadowmap)，返回true表示创建；返回false表示不创建
        /// </summary>
        /// <param name="renderingData"></param>
        /// <returns></returns>
        bool SetupForEmptyRendering(ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.renderer.stripShadowsOffVariants)
                return false;

            m_MainLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
            m_CreateEmptyShadowmap = true;
            useNativeRenderPass = false;

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(new RenderTargetIdentifier(m_MainLightShadowmapTexture), m_MainLightShadowmapTexture.depthStencilFormat, renderTargetWidth, renderTargetHeight, 1, true);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_CreateEmptyShadowmap)
            {
                SetEmptyMainLightCascadeShadowmap(ref context);
                return;
            }

            RenderMainLightCascadeShadowmap(ref context, ref renderingData.cullResults, ref renderingData.lightData, ref renderingData.shadowData);
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (m_MainLightShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(m_MainLightShadowmapTexture);
                m_MainLightShadowmapTexture = null;
            }
        }

        void Clear()
        {
            m_MainLightShadowmapTexture = null;

            for (int i = 0; i < m_MainLightShadowMatrices.Length; ++i)
                m_MainLightShadowMatrices[i] = Matrix4x4.identity;

            for (int i = 0; i < m_CascadeSplitDistances.Length; ++i)
                m_CascadeSplitDistances[i] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            for (int i = 0; i < m_CascadeSlices.Length; ++i)
                m_CascadeSlices[i].Clear();
        }

        void SetEmptyMainLightCascadeShadowmap(ref ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, true);
            cmd.SetGlobalTexture(m_MainLightShadowmap.id, m_MainLightShadowmapTexture);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams,
                new Vector4(1, 0, 1, 0));
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowmapSize,
                new Vector4(1f / m_MainLightShadowmapTexture.width, 1f / m_MainLightShadowmapTexture.height, m_MainLightShadowmapTexture.width, m_MainLightShadowmapTexture.height));
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void RenderMainLightCascadeShadowmap(ref ScriptableRenderContext context, ref CullingResults cullResults, ref LightData lightData, ref ShadowData shadowData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;

            // 获取主光源
            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];

            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.MainLightShadow)))
            {
                // 设置哪些灯光层可以产生阴影
                var settings = new ShadowDrawingSettings(cullResults, shadowLightIndex);
                settings.useRenderingLayerMaskTest = UniversalRenderPipeline.asset.supportsLightLayers;

                // 对于每一级阴影
                for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
                {
                    settings.splitData = m_CascadeSlices[cascadeIndex].splitData;

                    // 计算阴影的深度偏移和法线偏移，存储于x和y分量中
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, ref shadowData, m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].resolution);
                    // 设置shader阴影的偏移、光照方向、光源位置
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                    // 关闭关键字：_CASTING_PUNCTUAL_LIGHT_SHADOW
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.CastingPunctualLightShadow, false);
                    // 渲染阴影
                    ShadowUtils.RenderShadowSlice(cmd, ref context, ref m_CascadeSlices[cascadeIndex],
                        ref settings, m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].viewMatrix);
                }

                shadowData.isKeywordSoftShadowsEnabled = shadowLight.light.shadows == LightShadows.Soft && shadowData.supportsSoftShadows;

                // 非级联阴影，开启关键字：_MAIN_LIGHT_SHADOWS
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, shadowData.mainLightShadowCascadesCount == 1);

                // 级联阴影，开启关键字：_MAIN_LIGHT_SHADOWS_CASCADE
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, shadowData.mainLightShadowCascadesCount > 1);

                // 软阴影，开启关键字：_SHADOWS_SOFT
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, shadowData.isKeywordSoftShadowsEnabled);

                SetupMainLightShadowReceiverConstants(cmd, shadowLight, shadowData.supportsSoftShadows);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// 设置级联阴影 shader需要使用的参数
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="shadowLight"></param>
        /// <param name="supportsSoftShadows"></param>
        void SetupMainLightShadowReceiverConstants(CommandBuffer cmd, VisibleLight shadowLight, bool supportsSoftShadows)
        {
            Light light = shadowLight.light;
            bool softShadows = shadowLight.light.shadows == LightShadows.Soft && supportsSoftShadows;

            int cascadeCount = m_ShadowCasterCascadesCount;
            for (int i = 0; i < cascadeCount; ++i)
                m_MainLightShadowMatrices[i] = m_CascadeSlices[i].shadowTransform;

            // We setup and additional a no-op WorldToShadow matrix in the last index
            // because the ComputeCascadeIndex function in Shadows.hlsl can return an index
            // out of bounds. (position not inside any cascade) and we want to avoid branching

            // 将未使用的级联阴影矩阵槽位填充无操作的单位矩阵，防止shader中访问未初始化的数据产生错误的阴影计算
            Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
            noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
            for (int i = cascadeCount; i <= k_MaxCascades; ++i)
                m_MainLightShadowMatrices[i] = noOpShadowMatrix;

            float invShadowAtlasWidth = 1.0f / renderTargetWidth;
            float invShadowAtlasHeight = 1.0f / renderTargetHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
            float softShadowsProp = softShadows ? 1.0f : 0.0f;

            ShadowUtils.GetScaleAndBiasForLinearDistanceFade(m_MaxShadowDistanceSq, m_CascadeBorder, out float shadowFadeScale, out float shadowFadeBias);

            cmd.SetGlobalTexture(m_MainLightShadowmap.id, m_MainLightShadowmapTexture);
            cmd.SetGlobalMatrixArray(MainLightShadowConstantBuffer._WorldToShadow, m_MainLightShadowMatrices);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams,
                new Vector4(light.shadowStrength, softShadowsProp, shadowFadeScale, shadowFadeBias));

            if (m_ShadowCasterCascadesCount > 1)
            {
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0,
                    m_CascadeSplitDistances[0]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1,
                    m_CascadeSplitDistances[1]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2,
                    m_CascadeSplitDistances[2]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3,
                    m_CascadeSplitDistances[3]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, new Vector4(
                    m_CascadeSplitDistances[0].w * m_CascadeSplitDistances[0].w,
                    m_CascadeSplitDistances[1].w * m_CascadeSplitDistances[1].w,
                    m_CascadeSplitDistances[2].w * m_CascadeSplitDistances[2].w,
                    m_CascadeSplitDistances[3].w * m_CascadeSplitDistances[3].w));
            }

            // Inside shader soft shadows are controlled through global keyword.
            // If any additional light has soft shadows it will force soft shadows on main light too.
            // As it is not trivial finding out which additional light has soft shadows, we will pass main light properties if soft shadows are supported.
            // This workaround will be removed once we will support soft shadows per light.
            if (supportsSoftShadows)
            {
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset0,
                    new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset1,
                    new Vector4(invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset2,
                    new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset3,
                    new Vector4(invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));

                // Currently only used when !SHADER_API_MOBILE but risky to not set them as it's generic
                // enough so custom shaders might use it.
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowmapSize, new Vector4(invShadowAtlasWidth,
                    invShadowAtlasHeight,
                    renderTargetWidth, renderTargetHeight));
            }
        }
    };
}

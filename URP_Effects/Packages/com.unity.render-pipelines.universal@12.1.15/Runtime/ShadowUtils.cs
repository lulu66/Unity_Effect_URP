using System;

namespace UnityEngine.Rendering.Universal
{
    public struct ShadowSliceData
    {
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;
        public Matrix4x4 shadowTransform;
        public int offsetX;
        public int offsetY;
        public int resolution;
        public ShadowSplitData splitData; // splitData contains culling information

        public void Clear()
        {
            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            shadowTransform = Matrix4x4.identity;
            offsetX = offsetY = 0;
            resolution = 1024;
        }
    }

    public static class ShadowUtils
    {
        private static readonly bool m_ForceShadowPointSampling;

        static ShadowUtils()
        {
            m_ForceShadowPointSampling = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal &&
                GraphicsSettings.HasShaderDefine(Graphics.activeTier, BuiltinShaderDefine.UNITY_METAL_SHADOWS_USE_POINT_FILTERING);
        }

        public static bool ExtractDirectionalLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, int cascadeIndex, int shadowmapWidth, int shadowmapHeight, int shadowResolution, float shadowNearPlane, out Vector4 cascadeSplitDistance, out ShadowSliceData shadowSliceData, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix)
        {
            bool result = ExtractDirectionalLightMatrix(ref cullResults, ref shadowData, shadowLightIndex, cascadeIndex, shadowmapWidth, shadowmapHeight, shadowResolution, shadowNearPlane, out cascadeSplitDistance, out shadowSliceData);
            viewMatrix = shadowSliceData.viewMatrix;
            projMatrix = shadowSliceData.projectionMatrix;
            return result;
        }

        /// <summary>
        /// 计算每个级联层级的位置、距离和阴影转换矩阵等数据
        /// </summary>
        /// <param name="cullResults">参与渲染的场景物体</param>
        /// <param name="shadowData"></param>
        /// <param name="shadowLightIndex">产生阴影的光源的索引</param>
        /// <param name="cascadeIndex">级联阴影的层级</param>
        /// <param name="shadowmapWidth">级联阴影图集的宽</param>
        /// <param name="shadowmapHeight">级联阴影图集的高</param>
        /// <param name="shadowResolution">图集中单个shadowmap的最大分辨率</param>
        /// <param name="shadowNearPlane">阴影近平面距离</param>
        /// <param name="cascadeSplitDistance">给定层级的包围球的中心点和半径</param>
        /// <param name="shadowSliceData">阴影空间转换矩阵</param>
        /// <returns></returns>
        public static bool ExtractDirectionalLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, int cascadeIndex, int shadowmapWidth, int shadowmapHeight, int shadowResolution, float shadowNearPlane, out Vector4 cascadeSplitDistance, out ShadowSliceData shadowSliceData)
        {
            // 获取世界空间到光源空间的视图矩阵和投影矩阵、给定层级的裁剪数据
            bool success = cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(shadowLightIndex,
                cascadeIndex, shadowData.mainLightShadowCascadesCount, shadowData.mainLightShadowCascadesSplit, shadowResolution, shadowNearPlane, out shadowSliceData.viewMatrix, out shadowSliceData.projectionMatrix,
                out shadowSliceData.splitData);

            // xyz: sphere center; w:sphere radius
            cascadeSplitDistance = shadowSliceData.splitData.cullingSphere;

            // 应该是在图集中的x和y方向上的偏移
            shadowSliceData.offsetX = (cascadeIndex % 2) * shadowResolution;
            shadowSliceData.offsetY = (cascadeIndex / 2) * shadowResolution;

            // 当前层级shadow的分辨率
            shadowSliceData.resolution = shadowResolution;

            // 世界空间到阴影纹理空间的转换矩阵
            shadowSliceData.shadowTransform = GetShadowTransform(shadowSliceData.projectionMatrix, shadowSliceData.viewMatrix);

            // It is the culling sphere radius multiplier for shadow cascade blending
            // If this is less than 1.0, then it will begin to cull castors across cascades
            // 层级之间阴影混合的比率
            shadowSliceData.splitData.shadowCascadeBlendCullingFactor = 1.0f;

            // If we have shadow cascades baked into the atlas we bake cascade transform
            // in each shadow matrix to save shader ALU and L/S
            // 将阴影的转换矩阵转换到图集中的精确位置（施加纹理空间缩放和偏移）
            if (shadowData.mainLightShadowCascadesCount > 1)
                ApplySliceTransform(ref shadowSliceData, shadowmapWidth, shadowmapHeight);

            return success;
        }

        public static bool ExtractSpotLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, out Matrix4x4 shadowMatrix, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData)
        {
            bool success = cullResults.ComputeSpotShadowMatricesAndCullingPrimitives(shadowLightIndex, out viewMatrix, out projMatrix, out splitData); // returns false if input parameters are incorrect (rare)
            shadowMatrix = GetShadowTransform(projMatrix, viewMatrix);
            return success;
        }

        public static bool ExtractPointLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, CubemapFace cubemapFace, float fovBias, out Matrix4x4 shadowMatrix, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData)
        {
            bool success = cullResults.ComputePointShadowMatricesAndCullingPrimitives(shadowLightIndex, cubemapFace, fovBias, out viewMatrix, out projMatrix, out splitData); // returns false if input parameters are incorrect (rare)

            // In native API CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives there is code that inverts the 3rd component of shadow-casting spot light's "world-to-local" matrix (it was so since its original addition to the code base):
            // https://github.cds.internal.unity3d.com/unity/unity/commit/34813e063526c4be0ef0448dfaae3a911dd8be58#diff-cf0b417fc6bd8ee2356770797e628cd4R331
            // (the same transformation has also always been used in the Built-In Render Pipeline)
            //
            // However native API CullingResults.ComputePointShadowMatricesAndCullingPrimitives does not contain this transformation.
            // As a result, the view matrices returned for a point light shadow face, and for a spot light with same direction as that face, have opposite 3rd component.
            //
            // This causes normalBias to be incorrectly applied to shadow caster vertices during the point light shadow pass.
            // To counter this effect, we invert the point light shadow view matrix component here:
            {
                viewMatrix.m10 = -viewMatrix.m10;
                viewMatrix.m11 = -viewMatrix.m11;
                viewMatrix.m12 = -viewMatrix.m12;
                viewMatrix.m13 = -viewMatrix.m13;
            }

            shadowMatrix = GetShadowTransform(projMatrix, viewMatrix);
            return success;
        }

        /// <summary>
        /// 渲染阴影
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="context"></param>
        /// <param name="shadowSliceData"></param>
        /// <param name="settings"></param>
        /// <param name="proj"></param>
        /// <param name="view"></param>
        public static void RenderShadowSlice(CommandBuffer cmd, ref ScriptableRenderContext context,
            ref ShadowSliceData shadowSliceData, ref ShadowDrawingSettings settings,
            Matrix4x4 proj, Matrix4x4 view)
        {
            cmd.SetGlobalDepthBias(1.0f, 2.5f); // these values match HDRP defaults (see https://github.com/Unity-Technologies/Graphics/blob/9544b8ed2f98c62803d285096c91b44e9d8cbc47/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowAtlas.cs#L197 )

            cmd.SetViewport(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));
            cmd.SetViewProjectionMatrices(view, proj);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            context.DrawShadows(ref settings);
            cmd.DisableScissorRect();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            cmd.SetGlobalDepthBias(0.0f, 0.0f); // Restore previous depth bias values
        }

        public static void RenderShadowSlice(CommandBuffer cmd, ref ScriptableRenderContext context,
            ref ShadowSliceData shadowSliceData, ref ShadowDrawingSettings settings)
        {
            RenderShadowSlice(cmd, ref context, ref shadowSliceData, ref settings,
                shadowSliceData.projectionMatrix, shadowSliceData.viewMatrix);
        }

        /// <summary>
        /// 在一个给定大小的阴影纹理图集中，计算当需要容纳指定数量的贴图时，每张贴图最大能使用的分辨率是多少
        /// </summary>
        /// <param name="atlasWidth"></param>
        /// <param name="atlasHeight"></param>
        /// <param name="tileCount"></param>
        /// <returns></returns>
        public static int GetMaxTileResolutionInAtlas(int atlasWidth, int atlasHeight, int tileCount)
        {
            // 阴影纹理图集的分辨率
            int resolution = Mathf.Min(atlasWidth, atlasHeight);
            int currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            while (currentTileCount < tileCount)
            {
                // 分辨率减半
                resolution = resolution >> 1;
                currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            }
            return resolution;
        }

        /// <summary>
        /// 将阴影变换矩阵从整个阴影图集空间转换到当前级联在图集中的切片空间
        /// </summary>
        /// <param name="shadowSliceData"></param>
        /// <param name="atlasWidth"></param>
        /// <param name="atlasHeight"></param>
        public static void ApplySliceTransform(ref ShadowSliceData shadowSliceData, int atlasWidth, int atlasHeight)
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;

            // 目的：将像素坐标归一化到[0,1]的纹理空间
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            // 当前切片的分辨率缩放到纹理空间中
            sliceTransform.m00 = shadowSliceData.resolution * oneOverAtlasWidth;
            sliceTransform.m11 = shadowSliceData.resolution * oneOverAtlasHeight;

            // 当前切片的偏移归一化到纹理空间
            sliceTransform.m03 = shadowSliceData.offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = shadowSliceData.offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            shadowSliceData.shadowTransform = sliceTransform * shadowSliceData.shadowTransform;
        }

        /// <summary>
        /// 返回世界空间中的深度偏移和法线偏移（通过阴影贴图分辨率自适应）
        /// 解决阴影渲染中常见的"阴影痤疮"和"彼得潘"问题；
        /// 根据光源类型、阴影贴图分辨率和质量设置，计算出合适的深度偏置和法线偏置值，以消除阴影渲染中的各种视觉瑕疵。
        /// </summary>
        /// <param name="shadowLight"></param>
        /// <param name="shadowLightIndex"></param>
        /// <param name="shadowData"></param>
        /// <param name="lightProjectionMatrix"></param>
        /// <param name="shadowResolution"></param>
        /// <returns></returns>
        public static Vector4 GetShadowBias(ref VisibleLight shadowLight, int shadowLightIndex, ref ShadowData shadowData, Matrix4x4 lightProjectionMatrix, float shadowResolution)
        {
            if (shadowLightIndex < 0 || shadowLightIndex >= shadowData.bias.Count)
            {
                Debug.LogWarning(string.Format("{0} is not a valid light index.", shadowLightIndex));
                return Vector4.zero;
            }

            // 计算视锥体大小：计算阴影贴图中一个像素在世界空间中的大小，这是计算bias的基础；
            float frustumSize;
            if (shadowLight.lightType == LightType.Directional)
            {
                // Frustum size is guaranteed to be a cube as we wrap shadow frustum around a sphere
                // m00是投影矩阵的缩放因子
                // 计算阴影视锥体在世界空间中的宽度
                frustumSize = 2.0f / lightProjectionMatrix.m00;
            }
            else if (shadowLight.lightType == LightType.Spot)
            {
                // For perspective projections, shadow texel size varies with depth
                // It will only work well if done in receiver side in the pixel shader. Currently UniversalRP
                // do bias on caster side in vertex shader. When we add shader quality tiers we can properly
                // handle this. For now, as a poor approximation we do a constant bias and compute the size of
                // the frustum as if it was orthogonal considering the size at mid point between near and far planes.
                // Depending on how big the light range is, it will be good enough with some tweaks in bias
                frustumSize = Mathf.Tan(shadowLight.spotAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range; // half-width (in world-space units) of shadow frustum's "far plane"
            }
            else if (shadowLight.lightType == LightType.Point)
            {
                // [Copied from above case:]
                // "For perspective projections, shadow texel size varies with depth
                //  It will only work well if done in receiver side in the pixel shader. Currently UniversalRP
                //  do bias on caster side in vertex shader. When we add shader quality tiers we can properly
                //  handle this. For now, as a poor approximation we do a constant bias and compute the size of
                //  the frustum as if it was orthogonal considering the size at mid point between near and far planes.
                //  Depending on how big the light range is, it will be good enough with some tweaks in bias"
                // Note: HDRP uses normalBias both in HDShadowUtils.CalcGuardAnglePerspective and HDShadowAlgorithms/EvalShadow_NormalBias (receiver bias)
                float fovBias = Internal.AdditionalLightsShadowCasterPass.GetPointLightShadowFrustumFovBiasInDegrees((int)shadowResolution, (shadowLight.light.shadows == LightShadows.Soft));
                // Note: the same fovBias was also used to compute ShadowUtils.ExtractPointLightMatrix
                float cubeFaceAngle = 90 + fovBias;
                frustumSize = Mathf.Tan(cubeFaceAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range; // half-width (in world-space units) of shadow frustum's "far plane"
            }
            else
            {
                Debug.LogWarning("Only point, spot and directional shadow casters are supported in universal pipeline");
                frustumSize = 0.0f;
            }

            // depth and normal bias scale is in shadowmap texel size in world space
            // 阴影贴图中一个纹素在世界空间中的大小
            float texelSize = frustumSize / shadowResolution;
            // 深度偏移：设置中的depth bias * 纹素大小 = 世界空间中的实际深度偏移
            float depthBias = -shadowData.bias[shadowLightIndex].x * texelSize;
            // 法线偏移：原理同深度偏移
            float normalBias = -shadowData.bias[shadowLightIndex].y * texelSize;

            // The current implementation of NormalBias in Universal RP is the same as in Unity Built-In RP (i.e moving shadow caster vertices along normals when projecting them to the shadow map).
            // This does not work well with Point Lights, which is why NormalBias value is hard-coded to 0.0 in Built-In RP (see value of unity_LightShadowBias.z in FrameDebugger, and native code that sets it: https://github.cds.internal.unity3d.com/unity/unity/blob/a9c916ba27984da43724ba18e70f51469e0c34f5/Runtime/Camera/Shadows.cpp#L1686 )
            // We follow the same convention in Universal RP:
            if (shadowLight.lightType == LightType.Point)
                normalBias = 0.0f;

            if (shadowData.supportsSoftShadows && shadowLight.light.shadows == LightShadows.Soft)
            {
                // TODO: depth and normal bias assume sample is no more than 1 texel away from shadowmap
                // This is not true with PCF. Ideally we need to do either
                // cone base bias (based on distance to center sample)
                // or receiver place bias based on derivatives.
                // For now we scale it by the PCF kernel size of non-mobile platforms (5x5)
                // 根据滤波核大小放大偏移,这里使用5x5滤波核，半径2.5
                const float kernelRadius = 2.5f;
                depthBias *= kernelRadius;
                normalBias *= kernelRadius;
            }

            return new Vector4(depthBias, normalBias, 0.0f, 0.0f);
        }

        /// <summary>
        /// Extract scale and bias from a fade distance to achieve a linear fading of the fade distance.
        /// </summary>
        /// <param name="fadeDistance">Distance at which object should be totally fade</param>
        /// <param name="border">Normalized distance of fade</param>
        /// <param name="scale">[OUT] Slope of the fading on the fading part</param>
        /// <param name="bias">[OUT] Ordinate of the fading part at abscissa 0</param>
        internal static void GetScaleAndBiasForLinearDistanceFade(float fadeDistance, float border, out float scale, out float bias)
        {
            // To avoid division from zero
            // This values ensure that fade within cascade will be 0 and outside 1
            if (border < 0.0001f)
            {
                float multiplier = 1000f; // To avoid blending if difference is in fractions
                scale = multiplier;
                bias = -fadeDistance * multiplier;
                return;
            }

            border = 1 - border;
            border *= border;

            // Fade with distance calculation is just a linear fade from 90% of fade distance to fade distance. 90% arbitrarily chosen but should work well enough.
            float distanceFadeNear = border * fadeDistance;
            scale = 1.0f / (fadeDistance - distanceFadeNear);
            bias = -distanceFadeNear / (fadeDistance - distanceFadeNear);
        }
        
        /// <summary>
        /// 设置阴影偏移、光照方向和光照位置（非方向光）
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="shadowLight"></param>
        /// <param name="shadowBias"></param>
        public static void SetupShadowCasterConstantBuffer(CommandBuffer cmd, ref VisibleLight shadowLight, Vector4 shadowBias)
        {
            cmd.SetGlobalVector("_ShadowBias", shadowBias);

            // Light direction is currently used in shadow caster pass to apply shadow normal offset (normal bias).
            Vector3 lightDirection = -shadowLight.localToWorldMatrix.GetColumn(2);
            cmd.SetGlobalVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

            // For punctual lights, computing light direction at each vertex position provides more consistent results (shadow shape does not change when "rotating the point light" for example)
            Vector3 lightPosition = shadowLight.localToWorldMatrix.GetColumn(3);
            cmd.SetGlobalVector("_LightPosition", new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
        }

        /// <summary>
        /// 创建阴影纹理，给定纹理的宽、高和深度所占的位数
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bits"></param>
        /// <returns></returns>
        public static RenderTexture GetTemporaryShadowTexture(int width, int height, int bits)
        {
            // 根据深度位数和stencil位数，返回GraphicFormat
            var format = Experimental.Rendering.GraphicsFormatUtility.GetDepthStencilFormat(bits, 0);

            // 创建shadow texture的描述
            RenderTextureDescriptor rtd = new RenderTextureDescriptor(width, height, Experimental.Rendering.GraphicsFormat.None, format);

            // 选择阴影采样模式
            rtd.shadowSamplingMode = (RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap)
                && (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2)) ?
                ShadowSamplingMode.CompareDepths : ShadowSamplingMode.None;

            // 创建阴影纹理
            var shadowTexture = RenderTexture.GetTemporary(rtd);
            shadowTexture.filterMode = m_ForceShadowPointSampling ? FilterMode.Point : FilterMode.Bilinear;
            shadowTexture.wrapMode = TextureWrapMode.Clamp;
            return shadowTexture;
        }

        /// <summary>
        /// 生成世界空间到阴影贴图纹理空间的转换矩阵
        /// </summary>
        /// <param name="proj"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        static Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)
        {
            // Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives doesn't
            // apply z reversal to projection matrix. We need to do it manually here.
            // 确保深度比较在不同平台下的一致性(DX:[1,0],OpenGL:[0,1])
            if (SystemInfo.usesReversedZBuffer)
            {
                proj.m20 = -proj.m20;
                proj.m21 = -proj.m21;
                proj.m22 = -proj.m22;
                proj.m23 = -proj.m23;
            }

            // 光源裁剪空间矩阵,坐标范围[-1,1]
            Matrix4x4 worldToShadow = proj * view;

            // 转换到纹理空间：[-1,1] -> [0,1],先缩放0.5,再偏移0.5
            var textureScaleAndBias = Matrix4x4.identity;
            // 对角线：缩放0.5
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            // 矩阵第三列偏移0.5
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            // textureScaleAndBias maps texture space coordinates from [-1,1] to [0,1]

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }
    }
}

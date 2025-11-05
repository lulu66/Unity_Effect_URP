using System.Collections.Generic;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Rendering modes for Universal renderer.
    /// </summary>
    public enum RenderingMode
    {
        /// <summary>Render all objects and lighting in one pass, with a hard limit on the number of lights that can be applied on an object.</summary>
        Forward,
        /// <summary>Render all objects first in a g-buffer pass, then apply all lighting in a separate pass using deferred shading.</summary>
        Deferred
    };

    /// <summary>
    /// When the Universal Renderer should use Depth Priming in Forward mode.
    /// depth priming:深度预填充，一种优化技术，提高渲染性能，应该就是early z的意思
    /// </summary>
    public enum DepthPrimingMode
    {
        /// <summary>Depth Priming will never be used.</summary>
        Disabled,
        /// <summary>Depth Priming will only be used if there is a depth prepass needed by any of the render passes.</summary>
        Auto,
        /// <summary>A depth prepass will be explicitly requested so Depth Priming can be used.</summary>
        Forced,
    }

    /// <summary>
    /// Default renderer for Universal RP.
    /// This renderer is supported on all Universal RP supported platforms.
    /// It uses a classic forward rendering strategy with per-object light culling.
    /// </summary>
    public sealed class UniversalRenderer : ScriptableRenderer
    {
        #if UNITY_SWITCH || UNITY_ANDROID
        internal const int k_DepthStencilBufferBits = 24;
        #else
        internal const int k_DepthStencilBufferBits = 32;
        #endif
        static readonly List<ShaderTagId> k_DepthNormalsOnly = new List<ShaderTagId> { new ShaderTagId("DepthNormalsOnly") };

        private static class Profiling
        {
            private const string k_Name = nameof(UniversalRenderer);
            public static readonly ProfilingSampler createCameraRenderTarget = new ProfilingSampler($"{k_Name}.{nameof(CreateCameraRenderTarget)}");
        }

        /// <inheritdoc/>
        /// 定义不同渲染模式下支持的相机堆叠类型
        public override int SupportedCameraStackingTypes()
        {
            switch (m_RenderingMode)
            {
                // 用1 << 类型值创建标记位；| ：组合多个标记；支持两种相机类型Base和Overlay
                case RenderingMode.Forward:
                    return 1 << (int)CameraRenderType.Base | 1 << (int)CameraRenderType.Overlay;
                // 仅支持Base相机类型
                case RenderingMode.Deferred:
                    return 1 << (int)CameraRenderType.Base;
                default:
                    return 0;
            }
        }

        // Rendering mode setup from UI.
        internal RenderingMode renderingMode => m_RenderingMode;

        // Actual rendering mode, which may be different (ex: wireframe rendering, harware not capable of deferred rendering).
        internal RenderingMode actualRenderingMode => (GL.wireframe || (DebugHandler != null && DebugHandler.IsActiveModeUnsupportedForDeferred) || m_DeferredLights == null || !m_DeferredLights.IsRuntimeSupportedThisFrame() || m_DeferredLights.IsOverlay)
        ? RenderingMode.Forward
        : this.renderingMode;

        internal bool accurateGbufferNormals => m_DeferredLights != null ? m_DeferredLights.AccurateGbufferNormals : false;

#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
        internal bool needTransparencyPass { get { return !UniversalRenderPipeline.asset.useAdaptivePerformance || !AdaptivePerformance.AdaptivePerformanceRenderSettings.SkipTransparentObjects;; } }
#endif
        /// <summary>Property to control the depth priming behavior of the forward rendering path.</summary>
        /// 在前向渲染中使用的深度预填充设置
        public DepthPrimingMode depthPrimingMode { get { return m_DepthPrimingMode; } set { m_DepthPrimingMode = value; } }

        // 渲染所有拥有'DepthOnly' pass的物体到给定的depth buffer
        DepthOnlyPass m_DepthPrepass;
        DepthNormalOnlyPass m_DepthNormalPrepass;
        CopyDepthPass m_PrimedDepthCopyPass;
        MotionVectorRenderPass m_MotionVectorPass;
        MainLightShadowCasterPass m_MainLightShadowCasterPass;
        AdditionalLightsShadowCasterPass m_AdditionalLightsShadowCasterPass;
        GBufferPass m_GBufferPass;
        CopyDepthPass m_GBufferCopyDepthPass;
        TileDepthRangePass m_TileDepthRangePass;
        TileDepthRangePass m_TileDepthRangeExtraPass; // TODO use subpass API to hide this pass
        DeferredPass m_DeferredPass;
        // 延迟渲染相关的pass
        DrawObjectsPass m_RenderOpaqueForwardOnlyPass;
        DrawObjectsPass m_RenderOpaqueForwardPass;
        DrawSkyboxPass m_DrawSkyboxPass;
        CopyDepthPass m_CopyDepthPass;
        CopyColorPass m_CopyColorPass;
        TransparentSettingsPass m_TransparentSettingsPass;
        DrawObjectsPass m_RenderTransparentForwardPass;
        InvokeOnRenderObjectCallbackPass m_OnRenderObjectCallbackPass;
        FinalBlitPass m_FinalBlitPass;
        CapturePass m_CapturePass;
#if ENABLE_VR && ENABLE_XR_MODULE
        XROcclusionMeshPass m_XROcclusionMeshPass;
        CopyDepthPass m_XRCopyDepthPass;
#endif
#if UNITY_EDITOR
        CopyDepthPass m_FinalDepthCopyPass;
#endif
        // 渲染目标管理系统
        internal RenderTargetBufferSystem m_ColorBufferSystem;

        // 当前被激活的color texture
        RenderTargetHandle m_ActiveCameraColorAttachment;
        RenderTargetHandle m_ColorFrontBuffer;
        // 当前被激活的depth texture
        RenderTargetHandle m_ActiveCameraDepthAttachment;
        RenderTargetHandle m_CameraDepthAttachment;
        RenderTargetHandle m_DepthTexture;
        RenderTargetHandle m_NormalsTexture;
        RenderTargetHandle m_OpaqueColor;
        // For tiled-deferred shading.
        RenderTargetHandle m_DepthInfoTexture;
        RenderTargetHandle m_TileDepthInfoTexture;

        // 前向渲染灯光
        ForwardLights m_ForwardLights;
        DeferredLights m_DeferredLights;

        // 渲染模式：forward or defferred
        RenderingMode m_RenderingMode;

        // 深度预填充模式
        DepthPrimingMode m_DepthPrimingMode;

        // 深度拷贝的时机
        CopyDepthMode m_CopyDepthMode;
        bool m_DepthPrimingRecommended;
        StencilState m_DefaultStencilState;
        LightCookieManager m_LightCookieManager;

        // urp中一个选项配置，控制中间纹理的处理方式
        // Auto:URP自行决定何时创建中间纹理
        // Always:总是为相机创建中间纹理
        IntermediateTextureMode m_IntermediateTextureMode;
        bool m_VulkanEnablePreTransform;

        // Materials used in URP Scriptable Render Passes
        Material m_BlitMaterial = null;
        Material m_CopyDepthMaterial = null;
        Material m_SamplingMaterial = null;
        Material m_TileDepthInfoMaterial = null;
        Material m_TileDeferredMaterial = null;
        Material m_StencilDeferredMaterial = null;
        Material m_CameraMotionVecMaterial = null;
        Material m_ObjectMotionVecMaterial = null;

        PostProcessPasses m_PostProcessPasses;
        internal ColorGradingLutPass colorGradingLutPass { get => m_PostProcessPasses.colorGradingLutPass; }
        internal PostProcessPass postProcessPass { get => m_PostProcessPasses.postProcessPass; }
        internal PostProcessPass finalPostProcessPass { get => m_PostProcessPasses.finalPostProcessPass; }
        internal RenderTargetHandle colorGradingLut { get => m_PostProcessPasses.colorGradingLut; }
        internal DeferredLights deferredLights { get => m_DeferredLights; }

#if ENABLE_VR && ENABLE_VR_MODULE
#if PLATFORM_WINRT || PLATFORM_ANDROID
        // XRTODO: Remove this platform specific code(runs on Quest and HL).
        static List<XR.XRDisplaySubsystem> displaySubsystemList = new List<XR.XRDisplaySubsystem>();
        internal static bool IsRunningXRMobile()
        {
            var platform = Application.platform;
            if (platform == RuntimePlatform.WSAPlayerX86 || platform == RuntimePlatform.WSAPlayerARM || platform == RuntimePlatform.WSAPlayerX64 || platform == RuntimePlatform.Android)
            {
                XR.XRDisplaySubsystem display = null;
                SubsystemManager.GetInstances(displaySubsystemList);
                if (displaySubsystemList.Count > 0)
                    display = displaySubsystemList[0];
                if (display != null)
                    return true;
            }
            return false;
        }
#endif
#endif

        public UniversalRenderer(UniversalRendererData data) : base(data)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            UniversalRenderPipeline.m_XRSystem.InitializeXRSystemData(data.xrSystemData);
#endif
            // TODO: should merge shaders with HDRP into core, XR dependency for now.
            // TODO: replace/merge URP blit into core blitter.
            Blitter.Initialize(data.shaders.coreBlitPS, data.shaders.coreBlitColorAndDepthPS);

            m_BlitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);
            m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);
            m_SamplingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.samplingPS);
            //m_TileDepthInfoMaterial = CoreUtils.CreateEngineMaterial(data.shaders.tileDepthInfoPS);
            //m_TileDeferredMaterial = CoreUtils.CreateEngineMaterial(data.shaders.tileDeferredPS);
            m_StencilDeferredMaterial = CoreUtils.CreateEngineMaterial(data.shaders.stencilDeferredPS);
            m_CameraMotionVecMaterial = CoreUtils.CreateEngineMaterial(data.shaders.cameraMotionVector);
            m_ObjectMotionVecMaterial = CoreUtils.CreateEngineMaterial(data.shaders.objectMotionVector);

            StencilStateData stencilData = data.defaultStencilState;
            m_DefaultStencilState = StencilState.defaultValue;
            m_DefaultStencilState.enabled = stencilData.overrideStencilState;
            m_DefaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            m_DefaultStencilState.SetPassOperation(stencilData.passOperation);
            m_DefaultStencilState.SetFailOperation(stencilData.failOperation);
            m_DefaultStencilState.SetZFailOperation(stencilData.zFailOperation);

            m_IntermediateTextureMode = data.intermediateTextureMode;

            {
                var settings = LightCookieManager.Settings.GetDefault();
                var asset = UniversalRenderPipeline.asset;
                if (asset)
                {
                    settings.atlas.format = asset.additionalLightsCookieFormat;
                    settings.atlas.resolution = asset.additionalLightsCookieResolution;
                }

                m_LightCookieManager = new LightCookieManager(ref settings);
            }

            this.stripShadowsOffVariants = true;
            this.stripAdditionalLightOffVariants = true;
#if ENABLE_VR && ENABLE_VR_MODULE
#if PLATFORM_WINRT || PLATFORM_ANDROID
            // AdditionalLightOff variant is available on HL&Quest platform due to performance consideration.
            this.stripAdditionalLightOffVariants = !IsRunningXRMobile();
#endif
#endif

            ForwardLights.InitParams forwardInitParams;
            forwardInitParams.lightCookieManager = m_LightCookieManager;
            forwardInitParams.clusteredRendering = data.clusteredRendering;
            forwardInitParams.tileSize = (int)data.tileSize;
            m_ForwardLights = new ForwardLights(forwardInitParams);
            //m_DeferredLights.LightCulling = data.lightCulling;
            this.m_RenderingMode = data.renderingMode;
            this.m_DepthPrimingMode = data.depthPrimingMode;
            this.m_CopyDepthMode = data.copyDepthMode;
            useRenderPassEnabled = data.useNativeRenderPass && SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2
                                                            && !SystemInfo.graphicsDeviceName.Contains("Apple M"); // Apple Silicon does not support Native Render Pass on 2021.3;

#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS
            this.m_DepthPrimingRecommended = false;
#else
            this.m_DepthPrimingRecommended = true;
#endif

            // Note: Since all custom render passes inject first and we have stable sort,
            // we inject the builtin passes in the before events.
            m_MainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            m_AdditionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);

#if ENABLE_VR && ENABLE_XR_MODULE
            m_XROcclusionMeshPass = new XROcclusionMeshPass(RenderPassEvent.BeforeRenderingOpaques);
            // Schedule XR copydepth right after m_FinalBlitPass(AfterRendering + 1)
            m_XRCopyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRendering + 2, m_CopyDepthMaterial);
#endif
            m_DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrePasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            m_DepthNormalPrepass = new DepthNormalOnlyPass(RenderPassEvent.BeforeRenderingPrePasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            m_MotionVectorPass = new MotionVectorRenderPass(m_CameraMotionVecMaterial, m_ObjectMotionVecMaterial);

            if (this.renderingMode == RenderingMode.Forward)
            {
                m_PrimedDepthCopyPass = new CopyDepthPass(RenderPassEvent.AfterRenderingPrePasses, m_CopyDepthMaterial);
            }

            if (this.renderingMode == RenderingMode.Deferred)
            {
                var deferredInitParams = new DeferredLights.InitParams();
                deferredInitParams.tileDepthInfoMaterial = m_TileDepthInfoMaterial;
                deferredInitParams.tileDeferredMaterial = m_TileDeferredMaterial;
                deferredInitParams.stencilDeferredMaterial = m_StencilDeferredMaterial;
                deferredInitParams.lightCookieManager = m_LightCookieManager;
                m_DeferredLights = new DeferredLights(deferredInitParams, useRenderPassEnabled);
                m_DeferredLights.AccurateGbufferNormals = data.accurateGbufferNormals;
                //m_DeferredLights.TiledDeferredShading = data.tiledDeferredShading;
                m_DeferredLights.TiledDeferredShading = false;

                m_GBufferPass = new GBufferPass(RenderPassEvent.BeforeRenderingGbuffer, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference, m_DeferredLights);
                // Forward-only pass only runs if deferred renderer is enabled.
                // It allows specific materials to be rendered in a forward-like pass.
                // We render both gbuffer pass and forward-only pass before the deferred lighting pass so we can minimize copies of depth buffer and
                // benefits from some depth rejection.
                // - If a material can be rendered either forward or deferred, then it should declare a UniversalForward and a UniversalGBuffer pass.
                // - If a material cannot be lit in deferred (unlit, bakedLit, special material such as hair, skin shader), then it should declare UniversalForwardOnly pass
                // - Legacy materials have unamed pass, which is implicitely renamed as SRPDefaultUnlit. In that case, they are considered forward-only too.
                // TO declare a material with unnamed pass and UniversalForward/UniversalForwardOnly pass is an ERROR, as the material will be rendered twice.
                StencilState forwardOnlyStencilState = DeferredLights.OverwriteStencil(m_DefaultStencilState, (int)StencilUsage.MaterialMask);
                ShaderTagId[] forwardOnlyShaderTagIds = new ShaderTagId[]
                {
                    new ShaderTagId("UniversalForwardOnly"),
                    new ShaderTagId("SRPDefaultUnlit"), // Legacy shaders (do not have a gbuffer pass) are considered forward-only for backward compatibility
                    new ShaderTagId("LightweightForward") // Legacy shaders (do not have a gbuffer pass) are considered forward-only for backward compatibility
                };
                int forwardOnlyStencilRef = stencilData.stencilReference | (int)StencilUsage.MaterialUnlit;
                m_GBufferCopyDepthPass = new CopyDepthPass(RenderPassEvent.BeforeRenderingGbuffer + 1, m_CopyDepthMaterial);
                m_TileDepthRangePass = new TileDepthRangePass(RenderPassEvent.BeforeRenderingGbuffer + 2, m_DeferredLights, 0);
                m_TileDepthRangeExtraPass = new TileDepthRangePass(RenderPassEvent.BeforeRenderingGbuffer + 3, m_DeferredLights, 1);
                m_DeferredPass = new DeferredPass(RenderPassEvent.BeforeRenderingDeferredLights, m_DeferredLights);

                // 延迟渲染相关的pass
                m_RenderOpaqueForwardOnlyPass = new DrawObjectsPass("Render Opaques Forward Only", forwardOnlyShaderTagIds, true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, forwardOnlyStencilState, forwardOnlyStencilRef);
            }

            // Always create this pass even in deferred because we use it for wireframe rendering in the Editor or offscreen depth texture rendering.
            m_RenderOpaqueForwardPass = new DrawObjectsPass(URPProfileId.DrawOpaqueObjects, true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference);

            m_CopyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRenderingSkybox, m_CopyDepthMaterial);
            m_DrawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
            m_CopyColorPass = new CopyColorPass(RenderPassEvent.AfterRenderingSkybox, m_SamplingMaterial, m_BlitMaterial);
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            if (needTransparencyPass)
#endif
            {
                m_TransparentSettingsPass = new TransparentSettingsPass(RenderPassEvent.BeforeRenderingTransparents, data.shadowTransparentReceive);
                m_RenderTransparentForwardPass = new DrawObjectsPass(URPProfileId.DrawTransparentObjects, false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            }
            m_OnRenderObjectCallbackPass = new InvokeOnRenderObjectCallbackPass(RenderPassEvent.BeforeRenderingPostProcessing);

            m_PostProcessPasses = new PostProcessPasses(data.postProcessData, m_BlitMaterial);

            m_CapturePass = new CapturePass(RenderPassEvent.AfterRendering);
            m_FinalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering + 1, m_BlitMaterial);

#if UNITY_EDITOR
            m_FinalDepthCopyPass = new CopyDepthPass(RenderPassEvent.AfterRendering + 9, m_CopyDepthMaterial);
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            // 初始化渲染目标管理系统，创建两个用于交换的buffer:_CameraColorAttachmentA和_CameraColorAttachmentB;
            m_ColorBufferSystem = new RenderTargetBufferSystem("_CameraColorAttachment");
            m_CameraDepthAttachment.Init("_CameraDepthAttachment");
            m_DepthTexture.Init("_CameraDepthTexture");
            m_NormalsTexture.Init("_CameraNormalsTexture");
            m_OpaqueColor.Init("_CameraOpaqueTexture");
            m_DepthInfoTexture.Init("_DepthInfoTexture");
            m_TileDepthInfoTexture.Init("_TileDepthInfoTexture");

            supportedRenderingFeatures = new RenderingFeatures();

            if (this.renderingMode == RenderingMode.Deferred)
            {
                // Deferred rendering does not support MSAA.
                this.supportedRenderingFeatures.msaa = false;

                // Avoid legacy platforms: use vulkan instead.
                unsupportedGraphicsDeviceTypes = new GraphicsDeviceType[]
                {
                    GraphicsDeviceType.OpenGLCore,
                    GraphicsDeviceType.OpenGLES2,
                    GraphicsDeviceType.OpenGLES3
                };
            }

            LensFlareCommonSRP.mergeNeeded = 0;
            LensFlareCommonSRP.maxLensFlareWithOcclusionTemporalSample = 1;
            LensFlareCommonSRP.Initialize();

            m_VulkanEnablePreTransform = GraphicsSettings.HasShaderDefine(BuiltinShaderDefine.UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            m_ForwardLights.Cleanup();
            m_PostProcessPasses.Dispose();

            base.Dispose(disposing);
            CoreUtils.Destroy(m_BlitMaterial);
            CoreUtils.Destroy(m_CopyDepthMaterial);
            CoreUtils.Destroy(m_SamplingMaterial);
            CoreUtils.Destroy(m_TileDepthInfoMaterial);
            CoreUtils.Destroy(m_TileDeferredMaterial);
            CoreUtils.Destroy(m_StencilDeferredMaterial);
            CoreUtils.Destroy(m_CameraMotionVecMaterial);
            CoreUtils.Destroy(m_ObjectMotionVecMaterial);

            Blitter.Cleanup();

            LensFlareCommonSRP.Dispose();
        }

        private void SetupFinalPassDebug(ref CameraData cameraData)
        {
            if ((DebugHandler != null) && DebugHandler.IsActiveForCamera(ref cameraData))
            {
                if (DebugHandler.TryGetFullscreenDebugMode(out DebugFullScreenMode fullScreenDebugMode, out int textureHeightPercent))
                {
                    Camera camera = cameraData.camera;
                    float screenWidth = camera.pixelWidth;
                    float screenHeight = camera.pixelHeight;
                    float height = Mathf.Clamp01(textureHeightPercent / 100f) * screenHeight;
                    float width = height * (screenWidth / screenHeight);
                    float normalizedSizeX = width / screenWidth;
                    float normalizedSizeY = height / screenHeight;
                    Rect normalizedRect = new Rect(1 - normalizedSizeX, 1 - normalizedSizeY, normalizedSizeX, normalizedSizeY);

                    switch (fullScreenDebugMode)
                    {
                        case DebugFullScreenMode.Depth:
                        {
                            DebugHandler.SetDebugRenderTarget(m_DepthTexture.Identifier(), normalizedRect, true);
                            break;
                        }
                        case DebugFullScreenMode.AdditionalLightsShadowMap:
                        {
                            DebugHandler.SetDebugRenderTarget(m_AdditionalLightsShadowCasterPass.m_AdditionalLightsShadowmapTexture, normalizedRect, false);
                            break;
                        }
                        case DebugFullScreenMode.MainLightShadowMap:
                        {
                            DebugHandler.SetDebugRenderTarget(m_MainLightShadowCasterPass.m_MainLightShadowmapTexture, normalizedRect, false);
                            break;
                        }
                        default:
                        {
                            break;
                        }
                    }
                }
                else
                {
                    DebugHandler.ResetDebugRenderTarget();
                }
            }
        }

        /// <summary>
        /// 是否开启dpeth priming
        /// </summary>
        /// <param name="cameraData"></param>
        /// <returns></returns>
        bool IsDepthPrimingEnabled(ref CameraData cameraData)
        {
            // depth priming requires an extra depth copy, disable it on platforms not supporting it (like GLES when MSAA is on)
            // depth priming需要深度拷贝，若不支持深度拷贝，则不支持depth priming
            if (!CanCopyDepth(ref cameraData))
                return false;

            // 是否需要depth priming(设置中强制开启了depth priming；或者设置为auto，且系统推荐depth priming的情况下)
            bool depthPrimingRequested = (m_DepthPrimingRecommended && m_DepthPrimingMode == DepthPrimingMode.Auto) || m_DepthPrimingMode == DepthPrimingMode.Forced;

            // 是否是前向渲染模式
            bool isForwardRenderingMode = m_RenderingMode == RenderingMode.Forward;

            // 是否写入深度(base camera和camera设置为clear depth需要写入深度)
            bool isFirstCameraToWriteDepth = cameraData.renderType == CameraRenderType.Base || cameraData.clearDepth;

            // Enabled Depth priming when baking Reflection Probes causes artefacts (UUM-12397)
            // 是否是反射相机
            bool isNotReflectionCamera = cameraData.cameraType != CameraType.Reflection;

            // 如果需要depth priming且是前向渲染管线且需要写入深度且不是反射相机且渲染方式不是线框，则开启depth priming
            return depthPrimingRequested && isForwardRenderingMode && isFirstCameraToWriteDepth && isNotReflectionCamera && !GL.wireframe ;
        }

        /// <inheritdoc />
        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 处理灯光（只针对簇渲染）
            m_ForwardLights.ProcessLights(ref renderingData);

            // 获取相机和相机rt格式
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;
            RenderTextureDescriptor cameraTargetDescriptor = cameraData.cameraTargetDescriptor;

            DebugHandler?.Setup(context, ref cameraData);

            // 非游戏相机不适用RenderPass的渲染方式
            if (cameraData.cameraType != CameraType.Game)
                useRenderPassEnabled = false;

            // Special path for depth only offscreen cameras. Only write opaques + transparents.
            // 针对offscreen camera,只渲染不透明和透明物体的深度
            bool isOffscreenDepthTexture = cameraData.targetTexture != null && cameraData.targetTexture.format == RenderTextureFormat.Depth;

            // 如果渲染离线的深度纹理
            if (isOffscreenDepthTexture)
            {
                // 将渲染rt设置为camera rt
                ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);

                // 将自定义renderfeature的render pass加入激活的render pass队列中
                AddRenderPasses(ref renderingData);

                // 将绘制不透明物体的render pass加入激活的render pass队列中
                EnqueuePass(m_RenderOpaqueForwardPass);

                // TODO: Do we need to inject transparents and skybox when rendering depth only camera? They don't write to depth.
                // 将绘制skybox的render pass加入激活的render pass队列中
                EnqueuePass(m_DrawSkyboxPass);
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
                if (!needTransparencyPass)
                    return;
#endif
                // 将绘制半透的render pass加入激活的render pass队列中
                EnqueuePass(m_RenderTransparentForwardPass);
                return;
            }

            if (m_DeferredLights != null)
            {
                m_DeferredLights.ResolveMixedLightingMode(ref renderingData);
                m_DeferredLights.IsOverlay = cameraData.renderType == CameraRenderType.Overlay;
            }

            // Assign the camera color target early in case it is needed during AddRenderPasses.
            // 创建中间颜色纹理
            bool isPreviewCamera = cameraData.isPreviewCamera;
            var createColorTexture = (rendererFeatures.Count != 0 && m_IntermediateTextureMode == IntermediateTextureMode.Always) && !isPreviewCamera;
            if (createColorTexture)
            {
                // 获取back buffer
                m_ActiveCameraColorAttachment = m_ColorBufferSystem.GetBackBuffer();
                var activeColorRenderTargetId = m_ActiveCameraColorAttachment.Identifier();
#if ENABLE_VR && ENABLE_XR_MODULE
                if (cameraData.xr.enabled) activeColorRenderTargetId = new RenderTargetIdentifier(activeColorRenderTargetId, 0, CubemapFace.Unknown, -1);
#endif
                // 将当前的渲染目标设置为back buffer
                ConfigureCameraColorTarget(activeColorRenderTargetId);
            }

            // Add render passes and gather the input requirements
            isCameraColorTargetValid = true;
            // 添加自定义render feature的pass到激活队列中
            AddRenderPasses(ref renderingData);
            isCameraColorTargetValid = false;
            // 收集管线当前帧对所有renderpass的纹理需求
            RenderPassInputSummary renderPassInputs = GetRenderPassInputs(ref renderingData);

            // Should apply post-processing after rendering this camera?
            // 相机是否开启后处理
            bool applyPostProcessing = cameraData.postProcessEnabled && m_PostProcessPasses.isCreated;

            // There's at least a camera in the camera stack that applies post-processing
            // 整个渲染是否有后处理
            bool anyPostProcessing = renderingData.postProcessingEnabled && m_PostProcessPasses.isCreated;

            // If Camera's PostProcessing is enabled and if there any enabled PostProcessing requires depth texture as shader read resource (Motion Blur/DoF)
            // 判断后处理是否带深度
            bool cameraHasPostProcessingWithDepth = applyPostProcessing && cameraData.postProcessingRequiresDepthTexture;

            // TODO: We could cache and generate the LUT before rendering the stack
            // 如果开启后处理了就要生成color grading lut
            bool generateColorGradingLUT = cameraData.postProcessEnabled && m_PostProcessPasses.isCreated;

            // 判断是否是scene camera或者preview camera
            bool isSceneViewOrPreviewCamera = cameraData.isSceneViewCamera || cameraData.cameraType == CameraType.Preview;

            // 是否使用depth priming
            useDepthPriming = IsDepthPrimingEnabled(ref cameraData);

            // This indicates whether the renderer will output a depth texture.
            // 是否需要深度纹理（决定是否输出一个深度纹理）
            bool requiresDepthTexture = cameraData.requiresDepthTexture || renderPassInputs.requiresDepthTexture || useDepthPriming;

#if UNITY_EDITOR
            bool isGizmosEnabled = UnityEditor.Handles.ShouldRenderGizmos();
#else
            bool isGizmosEnabled = false;
#endif
            // 是否有主光源阴影
            bool mainLightShadows = m_MainLightShadowCasterPass.Setup(ref renderingData);

            // 额外光源是否有阴影
            bool additionalLightShadows = m_AdditionalLightsShadowCasterPass.Setup(ref renderingData);

            // 如果需要透明物体接受阴影，返回false,否则返回true
            bool transparentsNeedSettingsPass = m_TransparentSettingsPass.Setup(ref renderingData);

            // 是否要强制prepass
            bool forcePrepass = (m_CopyDepthMode == CopyDepthMode.ForcePrepass);

            // Depth prepass is generated in the following cases:
            // - If game or offscreen camera requires it we check if we can copy the depth from the rendering opaques pass and use that instead.
            // - Scene or preview cameras always require a depth texture. We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
            // - Render passes require it
            // 是否需要depth prepass? 如果需要深度纹理|后处理需要深度并且深度可以被拷贝或者强制prepass；scene camera或者preview camera总是需要depth texture;开启了gizmos需要深度纹理;renderpass需要depthprepass或者Normal纹理
            bool requiresDepthPrepass = (requiresDepthTexture || cameraHasPostProcessingWithDepth) && (!CanCopyDepth(ref renderingData.cameraData) || forcePrepass);
            requiresDepthPrepass |= isSceneViewOrPreviewCamera;
            requiresDepthPrepass |= isGizmosEnabled;
            requiresDepthPrepass |= isPreviewCamera;
            requiresDepthPrepass |= renderPassInputs.requiresDepthPrepass;
            requiresDepthPrepass |= renderPassInputs.requiresNormalsTexture;

            // Current aim of depth prepass is to generate a copy of depth buffer, it is NOT to prime depth buffer and reduce overdraw on non-mobile platforms.
            // When deferred renderer is enabled, depth buffer is already accessible so depth prepass is not needed.
            // The only exception is for generating depth-normal textures: SSAO pass needs it and it must run before forward-only geometry.
            // DepthNormal prepass will render:
            // - forward-only geometry when deferred renderer is enabled
            // - all geometry when forward renderer is enabled
            if (requiresDepthPrepass && this.actualRenderingMode == RenderingMode.Deferred && !renderPassInputs.requiresNormalsTexture)
                requiresDepthPrepass = false;

            // 如果使用了depth priming，也需要depth prepass
            requiresDepthPrepass |= useDepthPriming;

            // If possible try to merge the opaque and skybox passes instead of splitting them when "Depth Texture" is required.
            // The copying of depth should normally happen after rendering opaques.
            // But if we only require it for post processing or the scene camera then we do it after rendering transparent objects
            // Aim to have the most optimized render pass event for Depth Copy (The aim is to minimize the number of render passes)
            // 确定拷贝深度的时机：通常是渲染完不透明物体之后，但是如果仅在后处理或者scene camera需要它，则在渲染完半透物体之后拷贝；
            if (requiresDepthTexture)
            {
                RenderPassEvent copyDepthPassEvent = RenderPassEvent.AfterRenderingOpaques;
                // RenderPassInputs's requiresDepthTexture is configured through ScriptableRenderPass's ConfigureInput function
                if (renderPassInputs.requiresDepthTexture)
                {
                    // Do depth copy before the render pass that requires depth texture as shader read resource
                    copyDepthPassEvent = (RenderPassEvent)Mathf.Min((int)RenderPassEvent.AfterRenderingTransparents, ((int)renderPassInputs.requiresDepthTextureEarliestEvent) - 1);
                }
                m_CopyDepthPass.renderPassEvent = copyDepthPassEvent;
            }
            else if (cameraHasPostProcessingWithDepth || isSceneViewOrPreviewCamera || isGizmosEnabled)
            {
                // If only post process requires depth texture, we can re-use depth buffer from main geometry pass instead of enqueuing a depth copy pass, but no proper API to do that for now, so resort to depth copy pass for now
                m_CopyDepthPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            // 判断是否需要中间临时color纹理
            createColorTexture |= RequiresIntermediateColorTexture(ref cameraData);
            createColorTexture |= renderPassInputs.requiresColorTexture;
            createColorTexture |= renderPassInputs.requiresColorTextureCreated;
            createColorTexture &= !isPreviewCamera;

            // If camera requires depth and there's no depth pre-pass we create a depth texture that can be read later by effect requiring it.
            // When deferred renderer is enabled, we must always create a depth texture and CANNOT use BuiltinRenderTextureType.CameraTarget. This is to get
            // around a bug where during gbuffer pass (MRT pass), the camera depth attachment is correctly bound, but during
            // deferred pass ("camera color" + "camera depth"), the implicit depth surface of "camera color" is used instead of "camera depth",
            // because BuiltinRenderTextureType.CameraTarget for depth means there is no explicit depth attachment...
            // 判断是否需要创建深度纹理
            bool createDepthTexture = (requiresDepthTexture || cameraHasPostProcessingWithDepth) && !requiresDepthPrepass;
            createDepthTexture |= !cameraData.resolveFinalTarget;
            // Deferred renderer always need to access depth buffer.
            createDepthTexture |= (this.actualRenderingMode == RenderingMode.Deferred && !useRenderPassEnabled);
            // Some render cases (e.g. Material previews) have shown we need to create a depth texture when we're forcing a prepass.
            createDepthTexture |= useDepthPriming;

#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                // URP can't handle msaa/size mismatch between depth RT and color RT(for now we create intermediate textures to ensure they match)
                createDepthTexture |= createColorTexture;
                createColorTexture = createDepthTexture;
            }
#endif

#if UNITY_ANDROID || UNITY_WEBGL
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan || m_VulkanEnablePreTransform)
            {
                // GLES can not use render texture's depth buffer with the color buffer of the backbuffer
                // in such case we create a color texture for it too.
                // If Vulkan PreTransform is enabled we can't mix backbuffer and intermediate render target due to screen orientation mismatch
                createColorTexture |= createDepthTexture;
            }
#endif
            // Temporarily disable depth priming on certain platforms such as Vulkan because we lack proper depth resolve support.
            useDepthPriming &= SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan || cameraTargetDescriptor.msaaSamples == 1;

            if (useRenderPassEnabled || useDepthPriming)
            {
                createDepthTexture |= createColorTexture;
                createColorTexture = createDepthTexture;
            }

            // 将当前的camera target的描述设置给rt管理系统
            var colorDescriptor = cameraTargetDescriptor;
            colorDescriptor.useMipMap = false;
            colorDescriptor.autoGenerateMips = false;
            colorDescriptor.depthBufferBits = (int)DepthBits.None;
            m_ColorBufferSystem.SetCameraSettings(colorDescriptor, FilterMode.Bilinear);

            // Configure all settings require to start a new camera stack (base camera only)
            if (cameraData.renderType == CameraRenderType.Base)
            {
                RenderTargetHandle cameraTargetHandle = RenderTargetHandle.GetCameraTarget(cameraData.xr);

                // 过滤 Scene 视图中的可见对象，帮助开发者在编辑时专注于特定的游戏对象集合
                bool sceneViewFilterEnabled = camera.sceneViewFilterMode == Camera.SceneViewFilterMode.ShowFiltered;

                //Scene filtering redraws the objects on top of the resulting frame. It has to draw directly to the sceneview buffer.
                // 如果不对scene视图对象过滤且需要创建color texture，则将活跃color attachment设置为back buffer,活跃depth attachment设置为当前的深度附着
                m_ActiveCameraColorAttachment = (createColorTexture && !sceneViewFilterEnabled) ? m_ColorBufferSystem.GetBackBuffer() : cameraTargetHandle;
                m_ActiveCameraDepthAttachment = (createDepthTexture && !sceneViewFilterEnabled) ? m_CameraDepthAttachment : cameraTargetHandle;

                // 判断是否需要中间rt，如果需要中间rt,则使用渲染目标交换系统创建camera target
                bool intermediateRenderTexture = createColorTexture || createDepthTexture;

                // Doesn't create texture for Overlay cameras as they are already overlaying on top of created textures.
                if (intermediateRenderTexture)
                    CreateCameraRenderTarget(context, ref cameraTargetDescriptor, useDepthPriming);

                // 如果是back buffer，则绘制对象直接是CameraTarget
                m_RenderOpaqueForwardPass.m_IsActiveTargetBackBuffer = !intermediateRenderTexture;
                m_RenderTransparentForwardPass.m_IsActiveTargetBackBuffer = !intermediateRenderTexture;
#if ENABLE_VR && ENABLE_XR_MODULE
                m_XROcclusionMeshPass.m_IsActiveTargetBackBuffer = !intermediateRenderTexture;
#endif

            }
            else
            {
                m_ActiveCameraColorAttachment = m_ColorBufferSystem.GetBackBuffer();
                m_ActiveCameraDepthAttachment = m_CameraDepthAttachment;
            }

            cameraData.renderer.useDepthPriming = useDepthPriming;

            bool requiresDepthCopyPass = !requiresDepthPrepass
                && (requiresDepthTexture || cameraHasPostProcessingWithDepth)
                && createDepthTexture;
            bool copyColorPass = renderingData.cameraData.requiresOpaqueTexture || renderPassInputs.requiresColorTexture;

            if ((DebugHandler != null) && DebugHandler.IsActiveForCamera(ref cameraData))
            {
                DebugHandler.TryGetFullscreenDebugMode(out var fullScreenMode);
                if (fullScreenMode == DebugFullScreenMode.Depth)
                {
                    requiresDepthPrepass = true;
                }

                if (!DebugHandler.IsLightingActive)
                {
                    mainLightShadows = false;
                    additionalLightShadows = false;

                    if (!isSceneViewOrPreviewCamera)
                    {
                        requiresDepthPrepass = false;
                        useDepthPriming = false;
                        generateColorGradingLUT = false;
                        copyColorPass = false;
                        requiresDepthCopyPass = false;
                    }
                }

                if (useRenderPassEnabled)
                    useRenderPassEnabled = DebugHandler.IsRenderPassSupported;
            }

            // Assign camera targets (color and depth)
            {
                var activeColorRenderTargetId = m_ActiveCameraColorAttachment.Identifier();
                var activeDepthRenderTargetId = m_ActiveCameraDepthAttachment.Identifier();

#if ENABLE_VR && ENABLE_XR_MODULE
                if (cameraData.xr.enabled)
                {
                    activeColorRenderTargetId = new RenderTargetIdentifier(activeColorRenderTargetId, 0, CubemapFace.Unknown, -1);
                    activeDepthRenderTargetId = new RenderTargetIdentifier(activeDepthRenderTargetId, 0, CubemapFace.Unknown, -1);
                }
#endif
                // 将相机渲染目标设置为当前激活的color附着和depth附着
                ConfigureCameraTarget(activeColorRenderTargetId, activeDepthRenderTargetId);
            }

            // 检查在后处理之后是否还有pass
            bool hasPassesAfterPostProcessing = activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRenderingPostProcessing) != null;

            // 加入主光源阴影pass
            if (mainLightShadows)
                EnqueuePass(m_MainLightShadowCasterPass);

            // 加入额外光源阴影pass
            if (additionalLightShadows)
                EnqueuePass(m_AdditionalLightsShadowCasterPass);

            if (requiresDepthPrepass)
            {
                // 如果需要depth prepass和normal texture，则设置好DepthNormalPrepass,并加入到激活的pass队列
                if (renderPassInputs.requiresNormalsTexture)
                {
                    if (this.actualRenderingMode == RenderingMode.Deferred)
                    {
                        // In deferred mode, depth-normal prepass does really primes the depth and normal buffers, instead of creating a copy.
                        // It is necessary because we need to render depth&normal for forward-only geometry and it is the only way
                        // to get them before the SSAO pass.

                        int gbufferNormalIndex = m_DeferredLights.GBufferNormalSmoothnessIndex;
                        m_DepthNormalPrepass.Setup(cameraTargetDescriptor, m_ActiveCameraDepthAttachment, m_DeferredLights.GbufferAttachments[gbufferNormalIndex]);

                        // Change the normal format to the one used by the gbuffer.
                        RenderTextureDescriptor normalDescriptor = m_DepthNormalPrepass.normalDescriptor;
                        normalDescriptor.graphicsFormat = m_DeferredLights.GetGBufferFormat(gbufferNormalIndex);
                        m_DepthNormalPrepass.normalDescriptor = normalDescriptor;
                        // Depth is allocated by this renderer.
                        m_DepthNormalPrepass.allocateDepth = false;
                        // Only render forward-only geometry, as standard geometry will be rendered as normal into the gbuffer.
                        if (RenderPassEvent.AfterRenderingGbuffer <= renderPassInputs.requiresDepthNormalAtEvent &&
                            renderPassInputs.requiresDepthNormalAtEvent <= RenderPassEvent.BeforeRenderingOpaques)
                            m_DepthNormalPrepass.shaderTagIds = k_DepthNormalsOnly;
                    }
                    else
                    {
                        m_DepthNormalPrepass.Setup(cameraTargetDescriptor, m_DepthTexture, m_NormalsTexture);
                    }

                    EnqueuePass(m_DepthNormalPrepass);
                }
                else
                {
                    // Deferred renderer does not require a depth-prepass to generate samplable depth texture.
                    // 如果只需要depth prepass，则设置好DepthPrepass，并加入到激活的pass队列
                    if (this.actualRenderingMode != RenderingMode.Deferred)
                    {
                        m_DepthPrepass.Setup(cameraTargetDescriptor, m_DepthTexture);
                        EnqueuePass(m_DepthPrepass);
                    }
                }
            }

            // depth priming still needs to copy depth because the prepass doesn't target anymore CameraDepthTexture
            // TODO: this is unoptimal, investigate optimizations
            // 如果使用depth priming，则设置好PrimedDepthCopyPass，并加入pass队列
            if (useDepthPriming)
            {
                m_PrimedDepthCopyPass.Setup(m_ActiveCameraDepthAttachment, m_DepthTexture);
                m_PrimedDepthCopyPass.AllocateRT = false;

                EnqueuePass(m_PrimedDepthCopyPass);
            }
            // 如果生成color grading lut，则设置好color grading lut pass，并加入Pass队列
            if (generateColorGradingLUT)
            {
                colorGradingLutPass.Setup(colorGradingLut);
                EnqueuePass(colorGradingLutPass);
            }

#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.hasValidOcclusionMesh)
                EnqueuePass(m_XROcclusionMeshPass);
#endif

            bool lastCameraInTheStack = cameraData.resolveFinalTarget;

            if (this.actualRenderingMode == RenderingMode.Deferred)
            {
                if (m_DeferredLights.UseRenderPass && (RenderPassEvent.AfterRenderingGbuffer == renderPassInputs.requiresDepthNormalAtEvent || !useRenderPassEnabled))
                    m_DeferredLights.DisableFramebufferFetchInput();

                EnqueueDeferred(ref renderingData, requiresDepthPrepass, renderPassInputs.requiresNormalsTexture, mainLightShadows, additionalLightShadows);
            }
            else
            {
                // Optimized store actions are very important on tile based GPUs and have a great impact on performance.
                // if MSAA is enabled and any of the following passes need a copy of the color or depth target, make sure the MSAA'd surface is stored
                // if following passes won't use it then just resolve (the Resolve action will still store the resolved surface, but discard the MSAA'd surface, which is very expensive to store).
                // 设置不透明物体的绘制rt的存储行为：如果开启MSAA，则使用storeAndResolve,否则使用Store
                RenderBufferStoreAction opaquePassColorStoreAction = RenderBufferStoreAction.Store;
                if (cameraTargetDescriptor.msaaSamples > 1)
                    opaquePassColorStoreAction = copyColorPass ? RenderBufferStoreAction.StoreAndResolve : RenderBufferStoreAction.Store;


                // make sure we store the depth only if following passes need it.
                RenderBufferStoreAction opaquePassDepthStoreAction = (copyColorPass || requiresDepthCopyPass || !lastCameraInTheStack) ? RenderBufferStoreAction.Store : RenderBufferStoreAction.DontCare;
#if ENABLE_VR && ENABLE_XR_MODULE
                if (cameraData.xr.enabled && cameraData.xr.copyDepth)
                {
                    opaquePassDepthStoreAction = RenderBufferStoreAction.Store;
                }
#endif

                // 设置不透物体绘制pass的颜色和深度附着的rt数据存储方式
                m_RenderOpaqueForwardPass.ConfigureColorStoreAction(opaquePassColorStoreAction);
                m_RenderOpaqueForwardPass.ConfigureDepthStoreAction(opaquePassDepthStoreAction);

                EnqueuePass(m_RenderOpaqueForwardPass);
            }

            // skybox pass加入激活pass队列中
            if (camera.clearFlags == CameraClearFlags.Skybox && cameraData.renderType != CameraRenderType.Overlay)
            {
                if (RenderSettings.skybox != null || (camera.TryGetComponent(out Skybox cameraSkybox) && cameraSkybox.material != null))
                    EnqueuePass(m_DrawSkyboxPass);
            }

            // If a depth texture was created we necessarily need to copy it, otherwise we could have render it to a renderbuffer.
            // 如果需要深度拷贝pass，将深度拷贝队列加入队列
            if (requiresDepthCopyPass)
            {
                m_CopyDepthPass.Setup(m_ActiveCameraDepthAttachment, m_DepthTexture);

                if (this.actualRenderingMode == RenderingMode.Deferred && !useRenderPassEnabled)
                    m_CopyDepthPass.AllocateRT = false; // m_DepthTexture is already allocated by m_GBufferCopyDepthPass but it's not called when using RenderPass API.

                EnqueuePass(m_CopyDepthPass);
            }

            // Set the depth texture to the far Z if we do not have a depth prepass or copy depth
            // 如果不需要深度纹理，则设置默认的深度纹理为black(DX) or white texture(OpenGL)
            if (!requiresDepthPrepass && !requiresDepthCopyPass)
            {
                Shader.SetGlobalTexture(m_DepthTexture.id, SystemInfo.usesReversedZBuffer ? Texture2D.blackTexture : Texture2D.whiteTexture);
            }

            // 如果要拷贝颜色，则设置colorpass并加入队列
            if (copyColorPass)
            {
                // TODO: Downsampling method should be store in the renderer instead of in the asset.
                // We need to migrate this data to renderer. For now, we query the method in the active asset.
                Downsampling downsamplingMethod = UniversalRenderPipeline.asset.opaqueDownsampling;
                m_CopyColorPass.Setup(m_ActiveCameraColorAttachment.Identifier(), m_OpaqueColor, downsamplingMethod);
                EnqueuePass(m_CopyColorPass);
            }

            // 如果需要运动模糊，则设置motion vector pass并加入队列
            if (renderPassInputs.requiresMotionVectors && !cameraData.xr.enabled)
            {
                SupportedRenderingFeatures.active.motionVectors = true; // hack for enabling UI

                var data = MotionVectorRendering.instance.GetMotionDataForCamera(camera, cameraData);
                m_MotionVectorPass.Setup(data);
                EnqueuePass(m_MotionVectorPass);
            }

#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            if (needTransparencyPass)
#endif
            {
                // 加入绘制透明物体的pass
                if (transparentsNeedSettingsPass)
                {
                    EnqueuePass(m_TransparentSettingsPass);
                }

                // if this is not lastCameraInTheStack we still need to Store, since the MSAA buffer might be needed by the Overlay cameras
                // 设置透明物体绘制的rt数据存储方式：如果开启了MSAA，使用Resolve，否则使用Store，如果是最后一个相机，深度纹理数据则不用管
                RenderBufferStoreAction transparentPassColorStoreAction = cameraTargetDescriptor.msaaSamples > 1 && lastCameraInTheStack ? RenderBufferStoreAction.Resolve : RenderBufferStoreAction.Store;
                RenderBufferStoreAction transparentPassDepthStoreAction = lastCameraInTheStack ? RenderBufferStoreAction.DontCare : RenderBufferStoreAction.Store;

                // If CopyDepthPass pass event is scheduled on or after AfterRenderingTransparent, we will need to store the depth buffer or resolve (store for now until latest trunk has depth resolve support) it for MSAA case
                // 如果深度拷贝是在渲染完透明物体之后，则深度纹理数据的存储方式则为store
                if (requiresDepthCopyPass && m_CopyDepthPass.renderPassEvent >= RenderPassEvent.AfterRenderingTransparents)
                    transparentPassDepthStoreAction = RenderBufferStoreAction.Store;

                // 设置半透物体的color和depth附着的数据存储方式，并将pass加入队列
                m_RenderTransparentForwardPass.ConfigureColorStoreAction(transparentPassColorStoreAction);
                m_RenderTransparentForwardPass.ConfigureDepthStoreAction(transparentPassDepthStoreAction);

                EnqueuePass(m_RenderTransparentForwardPass);
            }

            // 将RenderObjectCallbackPass加入队列
            EnqueuePass(m_OnRenderObjectCallbackPass);

            // 是否需要捕捉操作？
            bool hasCaptureActions = renderingData.cameraData.captureActions != null && lastCameraInTheStack;

            // When FXAA or scaling is active, we must perform an additional pass at the end of the frame for the following reasons:
            // 1. FXAA expects to be the last shader running on the image before it's presented to the screen. Since users are allowed
            //    to add additional render passes after post processing occurs, we can't run FXAA until all of those passes complete as well.
            //    The FinalPost pass is guaranteed to execute after user authored passes so FXAA is always run inside of it.
            // 2. UberPost can only handle upscaling with linear filtering. All other filtering methods require the FinalPost pass.
            // 是否应用final post processing(final post process包括：相机是否开启fastApproximateAntialiasing，相机是否使用向上缩放rt操作且过滤方式为线性)？
            bool applyFinalPostProcessing = anyPostProcessing && lastCameraInTheStack &&
                ((renderingData.cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing) ||
                 ((renderingData.cameraData.imageScalingMode == ImageScalingMode.Upscaling) && (renderingData.cameraData.upscalingFilter != ImageUpscalingFilter.Linear)));

            // When post-processing is enabled we can use the stack to resolve rendering to camera target (screen or RT).
            // However when there are render passes executing after post we avoid resolving to screen so rendering continues (before sRGBConvertion etc)

            // 如果后处理后不存在要渲染的pass了 | 没有相机渲染后的捕获操作了 | 不会应用最后的后处理效果，则将后处理解析到cameraTarget上
            bool resolvePostProcessingToCameraTarget = !hasCaptureActions && !hasPassesAfterPostProcessing && !applyFinalPostProcessing;

            // 如果是stack上最后一个相机，
            if (lastCameraInTheStack)
            {
                SetupFinalPassDebug(ref cameraData);

                // Post-processing will resolve to final target. No need for final blit pass.
                // 如果应用postprocess，则加入队列,此步骤在msaa之前进行
                if (applyPostProcessing)
                {
                    // if resolving to screen we need to be able to perform sRGBConversion in post-processing if necessary
                    bool doSRGBConversion = resolvePostProcessingToCameraTarget;
                    postProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, resolvePostProcessingToCameraTarget, m_ActiveCameraDepthAttachment, colorGradingLut, applyFinalPostProcessing, doSRGBConversion);
                    EnqueuePass(postProcessPass);
                }

                var sourceForFinalPass = m_ActiveCameraColorAttachment;

                // Do FXAA or any other final post-processing effect that might need to run after AA.
                // 如果应用最后的postprocess，此步骤在msaa之后进行，则将finalPostProcessPass加入队列
                if (applyFinalPostProcessing)
                {
                    finalPostProcessPass.SetupFinalPass(sourceForFinalPass, true);
                    EnqueuePass(finalPostProcessPass);
                }

                // 如果有捕获操作，则将捕获pass加入队列
                if (renderingData.cameraData.captureActions != null)
                {
                    m_CapturePass.Setup(sourceForFinalPass);
                    EnqueuePass(m_CapturePass);
                }

                // if post-processing then we already resolved to camera target while doing post.
                // Also only do final blit if camera is not rendering to RT.
                // 判断是否渲染到camera target上了
                bool cameraTargetResolved =
                    // final PP always blit to camera target
                    applyFinalPostProcessing ||
                    // no final PP but we have PP stack. In that case it blit unless there are render pass after PP
                    (applyPostProcessing && !hasPassesAfterPostProcessing && !hasCaptureActions) ||
                    // offscreen camera rendering to a texture, we don't need a blit pass to resolve to screen
                    m_ActiveCameraColorAttachment == RenderTargetHandle.GetCameraTarget(cameraData.xr);

                // We need final blit to resolve to screen
                // 如果渲染到中间rt，则做最后的final blit pass，并加入队列
                if (!cameraTargetResolved)
                {
                    m_FinalBlitPass.Setup(cameraTargetDescriptor, sourceForFinalPass);
                    EnqueuePass(m_FinalBlitPass);
                }

#if ENABLE_VR && ENABLE_XR_MODULE
                if (cameraData.xr.enabled)
                {
                    bool depthTargetResolved =
                        // active depth is depth target, we don't need a blit pass to resolve
                        m_ActiveCameraDepthAttachment == RenderTargetHandle.GetCameraTarget(cameraData.xr);

                    if (!depthTargetResolved && cameraData.xr.copyDepth)
                    {
                        m_XRCopyDepthPass.Setup(m_ActiveCameraDepthAttachment, RenderTargetHandle.GetCameraTarget(cameraData.xr));
                        EnqueuePass(m_XRCopyDepthPass);
                    }
                }
#endif
            }
            // stay in RT so we resume rendering on stack after post-processing
            // 如果不是stack上最后一个相机，设置后处理pass，且加入队列
            else if (applyPostProcessing)
            {
                postProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, false, m_ActiveCameraDepthAttachment, colorGradingLut, false, false);
                EnqueuePass(postProcessPass);
            }

#if UNITY_EDITOR
            // 如果是非game view，则还要加入一个final depth copy pass，并加入队列
            if (isSceneViewOrPreviewCamera || (isGizmosEnabled && lastCameraInTheStack))
            {
                // Scene view camera should always resolve target (not stacked)
                m_FinalDepthCopyPass.Setup(m_DepthTexture, RenderTargetHandle.CameraTarget);
                m_FinalDepthCopyPass.MssaSamples = 0;
                // Turning off unnecessary NRP in Editor because of MSAA mistmatch between CameraTargetDescriptor vs camera backbuffer
                // NRP layer considers this being a pass with MSAA samples by checking CameraTargetDescriptor taken from RP asset
                // while the camera backbuffer has a single sample
                m_FinalDepthCopyPass.useNativeRenderPass = false; 
                EnqueuePass(m_FinalDepthCopyPass);
            }
#endif
        }

        /// <inheritdoc />
        public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            m_ForwardLights.Setup(context, ref renderingData);

            // Perform per-tile light culling on CPU
            if (this.actualRenderingMode == RenderingMode.Deferred)
                m_DeferredLights.SetupLights(context, ref renderingData);
        }

        /// <inheritdoc />
        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters,
            ref CameraData cameraData)
        {
            // TODO: PerObjectCulling also affect reflection probes. Enabling it for now.
            // if (asset.additionalLightsRenderingMode == LightRenderingMode.Disabled ||
            //     asset.maxAdditionalLightsCount == 0)
            // {
            //     cullingParameters.cullingOptions |= CullingOptions.DisablePerObjectCulling;
            // }

            // We disable shadow casters if both shadow casting modes are turned off
            // or the shadow distance has been turned down to zero

            // 对阴影进行裁剪(shadowcast关闭了或者阴影距离为0的都被裁剪掉)
            bool isShadowCastingDisabled = !UniversalRenderPipeline.asset.supportsMainLightShadows && !UniversalRenderPipeline.asset.supportsAdditionalLightShadows;
            bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
            if (isShadowCastingDisabled || isShadowDistanceZero)
            {
                cullingParameters.cullingOptions &= ~CullingOptions.ShadowCasters;
            }

            if (this.actualRenderingMode == RenderingMode.Deferred)
                cullingParameters.maximumVisibleLights = 0xFFFF;
            else
            {
                // We set the number of maximum visible lights allowed and we add one for the mainlight...
                //
                // Note: However ScriptableRenderContext.Cull() does not differentiate between light types.
                //       If there is no active main light in the scene, ScriptableRenderContext.Cull() might return  ( cullingParameters.maximumVisibleLights )  visible additional lights.
                //       i.e ScriptableRenderContext.Cull() might return  ( UniversalRenderPipeline.maxVisibleAdditionalLights + 1 )  visible additional lights !

                // 限制可见灯光数量
                cullingParameters.maximumVisibleLights = UniversalRenderPipeline.maxVisibleAdditionalLights + 1;
            }

            // 裁剪阴影距离超限的阴影
            cullingParameters.shadowDistance = cameraData.maxShadowDistance;

            // 保守包围球是用于优化的空间数据结构
            cullingParameters.conservativeEnclosingSphere = UniversalRenderPipeline.asset.conservativeEnclosingSphere;

            // 计算包围球使用的迭代次数，次数越多越精确
            cullingParameters.numIterationsEnclosingSphere = UniversalRenderPipeline.asset.numIterationsEnclosingSphere;
        }

        /// <inheritdoc />
        public override void FinishRendering(CommandBuffer cmd)
        {
            m_ColorBufferSystem.Clear(cmd);

            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                m_ActiveCameraColorAttachment = RenderTargetHandle.CameraTarget;
            }

            if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_ActiveCameraDepthAttachment.id);
                m_ActiveCameraDepthAttachment = RenderTargetHandle.CameraTarget;
            }
        }

        void EnqueueDeferred(ref RenderingData renderingData, bool hasDepthPrepass, bool hasNormalPrepass, bool applyMainShadow, bool applyAdditionalShadow)
        {
            m_DeferredLights.Setup(
                ref renderingData,
                applyAdditionalShadow ? m_AdditionalLightsShadowCasterPass : null,
                hasDepthPrepass,
                hasNormalPrepass,
                m_DepthTexture,
                m_DepthInfoTexture,
                m_TileDepthInfoTexture,
                m_ActiveCameraDepthAttachment,
                m_ActiveCameraColorAttachment
            );
            // Need to call Configure for both of these passes to setup input attachments as first frame otherwise will raise errors
            if (useRenderPassEnabled && m_DeferredLights.UseRenderPass)
            {
                m_GBufferPass.Configure(null, renderingData.cameraData.cameraTargetDescriptor);
                m_DeferredPass.Configure(null, renderingData.cameraData.cameraTargetDescriptor);
            }

            EnqueuePass(m_GBufferPass);

            //Must copy depth for deferred shading: TODO wait for API fix to bind depth texture as read-only resource.
            if (!useRenderPassEnabled || !m_DeferredLights.UseRenderPass)
            {
                m_GBufferCopyDepthPass.Setup(m_CameraDepthAttachment, m_DepthTexture);
                EnqueuePass(m_GBufferCopyDepthPass);
            }

            // Note: DeferredRender.Setup is called by UniversalRenderPipeline.RenderSingleCamera (overrides ScriptableRenderer.Setup).
            // At this point, we do not know if m_DeferredLights.m_Tilers[x].m_Tiles actually contain any indices of lights intersecting tiles (If there are no lights intersecting tiles, we could skip several following passes) : this information is computed in DeferredRender.SetupLights, which is called later by UniversalRenderPipeline.RenderSingleCamera (via ScriptableRenderer.Execute).
            // However HasTileLights uses m_HasTileVisLights which is calculated by CheckHasTileLights from all visibleLights. visibleLights is the list of lights that have passed camera culling, so we know they are in front of the camera. So we can assume m_DeferredLights.m_Tilers[x].m_Tiles will not be empty in that case.
            // m_DeferredLights.m_Tilers[x].m_Tiles could be empty if we implemented an algorithm accessing scene depth information on the CPU side, but this (access depth from CPU) will probably not happen.
            if (m_DeferredLights.HasTileLights())
            {
                // Compute for each tile a 32bits bitmask in which a raised bit means "this 1/32th depth slice contains geometry that could intersect with lights".
                // Per-tile bitmasks are obtained by merging together the per-pixel bitmasks computed for each individual pixel of the tile.
                EnqueuePass(m_TileDepthRangePass);

                // On some platform, splitting the bitmasks computation into two passes:
                //   1/ Compute bitmasks for individual or small blocks of pixels
                //   2/ merge those individual bitmasks into per-tile bitmasks
                // provides better performance that doing it in a single above pass.
                if (m_DeferredLights.HasTileDepthRangeExtraPass())
                    EnqueuePass(m_TileDepthRangeExtraPass);
            }

            EnqueuePass(m_DeferredPass);

            EnqueuePass(m_RenderOpaqueForwardOnlyPass);
        }

        private struct RenderPassInputSummary
        {
            internal bool requiresDepthTexture;
            internal bool requiresDepthPrepass;
            internal bool requiresNormalsTexture;
            internal bool requiresColorTexture;
            internal bool requiresColorTextureCreated;
            internal bool requiresMotionVectors;
            internal RenderPassEvent requiresDepthNormalAtEvent;
            internal RenderPassEvent requiresDepthTextureEarliestEvent;
        }

        /// <summary>
        /// 判断render pass是否需要创建depth or normal or color等纹理
        /// </summary>
        /// <param name="renderingData"></param>
        /// <returns></returns>
        private RenderPassInputSummary GetRenderPassInputs(ref RenderingData renderingData)
        {
            RenderPassEvent beforeMainRenderingEvent = m_RenderingMode == RenderingMode.Deferred ? RenderPassEvent.BeforeRenderingGbuffer : RenderPassEvent.BeforeRenderingOpaques;

            RenderPassInputSummary inputSummary = new RenderPassInputSummary();
            inputSummary.requiresDepthNormalAtEvent = RenderPassEvent.BeforeRenderingOpaques;
            inputSummary.requiresDepthTextureEarliestEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            for (int i = 0; i < activeRenderPassQueue.Count; ++i)
            {
                ScriptableRenderPass pass = activeRenderPassQueue[i];
                bool needsDepth = (pass.input & ScriptableRenderPassInput.Depth) != ScriptableRenderPassInput.None;
                bool needsNormals = (pass.input & ScriptableRenderPassInput.Normal) != ScriptableRenderPassInput.None;
                bool needsColor = (pass.input & ScriptableRenderPassInput.Color) != ScriptableRenderPassInput.None;
                bool needsMotion = (pass.input & ScriptableRenderPassInput.Motion) != ScriptableRenderPassInput.None;
                bool eventBeforeMainRendering = pass.renderPassEvent <= beforeMainRenderingEvent;

                // TODO: Need a better way to handle this, probably worth to recheck after render graph
                // DBuffer requires color texture created as it does not handle y flip correctly
                if (pass is DBufferRenderPass dBufferRenderPass)
                {
                    inputSummary.requiresColorTextureCreated = true;
                }

                inputSummary.requiresDepthTexture |= needsDepth;
                inputSummary.requiresDepthPrepass |= needsNormals || needsDepth && eventBeforeMainRendering;
                inputSummary.requiresNormalsTexture |= needsNormals;
                inputSummary.requiresColorTexture |= needsColor;
                inputSummary.requiresMotionVectors |= needsMotion;
                if (needsDepth)
                    inputSummary.requiresDepthTextureEarliestEvent = (RenderPassEvent)Mathf.Min((int)pass.renderPassEvent, (int)inputSummary.requiresDepthTextureEarliestEvent);
                if (needsNormals || needsDepth)
                    inputSummary.requiresDepthNormalAtEvent = (RenderPassEvent)Mathf.Min((int)pass.renderPassEvent, (int)inputSummary.requiresDepthNormalAtEvent);
            }

            return inputSummary;
        }

        bool IsGLESDevice()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
        }

        /// <summary>
        /// 使用渲染目标交换系统，创建camera target的颜色附着和深度附着
        /// </summary>
        /// <param name="context"></param>
        /// <param name="descriptor"></param>
        /// <param name="primedDepth"></param>
        void CreateCameraRenderTarget(ScriptableRenderContext context, ref RenderTextureDescriptor descriptor, bool primedDepth)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(null, Profiling.createCameraRenderTarget))
            {
                // 如果当前激活的颜色附着不是camera target
                if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                {
                    // 是否当前激活的深度附着是camera target，则需要生成depth
                    bool useDepthRenderBuffer = m_ActiveCameraDepthAttachment == RenderTargetHandle.CameraTarget;
                    var colorDescriptor = descriptor;
                    colorDescriptor.useMipMap = false;  // 不使用mipmap
                    colorDescriptor.autoGenerateMips = false;   // 不生成mip
                    colorDescriptor.depthBufferBits = (useDepthRenderBuffer) ? k_DepthStencilBufferBits : 0; //深度纹理位数
                    // 传递rt描述和过滤方式，生成swap buffer
                    m_ColorBufferSystem.SetCameraSettings(cmd, colorDescriptor, FilterMode.Bilinear);

                    // 如果需要rt需要深度
                    if (useDepthRenderBuffer)
                        // 为什么第二个参数是获取bufferA？因为只有bufferA允许带深度,所以只能从bufferA上获取
                        // 将camera target设置为渲染目标管理系统的back buffer
                        ConfigureCameraTarget(m_ColorBufferSystem.GetBackBuffer(cmd).id, m_ColorBufferSystem.GetBufferA().id);
                    else // 如果rt不需要深度，只需要设置camera target的颜色target即可，不用管深度target
                        ConfigureCameraColorTarget(m_ColorBufferSystem.GetBackBuffer(cmd).id);

                    // 将当前激活的颜色附着设置为back buffer
                    m_ActiveCameraColorAttachment = m_ColorBufferSystem.GetBackBuffer(cmd);
                    // 将当前激活的颜色附着设置为全局纹理_CameraColorTexture和_AfterPostProcessTexture
                    cmd.SetGlobalTexture("_CameraColorTexture", m_ActiveCameraColorAttachment.id);
                    //Set _AfterPostProcessTexture, users might still rely on this although it is now always the cameratarget due to swapbuffer
                    cmd.SetGlobalTexture("_AfterPostProcessTexture", m_ActiveCameraColorAttachment.id);
                }

                // 如果当前激活的深度附着不是camera target
                if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
                {
                    var depthDescriptor = descriptor;
                    depthDescriptor.useMipMap = false;
                    depthDescriptor.autoGenerateMips = false;

                    // true:着色器直接访问多重采样纹理；false:着色器访问解析后的单采样纹理
                    depthDescriptor.bindMS = depthDescriptor.msaaSamples > 1 && (SystemInfo.supportsMultisampledTextures != 0);

                    // binding MS surfaces is not supported by the GLES backend, and it won't be fixed after investigating
                    // the high performance impact of potential fixes, which would make it more expensive than depth prepass (fogbugz 1339401 for more info)
                    if (IsGLESDevice())
                        depthDescriptor.bindMS = false;

                    depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                    depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                    // 创建临时rt作为当前激活的深度附着
                    cmd.GetTemporaryRT(m_ActiveCameraDepthAttachment.id, depthDescriptor, FilterMode.Point);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        bool PlatformRequiresExplicitMsaaResolve()
        {
#if UNITY_EDITOR
            // In the editor play-mode we use a Game View Render Texture, with
            // samples count forced to 1 so we always need to do an explicit MSAA resolve.
            return true;
#else
            // On Metal/iOS the MSAA resolve is done implicitly as part of the renderpass, so we do not need an extra intermediate pass for the explicit autoresolve.
            // Note: On Vulkan Standalone, despite SystemInfo.supportsMultisampleAutoResolve being true, the backbuffer has only 1 sample, so we still require
            // the explicit resolve on non-mobile platforms with supportsMultisampleAutoResolve.
            return !(SystemInfo.supportsMultisampleAutoResolve && Application.isMobilePlatform)
                && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal;
#endif
        }

        /// <summary>
        /// Checks if the pipeline needs to create a intermediate render texture.
        /// </summary>
        /// <param name="cameraData">CameraData contains all relevant render target information for the camera.</param>
        /// <seealso cref="CameraData"/>
        /// <returns>Return true if pipeline needs to render to a intermediate render texture.</returns>
        bool RequiresIntermediateColorTexture(ref CameraData cameraData)
        {
            // When rendering a camera stack we always create an intermediate render texture to composite camera results.
            // We create it upon rendering the Base camera.
            if (cameraData.renderType == CameraRenderType.Base && !cameraData.resolveFinalTarget)
                return true;

            // Always force rendering into intermediate color texture if deferred rendering mode is selected.
            // Reason: without intermediate color texture, the target camera texture is y-flipped.
            // However, the target camera texture is bound during gbuffer pass and deferred pass.
            // Gbuffer pass will not be y-flipped because it is MRT (see ScriptableRenderContext implementation),
            // while deferred pass will be y-flipped, which breaks rendering.
            // This incurs an extra blit into at the end of rendering.
            if (this.actualRenderingMode == RenderingMode.Deferred)
                return true;

            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            var cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = cameraTargetDescriptor.msaaSamples;
            bool isScaledRender = cameraData.imageScalingMode != ImageScalingMode.None;
            bool isCompatibleBackbufferTextureDimension = cameraTargetDescriptor.dimension == TextureDimension.Tex2D;
            bool requiresExplicitMsaaResolve = msaaSamples > 1 && PlatformRequiresExplicitMsaaResolve();
            bool isOffscreenRender = cameraData.targetTexture != null && !isSceneViewCamera;
            bool isCapturing = cameraData.captureActions != null;

#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                isScaledRender = false;
                isCompatibleBackbufferTextureDimension = cameraData.xr.renderTargetDesc.dimension == cameraTargetDescriptor.dimension;
            }
#endif
            bool postProcessEnabled = cameraData.postProcessEnabled && m_PostProcessPasses.isCreated;
            bool requiresBlitForOffscreenCamera = postProcessEnabled || cameraData.requiresOpaqueTexture || requiresExplicitMsaaResolve || !cameraData.isDefaultViewport;
            if (isOffscreenRender)
                return requiresBlitForOffscreenCamera;

            return requiresBlitForOffscreenCamera || isSceneViewCamera || isScaledRender || cameraData.isHdrEnabled ||
                !isCompatibleBackbufferTextureDimension || isCapturing || cameraData.requireSrgbConversion;
        }

        // 判断是否可以拷贝深度。(结论：A.开启了MSAA且进行了多重采样(非GLES设备)，支持；B:没开启MSAA，且设备支持纹理拷贝和深度格式，支持。)
        bool CanCopyDepth(ref CameraData cameraData)
        {
            // 是否开启msaa
            bool msaaEnabledForCamera = cameraData.cameraTargetDescriptor.msaaSamples > 1;

            // 平台是否支持纹理拷贝
            bool supportsTextureCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;

            // 平台是否支持深度
            bool supportsDepthTarget = RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Depth);

            // 如果相机不支持msaa且支持深度纹理和纹理拷贝功能，则支持深度拷贝
            bool supportsDepthCopy = !msaaEnabledForCamera && (supportsDepthTarget || supportsTextureCopy);

            // 是否开启msaa并使用多重采样?
            bool msaaDepthResolve = msaaEnabledForCamera && SystemInfo.supportsMultisampledTextures != 0;

            // copying MSAA depth on GLES3 is giving invalid results. Needs investigation (Fogbugz issue 1339401)
            // 如果是Gles2或Gles3设备并且开启了msaa且msaa设置为了多重采样不能拷贝深度。
            if (IsGLESDevice() && msaaDepthResolve)
                return false;

            return supportsDepthCopy || msaaDepthResolve;
        }

        internal override void SwapColorBuffer(CommandBuffer cmd)
        {
            m_ColorBufferSystem.Swap();

            //Check if we are using the depth that is attached to color buffer
            if (m_ActiveCameraDepthAttachment == RenderTargetHandle.CameraTarget)
                ConfigureCameraTarget(m_ColorBufferSystem.GetBackBuffer(cmd).id, m_ColorBufferSystem.GetBufferA().id);
            else ConfigureCameraColorTarget(m_ColorBufferSystem.GetBackBuffer(cmd).id);

            m_ActiveCameraColorAttachment = m_ColorBufferSystem.GetBackBuffer();
            cmd.SetGlobalTexture("_CameraColorTexture", m_ActiveCameraColorAttachment.id);
            //Set _AfterPostProcessTexture, users might still rely on this although it is now always the cameratarget due to swapbuffer
            cmd.SetGlobalTexture("_AfterPostProcessTexture", m_ActiveCameraColorAttachment.id);
        }

        internal override RenderTargetIdentifier GetCameraColorFrontBuffer(CommandBuffer cmd)
        {
            return m_ColorBufferSystem.GetFrontBuffer(cmd).id;
        }

        internal override void EnableSwapBufferMSAA(bool enable)
        {
            m_ColorBufferSystem.EnableMSAA(enable);
        }
    }
}

Shader "Hidden/Universal Render Pipeline/Scaling Setup"
{
    HLSLINCLUDE
        #pragma multi_compile_local_fragment _ _FXAA
        #pragma multi_compile_vertex _ _USE_DRAW_PROCEDURAL
        #pragma multi_compile_local_fragment _ _GAMMA_20  // 颜色空间转换,FSR需要
        // gaussf
        #pragma multi_compile_local_fragment _ _GAUSSF_RGB _GAUSSF_YUV

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

        TEXTURE2D_X(_SourceTex);
        float4 _SourceSize;

        // --------------------Gaussf start--------------------
        half3 RGB2YUV(half3 rgb)
        {
            const half3 rgb2Y = half3(+0.299, +0.587, +0.114);
            const half3 rgb2U = half3(-0.167, -0.331, +0.500);
            const half3 rgb2V = half3(+0.500, -0.418, -0.081);

            half3 yuv;
            yuv.r = dot(rgb2Y, rgb) + 0.000;
            yuv.g = dot(rgb2U, rgb) + 0.500;
            yuv.b = dot(rgb2V, rgb) + 0.500;

            return yuv;
        }
        // --------------------Gaussf end--------------------

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
            float2 positionNDC = uv;
            int2   positionSS = uv * _SourceSize.xy;

            half3 color = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_PointClamp, uv).xyz;

#if _FXAA
            color = ApplyFXAA(color, positionNDC, positionSS, _SourceSize, _SourceTex);
#endif

#if _GAMMA_20 && !UNITY_COLORSPACE_GAMMA
            // EASU expects perceptually encoded color data so either encode to gamma 2.0 here if the input
            // data is linear, or let it pass through unchanged if it's already gamma encoded.
            color = LinearToGamma20(color);
#endif

            // -------------- GAUSSF -----------------------
#if _GAUSSF_RGB
            color = LinearToGamma20(color);
#endif
#if _GAUSSF_YUV
            color = RGB2YUV(color);
#endif
            return half4(color, 1.0);
        }

    ENDHLSL

    ///
    /// Scaling Setup Shader
    ///
    /// This shader is used to perform any operations that need to place before image scaling occurs.
    /// It is not expected to be executed unless image scaling is active.
    ///
    /// Supported Operations:
    ///
    /// FXAA
    /// The FXAA shader does not support mismatched input and output dimensions so it must be run before any image
    /// scaling takes place.
    ///
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "ScalingSetup"

            HLSLPROGRAM
                #pragma vertex FullscreenVert
                #pragma fragment Frag
            ENDHLSL
        }
    }
}

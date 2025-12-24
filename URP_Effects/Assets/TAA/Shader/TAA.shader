Shader "Shaders/Naiwen/TAA.shader"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
    }

    HLSLINCLUDE

        #pragma exclude_renderers gles
        #pragma target 3.5
        #pragma multi_compile _LOW_TAA _MIDDLE_TAA _HIGH_TAA
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        float3 _TAA_Params;  // xy = offset, z = feedback
        TEXTURE2D_X(_MainTex);
        uniform float4 _MainTex_TexelSize;

        TEXTURE2D_X(_TAA_Pretexture);               // 前一帧图像
        TEXTURE2D_X_FLOAT(_CameraDepthTexture);     // 深度图（当前帧的）
        float4x4 _PrevViewProjM_TAA;                // 前一帧的vp矩阵
        float4x4 _I_P_Current_jittered;             // 当前帧投影矩阵的逆矩阵
        float4x4 _I_V_Current_jittered;             // 当前帧视图矩阵的逆矩阵

        struct AttributesTAA
        {
            float4 positionOS   : POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct VaryingsTAA
        {
            half4  positionCS   : SV_POSITION;
            half4  uv           : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        VaryingsTAA VertexTAA(AttributesTAA input)
        {
            VaryingsTAA output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            // 裁剪空间下顶点坐标的范围为：[-w, w]，经过透视除法后为[-1,1]
            float4 projPos = output.positionCS * 0.5;       // [-1/2w, 1/2w]
            projPos.xy = projPos.xy + projPos.w;            // [1/2w, 3/2w]

            output.uv.xy = UnityStereoTransformScreenSpaceTex(input.texcoord);

            output.uv.zw = projPos.xy;

            return output;
        }

        float4 sample_color(Texture2D<float4> tex, float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);
        }

        // 计算抖动后屏幕uv下的上一帧屏幕uv
        float2 historyPostion(float2 un_jitted_uv)
        {
            float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, un_jitted_uv).r;
            // DX平台深度范围近1远0，OPENGL平台深度范围-1，1
#if UNITY_REVERSED_Z
            depth = 1.0 - depth;
#endif
            depth = 2.0 * depth - 1.0;
#if UNITY_UV_STARTS_AT_TOP
            un_jitted_uv.y = 1.0f - un_jitted_uv.y;
#endif
            float3 viewPos = ComputeViewSpacePosition(un_jitted_uv, depth, _I_P_Current_jittered);
            float4 worldPos = float4(mul(unity_CameraToWorld, float4(viewPos, 1.0)).xyz, 1.0);
            
             float4 historyNDC = mul(_PrevViewProjM_TAA, worldPos);
            historyNDC /= historyNDC.w;
            historyNDC.xy = historyNDC.xy * 0.5f + 0.5f;
            return historyNDC.xy;
        }

        // 优化颜色处理，减少鬼影和闪烁现象
        float4 clip_aabb(float3 aabb_min, float3 aabb_max, float4 avg, float4 input_texel)
        {
            // 颜色的中心值
            float3 p_clip = 0.5 * (aabb_max + aabb_min);
            // 颜色的半长范围
            float3 e_clip = 0.5 * (aabb_max - aabb_min) + FLT_EPS;
            // 前一帧颜色相对于中心颜色值的偏移
            float4 v_clip = input_texel - float4(p_clip, avg.w);
            // 归一化到单位包围盒中
            float3 v_unit = v_clip.xyz / e_clip;

            // 输入颜色超出包围盒边界的程度,<1，在包围盒内，无需处理
            float3 a_unit = abs(v_unit);
            float ma_unit = max(a_unit.x, max(a_unit.y, a_unit.z));

            // 超出范围，沿原方向缩放到边界处
            if (ma_unit > 1.0)
                return float4(p_clip, avg.w) + v_clip / ma_unit;
            else
                return input_texel;
        }

        void minmax(in float2 uv, out float4 color_min, out float4 color_max, out float4 color_avg)
        {
            float2 du = float2(_MainTex_TexelSize.x, 0.0);
            float2 dv = float2(0.0, _MainTex_TexelSize.y);
#if defined(_HIGH_TAA)
            // 周围8个点 + center点
            float4 ctl = sample_color(_MainTex, uv - dv - du);
            float4 ctc = sample_color(_MainTex, uv - dv);
            float4 ctr = sample_color(_MainTex, uv - dv + du);
            float4 cml = sample_color(_MainTex, uv - du);
            float4 cmc = sample_color(_MainTex, uv);
            float4 cmr = sample_color(_MainTex, uv + du);
            float4 cbl = sample_color(_MainTex, uv + dv - du);
            float4 cbc = sample_color(_MainTex, uv + dv);
            float4 cbr = sample_color(_MainTex, uv + dv + du);

            // 按每个通道独立计算的，所有颜色的分量最小值
            color_min = min(ctl, min(ctc, min(ctr, min(cml, min(cmc, min(cmr, min(cbl, min(cbc, cbr))))))));
            // 所有颜色的分量最大值
            color_max = max(ctl, max(ctc, max(ctr, max(cml, max(cmc, max(cmr, max(cbl, max(cbc, cbr))))))));
            // 所有颜色的平均值
            color_avg = (ctl + ctc + ctr + cml + cmc + cmr + cbl + cbc + cbr) / 9.0;

#elif defined(_MIDDLE_TAA)
            float2 ss_offset01 =  float2(-_MainTex_TexelSize.x, _MainTex_TexelSize.y);
            float2 ss_offset11 =  float2(_MainTex_TexelSize.x, _MainTex_TexelSize.y);
            float4 c00 = sample_color(_MainTex, uv - ss_offset11);
            float4 c10 = sample_color(_MainTex, uv - ss_offset01);
            float4 c01 = sample_color(_MainTex, uv + ss_offset01);
            float4 c11 = sample_color(_MainTex, uv + ss_offset11);

            color_min = min(c00, min(c10, min(c01, c11)));
            color_max = max(c00, max(c10, max(c01, c11)));
            color_avg = (c00 + c10 + c01 + c11) / 4.0;
#elif defined(_LOW_TAA)
            float2 ss_offset11 = float2(_MainTex_TexelSize.x, _MainTex_TexelSize.y);
            float4 c00 = sample_color(_MainTex, uv - ss_offset11);
            float4 c11 = sample_color(_MainTex, uv + ss_offset11);
            color_min = min(c00, c11);
            color_max = max(c00, c11);
            color_avg = (c00 + c11) / 2.0;
#endif

        }

        float4 Frag(VaryingsTAA input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv_jitted = UnityStereoTransformScreenSpaceTex(input.uv);
            // 偏移后的屏幕uv
            float2 un_jitted_uv = uv_jitted - _TAA_Params.xy;
            float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, un_jitted_uv);

            // 计算周围邻居像素所有的颜色的分量最小值和最大值和平均值
            float4 color_min, color_max, color_avg;
            minmax(un_jitted_uv, color_min, color_max, color_avg);
            float2 previousTC = historyPostion(un_jitted_uv);
            // 采样上一帧的颜色值
             float4 prev_color = SAMPLE_TEXTURE2D_X(_TAA_Pretexture, sampler_LinearClamp, previousTC);
             // 颜色修正，减轻鬼影和闪烁现象
            prev_color = clip_aabb(color_min, color_max, color_avg, prev_color);
            float4 result_color = lerp(color, prev_color, _TAA_Params.z);
            return result_color;
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "Stop NaN"
            HLSLPROGRAM
                #pragma vertex VertexTAA
                #pragma fragment Frag
            ENDHLSL
        }
    }
}
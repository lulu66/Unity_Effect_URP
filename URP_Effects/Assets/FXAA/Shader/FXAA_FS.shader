Shader "Hidden/Anti-Aliasing/FXAA_FS"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline" }

        ZTest Always ZWrite Off Cull back

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

            TEXTURE2D_X(_MainTex);

            float4 _MainTex_TexelSize;

            #define FXAA_SPAN_MAX           (8.0)
            #define FXAA_REDUCE_MUL         (1.0 / 8.0)
            #define FXAA_REDUCE_MIN         (1.0 / 128.0)

            half3 Fetch(half2 coords, half2 offset)
            {
                half2 uv = coords + offset;
                return SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv).xyz;
            }

            half4 Load(half2 coords, int idx, int idy)
            {
                half2 uv = (coords + int2(idx, idy) * 0.5f) * _MainTex_TexelSize.xy;
                return SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);
            }

            half3 ApplyFXAA_FS(half3 color, half2 positionNDC, int2 positionSS, half4 sourceSize, TEXTURE2D_X(inputTexture))
            {
                //half3 color = Load(positionSS, 0, 0).xyz;

                half4 rgbNW = Load(positionSS, -1, -1);
                half4 rgbNE = Load(positionSS, 1, -1);
                half4 rgbSW = Load(positionSS, -1, 1);
                half4 rgbSE = Load(positionSS, 1, 1);

                rgbNW = saturate(rgbNW);
                rgbNE = saturate(rgbNE);
                rgbSW = saturate(rgbSW);
                rgbSE = saturate(rgbSE);
                color = saturate(color);

                half lumaNW = Luminance(rgbNW);
                half lumaNE = Luminance(rgbNE);
                half lumaSW = Luminance(rgbSW);
                half lumaSE = Luminance(rgbSE);
                half lumaM = Luminance(color);

                half2 dir;
                dir.x = (lumaSW + lumaSE) - (lumaNW + lumaNE);
                dir.y = (lumaNW + lumaSW) - (lumaNE + lumaSE);

                half lumaSum = lumaNW + lumaNE + lumaSW + lumaSE;
                half dirReduce = max(lumaSum * (0.25 * FXAA_REDUCE_MUL), FXAA_REDUCE_MIN);
                half rcpDirMin = rcp(min(abs(dir.x), abs(dir.y)) + dirReduce);

                dir = min((FXAA_SPAN_MAX).xx, max((-FXAA_SPAN_MAX).xx, dir * rcpDirMin)) * _MainTex_TexelSize.xy;

                // Blur
                half3 rgb03 = Fetch(positionNDC, dir * (0.0 / 3.0 - 0.5));
                half3 rgb13 = Fetch(positionNDC, dir * (1.0 / 3.0 - 0.5));
                half3 rgb23 = Fetch(positionNDC, dir * (2.0 / 3.0 - 0.5));
                half3 rgb33 = Fetch(positionNDC, dir * (3.0 / 3.0 - 0.5));

                rgb03 = saturate(rgb03);
                rgb13 = saturate(rgb13);
                rgb23 = saturate(rgb23);
                rgb33 = saturate(rgb33);

                half3 rgbA = 0.5 * (rgb13 + rgb23);
                half3 rgbB = rgbA * 0.5 + 0.25 * (rgb03 + rgb33);

                half lumaB = Luminance(rgbB);

                half lumaMin = Min3(lumaM, lumaNW, Min3(lumaNE, lumaSW, lumaSE));
                half lumaMax = Max3(lumaM, lumaNW, Max3(lumaNE, lumaSW, lumaSE));

                color = ((lumaB < lumaMin) || (lumaB > lumaMax)) ? rgbA : rgbB;

                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 uv = input.uv;
                half2 positionNDC = uv;
                int2 positionSS = uv * _MainTex_TexelSize.zw;

                half4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);

                color.rgb = ApplyFXAA_FS(color.rgb, positionNDC, positionSS, _MainTex_TexelSize, _MainTex);

                return color;
            }
        ENDHLSL
        Pass
        {
            Name "FXAA FS"

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}

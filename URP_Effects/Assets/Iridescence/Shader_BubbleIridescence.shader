Shader "EsShaders/BubbleIridescence"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.1, 0.1, 0.3, 0.5)
        _IridescenceIntensity("Iridescence Intensity", Range(0, 5)) = 2.0
        _IridescenceScale("Iridescence Scale", Range(0.1, 10)) = 3.0
        _IridescenceSpeed("Iridescence Speed", Range(0, 2)) = 0.5
        _FresnelPower("Fresnel Power", Range(0, 10)) = 3.0
        _FresnelColor("Iridescence Color", Color) = (1,1,1,1)
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _Distortion("Distortion", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            half3 normalOS : NORMAL;
            half2 texcoord : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            half3 normalWS : TEXCOORD0;
            half3 viewDirWS : TEXCOORD1;
            half2 uv : TEXCOORD2;
            half fresnel : TEXCOORD3;
        };

        CBUFFER_START(UnityPerMaterial)

            half4 _BaseColor;
            half4 _FresnelColor;
            half4 _NoiseTex_ST;
            half _IridescenceIntensity;
            half _IridescenceScale;
            half _IridescenceSpeed;
            half _FresnelPower;
            half _Distortion;

        CBUFFER_END

        TEXTURE2D(_NoiseTex);
        SAMPLER(sampler_NoiseTex);

        ENDHLSL

        Pass
        {
            Name "ForwardLit"

            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha

            ZWrite Off

            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half3 getIridescenceColor(half t)
            {
                // 基于相位生成彩虹色
                t = frac(t);
                half3 color = float3(1,1,1);

                if (t < 0.166) // 红色到黄色
                    color = lerp(half3(1,0,0), half3(1,1,0), t * 6.0);
                else if (t < 0.333) // 黄色到绿色
                    color = lerp(half3(1,1,0), half3(0,1,0), (t - 0.166) * 6.0);
                else if (t < 0.5) // 绿色到青色
                    color = lerp(half3(0,1,0), half3(0,1,1), (t - 0.333) * 6.0);
                else if (t < 0.666) // 青色到蓝色
                    color = lerp(half3(0,1,1), half3(0,0,1), (t - 0.5) * 6.0);
                else if (t < 0.833) // 蓝色到紫色
                    color = lerp(half3(0,0,1), half3(0.5,0,1), (t - 0.666) * 6.0);
                else // 紫色到红色
                    color = lerp(half3(0.5,0,1), half3(1,0,0), (t - 0.833) * 6.0);

                return color;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _NoiseTex);

                // 计算菲涅尔项
                float NdotV = 1.0 - saturate(dot(normalize(output.normalWS), normalize(output.viewDirWS)));
                output.fresnel = pow(NdotV, _FresnelPower);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 采样噪声纹理用于扭曲效果
                float2 noiseUV = input.uv + _Time.y * _IridescenceSpeed * 0.1;
                half2 distortion = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg;
                distortion = (distortion * 2.0 - 1.0) * _Distortion;

                // 基于视角和法线计算镭射相位
                half3 viewDir = normalize(input.viewDirWS);
                half3 normal = normalize(input.normalWS);

                // 使用扭曲后的UV计算相位
                half2 iridescenceUV = input.uv + distortion;
                half phase = dot(normal, viewDir) * _IridescenceScale + _Time.y * _IridescenceSpeed;

                // 添加基于UV的变化
                phase += (iridescenceUV.x + iridescenceUV.y) * 2.0;

                // 生成镭射颜色
                half3 iridescenceColor = getIridescenceColor(phase) * _IridescenceIntensity;

                // 结合基础颜色和镭射效果
                half3 finalColor = _BaseColor.rgb + iridescenceColor;

                // 增强菲涅尔效果
                half fresnel = input.fresnel;
                finalColor += _FresnelColor.rgb * fresnel;

                // 计算透明度 - 边缘更透明
                half alpha = _BaseColor.a * saturate(0.3 + fresnel * 0.7);

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}

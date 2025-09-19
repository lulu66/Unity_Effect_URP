// perlin clouds的原理
// 用噪声扭曲星云
// 模拟星云的丝状和管状
// 添加色彩:渐变映射

Shader "Shader/Nebula"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _MaskTex("Mask Texture", 2D) = "white"{}
        [Header(Layer One)]
        [Space(10)]
        _DistortA("Distort", Range(0,1)) = 0
        _HighLightColorA("High Light Color", Color) = (0,0,0,1)
        _ColorA("Color", Color) = (1,1,1,1)
        _AlphaPowA("Alpha Power", Range(0,1)) = 0.5
        _SpeedA("_Speed",Range(0,2)) = 1
        [Header(Layer Two)]
        [Space(10)]
        _TillingMainTexB("Tilling Main Texture",vector) = (1,1,0,0)
        _TillingNoiseTexB("Tilling Noise Texture",vector) = (1,1,0,0)
        _TillingMaskTexB("Tilling Mask Texture",vector) = (1,1,0,0)
        _DistortB("Distort", Range(0,1)) = 0
        _HighLightColorB("High Light Color B", Color) = (0,0,0,1)
        _ColorB("Color B", Color) = (1,1,1,1)
        _AlphaPowB("Alpha Power", Range(0,1)) = 0.5
        _SpeedB("_Speed",Range(0,2)) = 1

    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalRenderPipeline" "Queue" = "Transparent"}

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Nebula"
            Tags
            {
                 "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                half4 uv : TEXCOORD0;
                half4 uv2 : TEXCOORD1;
                half4 uv3 : TEXCOORD2;
                half4 uv4 : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _NoiseTex_ST;
                half4 _MaskTex_ST;
                half4 _HighLightColorA;
                half4 _ColorA;
                half _DistortA;
                half _AlphaPowA;
                half _SpeedA;
                half4 _TillingMainTexB;
                half4 _TillingNoiseTexB;
                half4 _TillingMaskTexB;
                half4 _HighLightColorB;
                half4 _ColorB;
                half _DistortB;
                half _AlphaPowB;
                half _SpeedB;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = positionInputs.positionCS;
                // 第一层uv
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv, _NoiseTex);
                o.uv2.xy = TRANSFORM_TEX(v.uv, _MaskTex);
                o.uv2.zw = v.uv; // 原始uv
                
                // 第二层uv
                o.uv3.xy = v.uv * _TillingMainTexB.xy + _TillingMainTexB.zw; // _MainTex
                o.uv3.zw = v.uv * _TillingNoiseTexB.xy + _TillingNoiseTexB.zw; // _NoiseTex
                o.uv4.xy = v.uv * _TillingMaskTexB.xy + _TillingMaskTexB.zw; // _MaskTex

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // 第一团星云
                half4 noiseA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,i.uv.zw);
                half2 offsetA = half2(noiseA.r - 0.5, noiseA.g - 0.5) * 2 * _DistortA;
                half4 colA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy + offsetA +  float2(_Time.x * _SpeedA, 0));
                half4 maskA = half4(1, 1, 1, 1) - SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv2.xy + offsetA + _Time.x * _SpeedA);
                half alphaA = colA.a * colA.g * colA.b;
                alphaA *= maskA.r * maskA.g;
                alphaA = pow(alphaA,_AlphaPowA) * _ColorA.a;
                half3 colorA = lerp(_ColorA, _HighLightColorA.rgb,  colA.b);

                // 第二层星云
                half4 noiseB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv3.zw);
                half2 offsetB = half2(noiseB.r - 0.5, noiseB.g - 0.5) * 2 * _DistortB;
                half4 colB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv3.xy + offsetB + float2(_Time.x * _SpeedB, 0));
                half4 maskB = half4(1,1,1,1) - SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv4.xy + offsetB + _Time.x * _SpeedB);
                half alphaB = colB.a * colB.g * colB.b;
                alphaB *= maskB.r * maskB.g;
                alphaB = pow(alphaB, _AlphaPowB) * _ColorB.a;
                half3 colorB = lerp(_ColorB, _HighLightColorB.rgb,  colB.b);

                half finalAlpha = alphaB * alphaB + alphaA * (1 - alphaB); //saturate(alphaA + alphaB);
                half3 finalColor = colorB * alphaB + colorA * (1 - alphaB);//lerp(0,saturate(colorA+colorB), finalAlpha);
                return half4(finalColor,finalAlpha);
            }
            ENDHLSL
        }
    }
}

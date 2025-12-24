Shader "EsShaders/BubbleMatcap"
{
    Properties
    {
        _MatCapTex("MatCap Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Reflectivity("Reflectivity", Range(0, 2)) = 1.0
        _NormalMap("NormalMap", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            half3 normalOS : NORMAL;
            half2 texcoord : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalVS : TEXCOORD1;
        };

        CBUFFER_START(UnityPerMaterial)

            float4 _BaseColor;
            float _Reflectivity;
            float4 _MatCapTex_ST;

        CBUFFER_END

        TEXTURE2D(_MatCapTex);
        SAMPLER(sampler_MatCapTex);

        ENDHLSL

        Pass
        {
            Name "Matcap"

            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha

            ZWrite Off

            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.uv = input.texcoord;

                // 转换法线到视图空间
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalVS = TransformWorldToViewDir(normalWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 计算MatCap UV
                float3 normalVS = normalize(input.normalVS);
                float2 matcapUV = normalVS.xy * 0.5 + 0.5;

                // 采样MatCap纹理
                float4 matcapColor = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MatCapTex, matcapUV);

                // 组合颜色
                float3 finalColor = _BaseColor.rgb  *  matcapColor.rgb * _Reflectivity;

                return half4(finalColor, _BaseColor.a);
            }

            ENDHLSL
        }
    }
}

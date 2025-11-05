Shader "Shaders/Fluid"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _DetailTex("Detail Texture", 2D) = "white"{}
        _NoiseTex("Noise Texture", 2D) = "white"{}
        _FluidSpeed("Fluid Speed", Range(0,2)) = 1
        _DisturbStrength("Disturb Strength", Range(0,1)) = 0.1
        _DisturbSpeed("Disturb Speed", Range(0,1)) = 0.1
        _ReflectionFactor("Reflection Factor", Range(0,1)) = 0.5
    }
        SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" "Queue" = "Transparent"}

        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Fluid"

            Tags{"LightMode" = "UniversalForward" "Queue" = "Transparent"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _NoiseTex_ST;
                half _FluidSpeed;
                half _DisturbStrength;
                half _DisturbSpeed;
                half _ReflectionFactor;
            CBUFFER_END

            TEXTURE2D(_MainTex); // state buffer
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_Velocity); // velocity buffer
            SAMPLER(sampler_Velocity);

            TEXTURE2D(_DetailTex);
            SAMPLER(sampler_DetailTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_PlanarReflectionTexture);
            SAMPLER(sampler_PlanarReflectionTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                half3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 worldNormal : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };


            v2f vert (appdata v)
            {
                v2f o;


                o.uv.xy = v.uv;

                o.uv.zw = v.uv * _NoiseTex_ST.xy + _NoiseTex_ST.zw;

                float3 positionWS = TransformObjectToWorld(v.vertex.xyz);

                float3 normalWS = TransformObjectToWorldNormal(v.normal);

                o.worldPos = positionWS;
                o.worldNormal = normalWS;
                o.vertex = TransformWorldToHClip(positionWS);
                o.screenPos = ComputeScreenPos(o.vertex);

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {

                //half4 state = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, i.uv.xy);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                half3 reflectionColor = SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, screenUV).rgb;

                half reflectFactor = (reflectionColor.r + reflectionColor.g + reflectionColor.b) > 0 ? 1 : 0;

                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv.zw - _Time.y * _DisturbSpeed).r;

                half disturb = (noise * 2 - 1) * _DisturbStrength;

                half2 disturbUV = i.uv.xy - disturb.xx;

                half4 velocity = SAMPLE_TEXTURE2D(_Velocity, sampler_Velocity, i.uv.xy);

                half2 remapVel = velocity.rg * _FluidSpeed;

                half factor = smoothstep(0, 0.1, length(remapVel));

                half2 fluidUV = lerp(disturbUV, (i.uv.xy - remapVel), factor);

                half4 detailTexture = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, fluidUV);

                half3 finalColor =lerp(detailTexture.rgb, reflectionColor, reflectFactor * _ReflectionFactor);

                half alpha = lerp(1, 0.9, factor);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}

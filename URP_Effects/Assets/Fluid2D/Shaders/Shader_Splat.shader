Shader "Shader/Splat"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Velocity("Texture", 2D) = "black" {}
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _BlendOp("__op", Float) = 1.0
    }
        SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent"}

        ZWrite Off
        Cull Off
        ZTest Always

        HLSLINCLUDE

            #pragma vertex vert
            #pragma fragment frag

            #include "FluidHelper.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _DensityNoiseParams;
                half4 _SplatTransform; // 外物的投影位置和投影大小
                half4 _VelocityNoiseParams; // 速度噪声
                half4 _SplatWeight; // 密度权重
                half4 _LinearVelicoty; // 相对线速度
                float _AngularVelocity; // 相对角速度
                half4 _MainTex_TexelSize;
                half4 _Noise_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;

                //float3 worldPos = mul(UNITY_MATRIX_M, v.vertex).xyz;

                o.vertex = TransformObjectToHClip(v.vertex);//mul(UNITY_MATRIX_VP, float4(worldPos, 1));

                // 裁剪空间坐标转换到物体投影区域
                float2 clipPos = o.vertex.xy;
                half4 tile = half4(0,0,1,1);
#if UNITY_UV_STARTS_AT_TOP
                tile.y = 1 - tile.y;
                tile.w *= -1;
                clipPos.y *= -1;
#endif
                clipPos = tile.xy + ((clipPos * _SplatTransform.zw + 1) * 0.5 + _SplatTransform.xy) * tile.zw;

                o.vertex.xy = clipPos * 2 - 1;

                o.uv.xy = v.uv;
                return o;
            }

        ENDHLSL


        Pass
        {
            Name "Splat Density"

            Tags{"LightMode" = "UniversalForward" "Queue" = "Transparent"}

            //BlendOp [_BlendOp] // Reverse Subtract : 目标颜色 * 目标透明度 - 源颜色 * 源透明度
            //Blend [_SrcBlend] [_DstBlend]
            BlendOp RevSub
            Blend One One

            HLSLPROGRAM

                half4 frag(v2f i) : SV_Target
                {
                    // 使用一张贴图来作为密度纹理
                    half4 density = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, i.uv) *_SplatWeight;

                //// 对密度做扰动
                //i.uv += _Time.x * _DensityNoiseParams.y;

                //i.uv *= _DensityNoiseParams.z;

                //half densityNoise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise,i.uv);

                //// 使用噪声对密度做扰动（此处可能可以忽略掉噪声）
                //density *= lerp(1, densityNoise, _DensityNoiseParams.x);

                return density;
            }
            ENDHLSL
        }

            Pass
            {
                Name "Splat Velocity"

                Tags{"LightMode" = "UniversalForward" "Queue" = "Transparent"}

                Blend SrcAlpha OneMinusSrcAlpha
                ColorMask RG

                HLSLPROGRAM

                // 速度纹理
                TEXTURE2D(_Velocity);
                SAMPLER(sampler_Velocity);


                float4 frag(v2f i) : SV_Target
                {
                    //return half4(1,0,0,0.01);
                    // 外物本身的速度
                    float4 splatVel = _LinearVelicoty * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;

                    // 通过纹理获取速度
                    half4 textureVel = SAMPLE_TEXTURE2D(_Velocity, sampler_Velocity, i.uv);

                    float2 offset = _MainTex_TexelSize.xy;
                    half pL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-offset.x, 0)).a;
                    half pR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(offset.x, 0)).a;
                    half pB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, -offset.y)).a;
                    half pT = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, offset.y)).a;

                    // 计算压力梯度(速度影响的一部分)
                    half2 pressureVel = -float2(pR - pL, pT - pB) * 0.5;

                    // 深度产生的速度(应该是通过噪声图制造一个假的深度,如果不给贴图那就不会产生深度相关的速度)
                    splatVel.xy += pressureVel * abs(splatVel.z);

                    // 将新速度结合进velocity buffer的速度
                    splatVel.xy += (textureVel.xy * 2 - 1) * textureVel.b;

                    // 物体旋转导致的速度
                    half2 r = (i.uv - 0.5f) * _SplatTransform.zw;   // 外物的大小
                    splatVel.xy += float2(-_AngularVelocity * r.y, _AngularVelocity * r.x);

                    // 噪声图对速度的影响:
                    //offset = _Noise_TexelSize.xy;
                    //i.uv += _Time.x * _VelocityNoiseParams.y;
                    //i.uv *= _VelocityNoiseParams.z;
                    //float nL = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv + float2(-offset.x, 0)).a;
                    //float nR = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv + float2(offset.x, 0)).a;
                    //float nB = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv + float2(0, -offset.y)).a;
                    //float nT = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv + float2(0, offset.y)).a;
                    //float2 curl = float2(nT - nB, nL - nR) * 0.5;
                    //splatVel.xy += curl * _VelocityNoiseParams.x;
                    return float4(splatVel.xy, 0, splatVel.w);
                }
                ENDHLSL
            }
    }
}

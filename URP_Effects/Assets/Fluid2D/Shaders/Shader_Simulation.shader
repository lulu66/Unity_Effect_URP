Shader "Shader/Simulation"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent"}

		Cull Off
		ZWrite Off
		ZTest Always

		HLSLINCLUDE

			#pragma vertex vert
			#pragma fragment frag

			#include "FluidHelper.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			TEXTURE2D(_Velocity);
			SAMPLER(sampler_Velocity);

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

				float3 worldPos = mul(UNITY_MATRIX_M, v.vertex).xyz;

				o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1));

				o.uv.xy = v.uv;
				return o;
			}

		ENDHLSL

		Pass
		{
			// 0
			Name "Advect State"
			Tags{"LightMode" = "UniversalForward" "Queue" = "Transparent"}
			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)
				int4 _WrapMode;
				half _DeltaTime;
				half4 _MainTex_TexelSize;
				half4 _Velocity_TexelSize;
			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				// 双线性插值的流体速度
				half2 vel = Tex2dBilinear(TEXTURE2D_ARGS(_Velocity, sampler_Velocity), i.uv, _Velocity_TexelSize, _WrapMode);

				half2 sourceUV = i.uv - vel * _DeltaTime;

				// 双线性插值获取流体状态: stateA
				half4 advection = Tex2dBilinear(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), sourceUV, _MainTex_TexelSize, _WrapMode);

				// 使用密度对速度进行缩放，本来此处要考虑附着力的，此处将附着力都假设为0
				vel *= saturate(1 + advection.a);

				return Tex2dBilinear(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv - vel * _DeltaTime, _MainTex_TexelSize, _WrapMode);
			}
			ENDHLSL
		}

		Pass
		{
				// 1
			Name "Advect Velocity"
			Tags{"LightMode" = "UniversalForward" "Queue" = "Transparent"}
			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)

				half4 _MainTex_TexelSize;
				half4 _WrapMode;
				half _DeltaTime;

			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				half4 warpMode = half4(_WrapMode.xy,1,1);

				half2 vel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rg;

				return Tex2dBilinear(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv - vel * _DeltaTime, _MainTex_TexelSize, warpMode);
			}

			ENDHLSL
		}

		Pass
		{
				// 2
			Name "Dissipation"
			Tags{"LightMode" = "UniversalForward" "Queue" = "Transparent"}
			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)
				half4 _Dissipation;
				half _DeltaTime;
				half4 _EdgeFalloff;
			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				half falloff = (1 - SquareFalloff(i.uv,_EdgeFalloff.x)) * _EdgeFalloff.y;
				return saturate(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) - (_Dissipation + falloff) * _DeltaTime);
			}

				ENDHLSL
			}

		Pass
		{
				// 3
			Name "Curl"

			Tags{"LightMode" = "UniversalForward" "Queue" = "Transparent"}

			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)

				half4 _MainTex_TexelSize;
				half4 _WrapMode;
				half _DeltaTime;

			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				half2 offset = _MainTex_TexelSize.xy;
				half4 wrapmask = half4(_WrapMode.xy,0,0);
				// 获取原始速度
				half4 vel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				// 计算旋度
				half vL = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(-offset.x, 0), _MainTex_TexelSize,wrapmask).g;
				half vR = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(offset.x, 0), _MainTex_TexelSize, wrapmask).g;
				half vB = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, -offset.y), _MainTex_TexelSize, wrapmask).r;
				half vT = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, offset.y),_MainTex_TexelSize, wrapmask).r;

				vel.b = (vL - vR + vT - vB) * 0.5;

				return vel;
			}

			ENDHLSL
		}

		Pass
		{
				// 4
			Name "Density Gradient"

			ColorMask RG

			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)

				half4 _MainTex_TexelSize;
				half4 _WrapMode;

			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				half3 offset = half3(_MainTex_TexelSize.xy,0);

				// 计算梯度
				half hb0 = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv - offset.xz, _MainTex_TexelSize, _WrapMode).a;
				half hb1 = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv - offset.zy, _MainTex_TexelSize, _WrapMode).a;
				half h0 = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + offset.xz, _MainTex_TexelSize, _WrapMode).a;
				half h1 = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + offset.zy, _MainTex_TexelSize, _WrapMode).a;

				half3 p0 = half3(offset.xz, (h0 - hb0));
				half3 p1 = half3(offset.zy, (h1 - hb1));

				half3 nrm = normalize(cross(p0, p1));

				return half4(nrm.xy, 0, 0);

			}

			ENDHLSL
		}

		Pass
		{
				// 5
			Name "Divergence of velocity"

			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)

				half4 _MainTex_TexelSize;
				half4 _WrapMode;

			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				half2 offset = _MainTex_TexelSize.xy;
				half4 vel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				half vL = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(-offset.x, 0), _MainTex_TexelSize, _WrapMode).r;
				half vR = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(offset.x, 0), _MainTex_TexelSize, _WrapMode).r;
				half vB = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, -offset.y), _MainTex_TexelSize, _WrapMode).g;
				half vT = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, offset.y), _MainTex_TexelSize, _WrapMode).g;

				vel.b = -(vR - vL + vT - vB) * 0.5;

				return vel;
			}

			ENDHLSL
		}

		Pass
		{
				// 6
			Name "JacobiIteration"

			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)

				half4 _MainTex_TexelSize;
				half4 _WrapMode;
			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				half2 offset = _MainTex_TexelSize.xy;
				half4 warpmask = half4(_WrapMode.xy,0,0);
				half4 vel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				half xL = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(-offset.x, 0), _MainTex_TexelSize, warpmask).a;
				half xR = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(offset.x, 0), _MainTex_TexelSize, warpmask).a;
				half xB = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, -offset.y), _MainTex_TexelSize, warpmask).a;
				half xT = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, offset.y), _MainTex_TexelSize, warpmask).a;

				// 考虑散度
				vel.a = (xL + xR + xB + xT + vel.b) * 0.25;

				return vel;
			}

			ENDHLSL
		}

		Pass
		{
				// 7
			Name "SubtractPressureGradient"
			Blend One One
			HLSLPROGRAM

			CBUFFER_START(UnityPerMaterial)

				half4 _MainTex_TexelSize;
				half4 _WrapMode;
				half _Pressure;
			CBUFFER_END

			half4 frag(v2f i) : SV_Target
			{
				half2 offset = _MainTex_TexelSize.xy;
				half pL = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(-offset.x, 0), _MainTex_TexelSize, _WrapMode).a;
				half pR = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(offset.x, 0), _MainTex_TexelSize, _WrapMode).a;
				half pB = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, -offset.y), _MainTex_TexelSize, _WrapMode).a;
				half pT = Tex2dNearest(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.uv + half2(0, offset.y), _MainTex_TexelSize, _WrapMode).a;

				return half4(-half2(pR - pL, pT - pB) * 0.5 * _Pressure, 0, 0);
			}

			ENDHLSL
		}
	}
}

Shader "EsShaders/Fx/Partile_SkillRange_AlphaBlend" 
{
	Properties 
	{
		[Enum(Zero, 0,One,1,DstColor,2,SrcColor, 3,OneMinusDstColor, 4,SrcAlpha, 5,OneMinusSrcColor,6,DstAlpha, 7,OneMinusDstAlpha,8,SrcAlphaSaturate, 9,OneMinusSrcAlpha,10)]
		_SrcBlend("SrcBlend", Float) = 5
		[Enum(Zero, 0,One,1,DstColor,2,SrcColor, 3,OneMinusDstColor, 4,SrcAlpha, 5,OneMinusSrcColor,6,DstAlpha, 7,OneMinusDstAlpha,8,SrcAlphaSaturate, 9,OneMinusSrcAlpha,10)]
		_DstBlend("DstBlend", Float) = 10
		[Header(Shining)]
		[Space(5)]
		[Toggle(_SHINNG)]_Shinng("闪烁开关", int) = 0
		_Speed("闪烁速度", Range(0, 1)) = 0
		_AlphaMax("闪烁半透控制", Range(0, 1)) = 0 

		[Space(5)]
		[Header(Main Setting)]
		[Space(5)]
		[HDR]_Color ("Color", Color) = (0.5,0.5,0.5,0.5)
		_MainTex ("主贴图", 2D) = "white" {}	

		[Space(5)]
		[Header(Progress Setting)]
		[Space(5)]
		_ProgressTex ("进度条贴图", 2D) = "white" {}
		[HDR]_FlowColor("Progress Color", Color) = (1, 1, 1, 1)
		[HDR]_HightLightColor("HightLight Color", Color) = (1, 1, 1, 1)
		[Toggle(_SCALE_DIFFUSION)]_scaleDiffusion("开启缩放扩散(Circle Type Only)", int) = 0
		_FlowDirX("Flow Dir X(Rectangle Type Only)", float) = 1
		_FlowDirY("Flow Dir Y(Rectangle Type Only)", float) = 0

		[Space(5)]
		[Header(InnerOutline Setting)]
		[Space(5)]
		[Toggle(_INNER_OUTLINE)]_InnerOutline("开启内环描边", int) = 0
		_InnerOutlineWidth("内描边宽度",Range(0.001,0.1)) = 0.01
		_OuterOutlineWidth("外描边宽度",Range(0.001,0.1)) = 0.01
		[HDR]_OutlineColor("内描边颜色", color) = (0,0,0,1)

		[Space(5)]
		[Header(Dissolve Setting)]
		[Space(5)]
		[Toggle(_DISSOLVETOGGLE_ON)]_DissolveOn("DissolveOn", Int) = 0
		_DissolveTex("DissolveTex",2D) = "white"{}
		_dissolveUVMove("X(speed),Y(speed),Z(centerScale),W(colorMul)",Vector) = (0,0,1,1)
		_depc("X(Dissolve),Y(edgeWidth),Z(colorPower),W(colorMul)",Vector) = (0.5,0.5,1,0)
		[Enum(Properties,0, CustomData,1)]_dissolveMode("溶解控制方式", int) = 0
		_DissolveValue("溶解", Range(0, 1)) = 0
		[Toggle]_InvertDissolveDir("InvertDissolveDir",int) = 0
		[Enum(X,0,Y,1)] _DissolveDir("DissolveDir",int) = 0
		_DissolveDirToggle("定向溶解控制",Range(0,1)) = 0

		[Space(5)]
		[Header(Edge Setting)]
		[Space(5)]
		[Toggle(ENABLE_EDGE)]_Enable_edge("Enable edge(Circle Type Only)", float) = 0
		[HDR]_EdgeColor("EdgeColor", Color) = (1,1,1,1)


		[Space(5)]
		[Header(Mask Setting)]
		[Space(5)]
		[Toggle(_MASKTOGGLE_ON)]_MaskToggleOn("MaskOn", int) = 0
		[Enum(_MASKTYPE_COLOR, 0, _MASKTYPE_DISTORT, 1, _MASKTYPE_WPO, 2)] _MaskMode("MaskMode", int) = 0
		[Enum(Repeat,0,Clamp,1)] _MKWarpMode("WarpMode", int) = 0
		_MaskTex ("MaskTex(g)",2D) = "white"{}
		_MaskTexUV("MaskTexUV", float) = 0
		_maskUVMove("X(speed),Y(speed),Z(centerScale)",Vector) = (0,0,1,1)

		[HideInInspector]_FlowType("Flow Type", int) = 1
		[HideInInspector]_FlowProgress("Flow Progress", Range(0, 1)) = 1.0
		[HideInInspector]_StartFlowProgress("Start Flow Progress", Range(0, 1)) = 0.0
		[HideInInspector]_MaxInnerAngle("Max inner angle", Range(0, 360)) = 360
		[HideInInspector]OuterRingOffset("Outer ring offset", float) = 4.8
		[HideInInspector]OuterRingWidth("Outer ring width", Range(0, 3)) = 0.2
		
	}
	SubShader
	{
		Tags
		{
			"RenderType"="Transparent" "RenderPipeline" = "UniversalRenderPipeline" "Queue"="Transparent" 
		}
		Blend[_SrcBlend][_DstBlend]
		Cull Off ZWrite Off
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_local _ _SHINNG
			#pragma shader_feature_local _ ENABLE_EDGE
			#pragma shader_feature_local _ _DISSOLVETOGGLE_ON
			#pragma shader_feature_local _MASKTOGGLE_ON
			#pragma shader_feature_local _SCALE_DIFFUSION
			#pragma shader_feature_local _MASKTYPE_COLOR _MASKTYPE_DISTORT _MASKTYPE_WPO
			#pragma shader_feature_local _INNER_OUTLINE
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
			TEXTURE2D(_ProgressTex);SAMPLER(sampler_ProgressTex);
			TEXTURE2D(_DissolveTex);SAMPLER(sampler_DissolveTex);
			TEXTURE2D(_MaskTex);SAMPLER(sampler_MaskTex);
			
			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				half4 _ProgressTex_ST;
				half4 _EdgePattern_ST;

				half4 _Color;
				half4 _HightLightColor;
				half4 _FlowColor;
				half4 _OutlineColor;

				half _MaxInnerAngle;
				half OuterRingOffset;
				half OuterRingWidth;

				half _FlowDirX;
				half _FlowDirY;
				int _FlowType;
				half _FlowProgress;
				float _StartFlowProgress;

				half4 _dissolveUVMove, _depc, _EdgeColor;
				half _DissolveValue;
				half _dissolveMode, _InvertDissolveDir, _DissolveDir, _DissolveDirToggle;
				half4 _DissolveTex_ST;

				half _MaskTexUV;
				half4 _maskUVMove, _MaskTex_ST;
				half _MKWarpMode;
				half _MaskMode;

				half _Speed;
				half _AlphaMax;

				half _InnerOutlineWidth;
				half _OuterOutlineWidth;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS		: POSITION;
				half4 color				: COLOR;
				float2 texcoord			: TEXCOORD0;
				float2 texcoord1    	: TEXCOORD1;
				float2 edge_texcoord	: TEXCOORD2;
				float4 Custom1      	: TEXCOORD3;
				float4 Custom2      	: TEXCOORD4;
			};

			struct Varyings
			{
				float4 positionHCS		: SV_POSITION;
				half4 color				: COLOR;
				float4 maintexcoord		: TEXCOORD0;
				float4 secondtexcoord	: TEXCOORD1;
				float4 uv1          	: TEXCOORD2;
				float4 uv2				: TEXCOORD3;
				float4 Custom1      	: TEXCOORD4;
			};

			float SmoothstepSimple(float min, float max, float x)
			{
				return saturate((x - min) / (max - min));
			}

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = mul(GetWorldToHClipMatrix(), mul(GetObjectToWorldMatrix(), float4(IN.positionOS.xyz, 1.0)));
				OUT.color = IN.color;
				OUT.maintexcoord.xy = (IN.texcoord - 0.5) * _MainTex_ST.xy + 0.5 + _MainTex_ST.zw;
				// 用于计算像素的朝向，长度为环形半径的长度
				OUT.maintexcoord.zw = IN.positionOS.xz;
				OUT.secondtexcoord.xy = IN.edge_texcoord * _EdgePattern_ST.xy * 0.1 + _EdgePattern_ST.zw * 0.1;
				if (_FlowType == 0)
				{
					// 进度条是从内圈扩散到外圈
					float flowProgress = lerp(_StartFlowProgress, 1, _FlowProgress); 

					// 进度用uv缩放贴图来控制，uv根据flowProgress缩放，flowProgress = 0时贴图缩小到0，flowProgress = 1时贴图放大到1
					OUT.secondtexcoord.zw = (IN.texcoord - 0.5) * _ProgressTex_ST.xy / (flowProgress + 1e-5f) + 0.5 +_ProgressTex_ST.zw;
				}
				else if (_FlowType == 1)
				{
					OUT.secondtexcoord.zw = IN.texcoord * _ProgressTex_ST.xy - half2(_FlowDirX, _FlowDirY) * (-1 +
					_FlowProgress) + _ProgressTex_ST.zw;
				}

				#if _DISSOLVETOGGLE_ON
					OUT.uv2.xy = ((TRANSFORM_TEX(IN.texcoord, _DissolveTex) - 0.5) * _dissolveUVMove.z + 0.5) + frac(_dissolveUVMove.xy * _Time.y);
					OUT.uv2.zw = IN.texcoord1;
				#endif

				#if _MASKTOGGLE_ON
					float2 maskTexUV = lerp(0,IN.Custom2.xy,_MaskTexUV);
					OUT.uv1.zw = ((TRANSFORM_TEX(IN.texcoord, _MaskTex) - 0.5) * _maskUVMove.z + 0.5) + frac(_maskUVMove.xy * _Time.y) + maskTexUV;
				#endif

				OUT.Custom1 = IN.Custom1;

				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				half4 progressTexture;
				half2 progreeUV = IN.secondtexcoord.zw;
				//得到当前像素的朝向：由于环形顶点是按照角度和半径生成的，此处外弧边的顶点2D坐标可以作为方向
				half2 objectCoords = IN.maintexcoord.zw;
				half2 objectdir = normalize(objectCoords);
				half2 mainUV = IN.maintexcoord.xy;
				
				//计算进度条贴图覆盖范围
				progressTexture = _ProgressTex.Sample(sampler_ProgressTex, progreeUV);

				#ifndef ENABLE_EDGE
					int isEdge = 0;
				#else
					// 两条侧边的顶点色是标记为白色的，所以通过step操作判断是不是侧边
					int isEdge = step(0.999, IN.color.r);

					// 当预警圈为环形，调整开启边缘后的弧边环状空隙和侧边空隙
					if(_FlowType == 0)
					{
						//当前像素与正Y轴的夹角
						half radian = dot(half2(0, 1), objectdir);
						//当前像素到圆心的距离，范围0~1
						float radius = length(objectCoords);
						//如果弧边间隙环的偏移大于了圆半径，并且间隙环的外圈半径比圆半径小，并且不是侧边，就不显示像素
						if(radius < OuterRingOffset&&radius > OuterRingOffset-OuterRingWidth&&!isEdge)
						return 0;
						//如果当前像素与Y轴夹角大于预警圈正常夹角范围，并且不是边界，并且弧边间隙环的偏移大于了圆半径，不显示像素
						if(radian < cos(_MaxInnerAngle*0.5)&&!isEdge&&radius < OuterRingOffset)
						return 0;
					}
				#endif

				half4 maskCol = 1;
				half4 maskDis = 1;
				half4 maskUV = 1;
				#if _MASKTOGGLE_ON
					//强制修改warpMode
					IN.uv1.zw = lerp(IN.uv1.zw, saturate(IN.uv1.zw), _MKWarpMode);
					float4 maskValue = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv1.zw);
					//根据Mask贴图将要起作用的方式输出2个变量
					#if _MASKTYPE_COLOR
						maskCol = maskValue;
						maskCol = min(maskCol.r,maskCol.a);
					#endif
					#if _MASKTYPE_DISSLOVE
						maskDis = maskValue;
						maskDis = min(maskDis.r,maskDis.a);
					#endif  
					#if _MASKTYPE_DISTORT
						maskUV = maskValue;
					#endif
				#endif

				float edgeSoftness = 1;
				#if _DISSOLVETOGGLE_ON
					half4 DissolveValue = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, IN.uv2);
					//溶解控制模式
					float DissolveMode = lerp(_DissolveValue, IN.Custom1.z,_dissolveMode);
					//溶解方向反向
					IN.uv2.z = lerp(IN.uv2.z,1-IN.uv2.z,_InvertDissolveDir);
					IN.uv2.w = lerp(IN.uv2.w,1-IN.uv2.w,_InvertDissolveDir);
					//溶解方向
					float4 DissolveDir = lerp((DissolveValue * IN.uv2.z + 1),(DissolveValue * IN.uv2.w + 1),_DissolveDir);
					//定向溶解
					DissolveValue = lerp((DissolveValue + 1),DissolveDir,_DissolveDirToggle);
					//方向控制
					
					//面板参数0-1重映射到0-0.5
					float depcY = (_depc.y + 1) * 0.5;
					//软硬控制
					edgeSoftness = SmoothstepSimple(1 - depcY, depcY, saturate(DissolveValue - (DissolveMode - _depc.w) * 2));
					float edgeSoftnessRGB = SmoothstepSimple(1 - depcY, depcY, saturate(DissolveValue - DissolveMode * 2));
					//Clip
					#if _CLIP_ON
						//硬边
						float hardEdge = round(edgeSoftness);
						clip(hardEdge - 0.01);
					#endif
				#endif

				// 提取进度范围，隐藏非进度范围，作用于透明度
				int isFlow = step(0.05, progressTexture.a);

				half radiusLen = abs(length(IN.maintexcoord.xy - 0.5)) * 2;

				// 主贴图采样，但是得从内圈起始位置开始，从中心点到内圈其实范围内，都不显示像素
				half4 col = _MainTex.Sample(sampler_MainTex, mainUV) * step(_StartFlowProgress, radiusLen);
				col *= _Color;

				half outline = 0;
				// 添加一个内环的描边
				#if defined(_INNER_OUTLINE)
				half innerOutline = 0;
				half outerOutline = 0;

					// 环的内描边
					if (_StartFlowProgress < 0.01)
					{
						innerOutline = 0;
					}
					else
					{
						innerOutline = step(_StartFlowProgress, radiusLen);
					}
					innerOutline *= saturate(1 - step(_StartFlowProgress + length(objectdir * _InnerOutlineWidth), radiusLen));

					// 环的外描边
					outline = innerOutline;
				#endif

				half flowMask = col.a;
#if defined(_SCALE_DIFFUSION)
				col.a = saturate(col.a + progressTexture.a * _FlowColor.a);
#else
				//col.a = max(col.a, _FlowColor.a * flowMask * progressTexture.a + col.a * (1 - isFlow));

				// 整个预警圈的透明度控制 = 进度范围的透明度 + 非进度范围的透明度
				// col.a * (1 - isFlow) : 非进度范围显示主贴图本身的透明度
				// col.a * _FlowColor.a * progressTexture.a : 进度范围内的透明度由flowColor和进度贴图透明度共同控制
				// 预警圈透明度至少是主贴图的透明度
				col.a = max(col.a, col.a * _FlowColor.a * progressTexture.a + col.a * (1 - isFlow));

#endif


				#if _SHINNG
					col.a *= max(_AlphaMax, sin(_Time.y * (20 * _Speed)));
					col.a = saturate(col.a);
				#endif

				#if _MASKTOGGLE_ON
					#if _MASKTYPE_COLOR
						col.a *= maskCol.r;
					#endif  
				#endif

#if defined(_SCALE_DIFFUSION)
				col.rgb = lerp(col.rgb, _FlowColor.rgb * progressTexture.rgb * progressTexture.a,progressTexture.a * _FlowColor.a);

#else
				////进度条接近1时，颜色由高亮颜色和进度贴图的颜色共同决定
				//if (abs(_FlowProgress - 1) < 1e-3)
				//	col.rgb = _HightLightColor.rgb * _HightLightColor.a * progressTexture.rgb * progressTexture.a;
				//// 进度条在0-1之间时，颜色在进度范围内由flowColor和进度贴图颜色共同决定，进度范围外为主贴图颜色
				//else
					col.rgb = lerp(col.rgb, _FlowColor.rgb * progressTexture.rgb * progressTexture.a,progressTexture.a * _FlowColor.a);

					#if defined(_INNER_OUTLINE)

					col = lerp(col, _OutlineColor, outline);

					#endif
#endif
				#if _DISSOLVETOGGLE_ON
					col.rgb = lerp(_EdgeColor.rgb,col.rgb,edgeSoftnessRGB);
					col.a = lerp(0, col.a, edgeSoftnessRGB);
				#endif

				return col;
			}
			ENDHLSL
		}
	}
}

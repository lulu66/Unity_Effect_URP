Shader "EsShaders/SkillRangeVisual2"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (1,1,1,1)
        _MainTex("Main Texture", 2D) = "white" {}
        [HDR] _FanSideColor("Fan Side Color", Color) = (1,1,1,1)
        _FanSideTex("Fan Side Texture", 2D) = "white"{}
        _FanSideOffset("Fan Side Offset",Range(-0.1,0.1)) = 0
        _ProgressTex("Progress Texture", 2D) = "white" {}
        [HDR]_ProgressColor("Progress Color", Color) = (1,1,1,1)
        // 结束效果
        _CloseNoiseTex("Close Noise Tex", 2D) = "white"{}
        [HDR]_CloseColor("Close Color",Color) = (1,1,1,1)
        _MaskTex("Mask Tex", 2D) = "white"{}
        [HideInInspector]_FlowProgress("Flow Progress", Range(0, 1)) = 1.0
        [HideInInspector]_CloseEffectProgress("Close Effect Progress", Float) = 0
        [HideInInspector]_MaxInnerAngle("Max inner angle", Range(0, 360)) = 360
        [HideInInspector]_SrcBlend("SrcBlend", Float) = 5
        [HideInInspector]_DstBlend("DstBlend", Float) = 10


    }
    SubShader
    {
        Tags
        {
            "LightMode" = "UniversalForward" "RenderType" = "Transparent" "Queue" = "Transparent" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Cull Off
            ZWrite Off
            Blend[_SrcBlend][_DstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 预警圈类型
            #pragma shader_feature_local _TYPE_RECTANGLE /*_TYPE_CIRCLE*/ _TYPE_FAN 
            // 扇形预警圈是否开启边界
            #pragma shader_feature_local _ENABLE_FAN_EDGE
            // 矩形预警圈的进度方向
            #pragma shader_feature_local _RECT_HORIZONTAL_SCALE _RECT_VERTICAL_SCALE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS		: POSITION;
                half2 texcoord			: TEXCOORD0;
                half2 texcoord1         : TEXCOORD1;
                half4 color				: COLOR;
            };

            struct v2f
            {
                float4 positionHCS		: SV_POSITION;
                half4 maintexcoord		: TEXCOORD0;        // xy:主纹理uv;zw:侧边界纹理uv
                half4 edgetexcoord      : TEXCOORD2;        // xy:fan edge uv; zw : flow progress uv
                half4 closeEffecttexcoord : TEXCOORD3;
                half4 color				: TEXCOORD1;


            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_ProgressTex); SAMPLER(sampler_ProgressTex);
            TEXTURE2D(_FanSideTex); SAMPLER(sampler_FanSideTex);
            TEXTURE2D(_CloseNoiseTex); SAMPLER(sampler_CloseNoiseTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _ProgressTex_ST;
                half4 _CloseNoiseTex_ST;
                half4 _ProgressColor;
                half4 _Color;
                half4 _FanSideColor;
                half4 _CloseColor;
                half _FanSideOffset;
                half _FlowProgress;
                half _CloseEffectProgress;

            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;

                half4 worldPos = mul(GetObjectToWorldMatrix(), float4(v.positionOS.xyz, 1.0));

                o.positionHCS = mul(GetWorldToHClipMatrix(), worldPos);

                // 技能框的纹理
                o.maintexcoord.xy = (v.texcoord - 0.5) * _MainTex_ST.xy + 0.5 + _MainTex_ST.zw;

                o.maintexcoord.zw = v.texcoord.xy;

                #if defined(_TYPE_FAN)
                    // 技能框的本地坐标

                    o.color = v.color;
                    o.edgetexcoord.xy = half2(v.texcoord1.x + _FanSideOffset, v.texcoord1.y);
                    // 进度
                    half flowProgress = max(_FlowProgress, 1e-5f);
                    o.edgetexcoord.zw = (v.texcoord - half2(0.5,0.5)) /flowProgress + half2(0.5,0.5);
                #endif

                #if defined(_TYPE_RECTANGLE)
                    o.color = v.color;
                    // 进度
                    half flowProgress = max(_FlowProgress, 1e-5f);
                    
                    #if defined(_RECT_HORIZONTAL_SCALE)
                        // 从中间往两边缩放
                        o.edgetexcoord.zw = half2((v.texcoord.x - 0.5) / flowProgress + 0.5, v.texcoord.y);
                    #endif
                    #if defined(_RECT_VERTICAL_SCALE)
                        // 从上往下渐进
                        o.edgetexcoord.zw = half2(v.texcoord.x, v.texcoord.y / flowProgress);
                    #endif
                #endif

                // 结束时的定向溶解效果
                #if defined(_TYPE_FAN)
                        bool isEdge = v.color.r > 0.5;
                       o.closeEffecttexcoord.xy = lerp((v.texcoord.xy - 0.5) * _CloseNoiseTex_ST.xy + 0.5 + _CloseNoiseTex_ST.zw, v.texcoord1.xy * _CloseNoiseTex_ST.xy + _CloseNoiseTex_ST.zw, isEdge);
                #elif defined(_TYPE_RECTANGLE)
                       o.closeEffecttexcoord.xy = (v.texcoord.xy - 0.5) * _CloseNoiseTex_ST.xy + 0.5 + _CloseNoiseTex_ST.zw;
                #endif

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 finalCol = half4(0,0,0,1);

                // 主纹理坐标
                half2 mainUV = i.maintexcoord.xy;
                // 进度uv
                half2 progressUV = i.edgetexcoord.zw;
                half4 mainColor = _MainTex.Sample(sampler_MainTex, mainUV) * _Color;

                // 第一层内部颜色
                half4 firstLayerInnerColor = mainColor;
                // 第二层内部颜色
                half4 progressLayerInnerColor = _ProgressTex.Sample(sampler_ProgressTex, progressUV) * _ProgressColor;

                half finalAlpha = 0;
                // 扇形相关的效果
                #if defined(_TYPE_FAN)
                    
                    // 当前像素的朝向
                    half2 objectdir = normalize(i.maintexcoord.zw);

                    #if defined(_ENABLE_FAN_EDGE)
                        bool isFanEdge = i.color.r > 0.5 ? true : false;


                        half2 fanEdgeUV = i.edgetexcoord.xy;

                        // 分为内部的融合和边界的融合

                        // 第一层边界
                        half4 firstLayerEdgeColor = 0;
                        half4 fanEdgeTex = _FanSideTex.Sample(sampler_FanSideTex, fanEdgeUV);
                        firstLayerEdgeColor = fanEdgeTex * _FanSideColor;

                        // 第二层边界
                        half4 progressLayerEdgeColor = 0;
                        progressLayerEdgeColor = fanEdgeTex * _ProgressColor;

                        // 边界的uv转换为0-1
                        half fanEdgeUvY = (fanEdgeUV.y - 0.5) * 2;
                        half edgeOverride = fanEdgeUvY < _FlowProgress ? 1 : 0;

                        // 进度覆盖效果  
                        half4 innerColor = 0;
                        innerColor.rgb = firstLayerInnerColor.rgb * (1 - progressLayerInnerColor.a) + progressLayerInnerColor.rgb * progressLayerInnerColor.a;
                        innerColor.a = saturate(firstLayerInnerColor.a + progressLayerInnerColor.a) * saturate(1 - i.color.r);

                        half4 edgeColor = edgeOverride? saturate(progressLayerEdgeColor+ firstLayerEdgeColor) : firstLayerEdgeColor;

                        finalCol = lerp(innerColor , edgeColor, i.color.r);
                    #else
                        // 没有边界
                        half4 innerColor = 0;
                        innerColor.rgb = firstLayerInnerColor.rgb * (1 - progressLayerInnerColor.a) + progressLayerInnerColor.rgb * progressLayerInnerColor.a;
                        innerColor.a = saturate(firstLayerInnerColor.a + progressLayerInnerColor.a) * saturate(1 - i.color.r);
                        finalCol = innerColor;
                    #endif
                #endif

                // 矩形相关效果
                #if defined(_TYPE_RECTANGLE)

                        half4 firstLayerColor = mainColor;
                        half4 progressLayerColor = _ProgressTex.Sample(sampler_ProgressTex, progressUV) * _ProgressColor;

                        finalCol.rgb = firstLayerColor * (1 - progressLayerColor.a) + progressLayerColor.rgb * progressLayerColor.a;
                        finalCol.a = saturate(firstLayerColor.a + progressLayerColor.a);
                #endif

                // 结束时的定向溶解效果
                if (_FlowProgress >= 1)
                 {
                    half noiseDisturb = _CloseNoiseTex.Sample(sampler_CloseNoiseTex, i.closeEffecttexcoord.xy).g;
                    half noise = _CloseNoiseTex.Sample(sampler_CloseNoiseTex, i.closeEffecttexcoord.xy + +_Time.xx + noiseDisturb.rr * 0.2).r;
                    half noiseMask = _MaskTex.Sample(sampler_MaskTex, i.maintexcoord.zw).r;
                    #if defined(_TYPE_FAN) && defined(_ENABLE_FAN_EDGE)
                    noiseMask = saturate(edgeColor.a + noiseMask);
                    #endif
                    noiseMask = noiseMask>0?1:0;
                    half transparency = step(noise.r - _CloseEffectProgress*0.8, 0.0) * noiseMask;
                    half4 closeEffColor = _CloseColor * transparency;
                    finalCol = lerp(finalCol, closeEffColor, transparency);
                 }
                return finalCol;
            }
            ENDHLSL
        }
    }
    CustomEditor"EsSkillRangeVisual2ShaderGUI"
}

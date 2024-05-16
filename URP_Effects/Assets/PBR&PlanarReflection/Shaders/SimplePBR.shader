Shader "SimplePBR"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseMap ("Albedo Texture", 2D) = "white" {}
        _Color("Color",Color) = (1,1,1,1)

        [Header(Normal Distorb)]
        _NormalMap1("Normal Map 1", 2D) = "bump"{}
        _NormalMap2("Normal Map 2", 2D) = "bump"{}
        _DistorbSpeed("Distorb Speed", Range(0,1)) = 0

        [Header(Pbr Settings)]
        _PbrMap("Pbr Texture[R:-;G:Smoothness; B:-;A:Occlusion]", 2D) = "white"{}
        _Smoothness("Smoothness", Range(0,1)) = 1
        _Metallic("Metallic",Range(0,1)) = 1
        _Occlusion("Occlusion",Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalRenderPipeline" "Queue" = "Geometry"}

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                 "LightMode" = "UniversalForward"
                 "RenderType" = "Opaque"
            }

            //Blend SrcAlpha OneMinusSrcAlpha
            //ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #include "SimplePBR_ForwardLighting.hlsl"
            ENDHLSL

        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

    }
}

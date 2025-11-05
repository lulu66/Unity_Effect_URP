Shader "Decal"
{
    Properties
    {
        [NoScaleOffset]Base_Map("Base Map", 2D) = "white" {}
        [Normal][NoScaleOffset]Normal_Map("Normal Map", 2D) = "bump" {}
        Normal_Blend("Normal Blend", Float) = 0.5
        [HideInInspector]_DrawOrder("Draw Order", Range(-50, 50)) = 0
        [HideInInspector][Enum(Depth Bias, 0, View Bias, 1)]_DecalMeshBiasType("DecalMesh BiasType", Float) = 0
        [HideInInspector]_DecalMeshDepthBias("DecalMesh DepthBias", Float) = 0
        [HideInInspector]_DecalMeshViewBias("DecalMesh ViewBias", Float) = 0
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            // RenderType: <None>
            "PreviewType"="Plane"
            // Queue: <None>
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"=""
        }
        Pass
        { 
            Name "DBufferProjector"
            Tags 
            { 
                "LightMode" = "DBufferProjector"
            }
        
            // Render State
            Cull Front
        Blend 0 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
        Blend 1 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
        Blend 2 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
        ZTest Greater
        ZWrite Off
        ColorMask RGBA
        ColorMask RGBA 1
        ColorMask 0 2
        
            // Debug
            // <None>
        
            // --------------------------------------------------
            // Pass
        
            HLSLPROGRAM
        
            // Pragmas
            #pragma target 3.5
        #pragma vertex Vert
        #pragma fragment Frag
        #pragma multi_compile_instancing
        #pragma editor_sync_compilation
        
            // Keywords
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            // GraphKeywords: <None>
        
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        
            // Defines
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define VARYINGS_NEED_TEXCOORD0
            
        
        
            #define SHADERPASS SHADERPASS_DBUFFER_PROJECTOR
        #define _MATERIAL_AFFECTS_ALBEDO 1
        #define _MATERIAL_AFFECTS_NORMAL 1
        #define _MATERIAL_AFFECTS_NORMAL_BLEND 1
        
            // HybridV1InjectedBuiltinProperties: <None>
        
            // -- Properties used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif
        
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        
            // --------------------------------------------------
            // Structs and Packing
        
            struct Attributes
        {
             float3 positionOS : POSITION;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
            PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
            // --------------------------------------------------
            // Graph
        
            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
        float4 Base_Map_TexelSize;
        float4 Normal_Map_TexelSize;
        float Normal_Blend;
        float _DrawOrder;
        float _DecalMeshBiasType;
        float _DecalMeshDepthBias;
        float _DecalMeshViewBias;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Base_Map);
        SAMPLER(samplerBase_Map);
        TEXTURE2D(Normal_Map);
        SAMPLER(samplerNormal_Map);
        
            // Graph Functions
            // GraphFunctions: <None>
        
            // Graph Vertex
            // GraphVertex: <None>
            
            // Graph Pixel
            struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
            float3 NormalTS;
            float NormalAlpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0 = UnityBuildTexture2DStructNoScale(Base_Map);
            float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.r;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.g;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.b;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.a;
            UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0 = UnityBuildTexture2DStructNoScale(Normal_Map);
            float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.r;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.g;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.b;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.a;
            float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0 = Normal_Blend;
            surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.xyz);
            surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7;
            surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.xyz);
            surface.NormalAlpha = _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0;
            return surface;
        }
        
            // --------------------------------------------------
            // Build Graph Inputs
        
            
        //     $features.graphPixel:   $include("SharedCode.template.hlsl")
        //                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
        // Packages/com.unity.shadergraph/Editor/Generation/Templates
        
            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */
        
        
        
            output.TangentSpaceNormal =                         float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 =                                        input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
            return output;
        }
        
            // --------------------------------------------------
            // Build Surface Data
        
            uint2 ComputeFadeMaskSeed(uint2 positionSS)
            {
                uint2 fadeMaskSeed;
        
                // Can't use the view direction, it is the same across the entire screen.
                fadeMaskSeed = positionSS;
        
                return fadeMaskSeed;
            }
        
            void GetSurfaceData(Varyings input, uint2 positionSS, float angleFadeFactor, out DecalSurfaceData surfaceData)
            {
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                    half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
                    float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
                    float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                    input.texCoord0.xy = input.texCoord0.xy * scale + offset;
                    half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
                    half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
                    half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
                    half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
                #else
                    #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                        LODDitheringTransition(ComputeFadeMaskSeed(positionSS), unity_LODFade.x);
                    #endif
        
                    half fadeFactor = half(1.0);
                #endif
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.occlusion = half(1.0);
                surfaceData.smoothness = half(0);
        
                #ifdef _MATERIAL_AFFECTS_NORMAL
                    surfaceData.normalWS.w = half(1.0);
                #else
                    surfaceData.normalWS.w = half(0.0);
                #endif
        
        
                // copy across graph values, if defined
                surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
                surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);
        
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
                    #else
                        surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
                    #endif
                #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        float sgn = input.tangentWS.w;      // should be either +1 or -1
                        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        
                        // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                        surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
                    #else
                        surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
                    #endif
                #endif
        
                surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;
        
                // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
            }
        
            // --------------------------------------------------
            // Main
        
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl"
        
            ENDHLSL
        }
        Pass
        { 
            Name "DecalScreenSpaceProjector"
            Tags 
            { 
                "LightMode" = "DecalScreenSpaceProjector"
            }
        
            Cull Front // 剔除正面
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Greater // 只渲染现有几何体后面的像素
            ZWrite Off // 贴花不影响深度缓冲区        
        
            HLSLPROGRAM
        
            #pragma target 2.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma editor_sync_compilation
        
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTERED_RENDERING
            // 支持不同法线混合质量级别
            #pragma multi_compile _DECAL_NORMAL_BLEND_LOW _DECAL_NORMAL_BLEND_MEDIUM _DECAL_NORMAL_BLEND_HIGH
        
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_VIEWDIRECTION_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
            #define VARYINGS_NEED_SH
            #define VARYINGS_NEED_STATIC_LIGHTMAP_UV
            #define VARYINGS_NEED_DYNAMIC_LIGHTMAP_UV
            
        
        
            #define SHADERPASS SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR
            #define _MATERIAL_AFFECTS_ALBEDO 1
            #define _MATERIAL_AFFECTS_NORMAL 1
            #define _MATERIAL_AFFECTS_NORMAL_BLEND 1
                
            // -- Properties used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif
        
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"
        
            // --------------------------------------------------
            // Structs and Packing
        
            struct Attributes
            {
                 float3 positionOS : POSITION;
                 float3 normalOS : NORMAL;
                 float4 uv0 : TEXCOORD0;
                #if UNITY_ANY_INSTANCING_ENABLED
                 uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                 float4 positionCS : SV_POSITION;
                 float3 normalWS;           // 世界空间法线
                 float4 texCoord0;          // 纹理坐标
                 float3 viewDirectionWS;    // 世界空间视线方向
                #if defined(LIGHTMAP_ON)
                 float2 staticLightmapUV;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                 float2 dynamicLightmapUV;
                #endif
                #if !defined(LIGHTMAP_ON)
                 float3 sh;
                #endif
                 float4 fogFactorAndVertexLight;
                #if UNITY_ANY_INSTANCING_ENABLED
                 uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };
            struct SurfaceDescriptionInputs
            {
                 float3 TangentSpaceNormal;
                 float4 uv0;
            };
            struct VertexDescriptionInputs
            {
            };
            struct PackedVaryings
            {
                 float4 positionCS : SV_POSITION;
                #if defined(LIGHTMAP_ON)
                 float2 staticLightmapUV : INTERP0;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                 float2 dynamicLightmapUV : INTERP1;
                #endif
                #if !defined(LIGHTMAP_ON)
                 float3 sh : INTERP2;
                #endif
                 float4 texCoord0 : INTERP3;
                 float4 fogFactorAndVertexLight : INTERP4;
                 float3 normalWS : INTERP5;
                 float3 viewDirectionWS : INTERP6;
                #if UNITY_ANY_INSTANCING_ENABLED
                 uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
                #endif
            };
        
            PackedVaryings PackVaryings (Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                #if defined(LIGHTMAP_ON)
                output.staticLightmapUV = input.staticLightmapUV;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = input.dynamicLightmapUV;
                #endif
                #if !defined(LIGHTMAP_ON)
                output.sh = input.sh;
                #endif
                output.texCoord0.xyzw = input.texCoord0;
                output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
                output.normalWS.xyz = input.normalWS;
                output.viewDirectionWS.xyz = input.viewDirectionWS;
                #if UNITY_ANY_INSTANCING_ENABLED
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }
        
            Varyings UnpackVaryings (PackedVaryings input)
            {
                Varyings output;
                output.positionCS = input.positionCS;
                #if defined(LIGHTMAP_ON)
                output.staticLightmapUV = input.staticLightmapUV;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = input.dynamicLightmapUV;
                #endif
                #if !defined(LIGHTMAP_ON)
                output.sh = input.sh;
                #endif
                output.texCoord0 = input.texCoord0.xyzw;
                output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
                output.normalWS = input.normalWS.xyz;
                output.viewDirectionWS = input.viewDirectionWS.xyz;
                #if UNITY_ANY_INSTANCING_ENABLED
                output.instanceID = input.instanceID;
                #endif
                #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
                output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
                #endif
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                output.cullFace = input.cullFace;
                #endif
                return output;
            }
        
        
            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
            float4 Base_Map_TexelSize;
            float4 Normal_Map_TexelSize;
            float Normal_Blend;
            float _DrawOrder;
            float _DecalMeshBiasType;
            float _DecalMeshDepthBias;
            float _DecalMeshViewBias;
            CBUFFER_END
        
            SAMPLER(SamplerState_Linear_Repeat);
            TEXTURE2D(Base_Map);
            SAMPLER(samplerBase_Map);
            TEXTURE2D(Normal_Map);
            SAMPLER(samplerNormal_Map);       
            
            struct SurfaceDescription
            {
                float3 BaseColor;       // 纹理颜色
                float Alpha;            // 透明度
                float3 NormalTS;        // 切线空间法线
                float NormalAlpha;      // 法线混合强度
            };
        
            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;
                UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0 = UnityBuildTexture2DStructNoScale(Base_Map);
                float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.GetTransformedUV(IN.uv0.xy));
                float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.r;
                float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.g;
                float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.b;
                float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.a;
                UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0 = UnityBuildTexture2DStructNoScale(Normal_Map);
                float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.GetTransformedUV(IN.uv0.xy));
                float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.r;
                float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.g;
                float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.b;
                float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.a;
                float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0 = Normal_Blend;
                // 基础纹理颜色
                surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.xyz);
                // 纹理alpha
                surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7;
                // 法线贴图原始值
                surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.xyz);
                // 法线混合强度
                surface.NormalAlpha = _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0;
                return surface;
            }
        
        
            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);                           
                output.TangentSpaceNormal =  float3(0.0f, 0.0f, 1.0f);               
                output.uv0 = input.texCoord0;
                #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                
                    output.FaceSign = IS_FRONT_VFACE(input.cullFace, true, false);
                #else
                #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
                #endif
                #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
            }
        
        
            uint2 ComputeFadeMaskSeed(uint2 positionSS)
            {
                uint2 fadeMaskSeed;
        
                // Can't use the view direction, it is the same across the entire screen.
                fadeMaskSeed = positionSS;
        
                return fadeMaskSeed;
            }
        
            void GetSurfaceData(Varyings input, uint2 positionSS, float angleFadeFactor, out DecalSurfaceData surfaceData)
            {
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                // decal
                half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);

                // normalToWorld[0][3] : 存放fadeFactor; 
                    half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;

                    // normalToWorld[3][0], normalToWorld[3][1]:uv的缩放；
                    float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);

                    // normalToWorld[3][2], normalToWorld[3][3]：uv的偏移
                    float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);

                    input.texCoord0.xy = input.texCoord0.xy * scale + offset;
                    half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
                    half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
                    half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
                    half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
                    input.normalWS.xyz = normalWS;
                #else
                    #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                        LODDitheringTransition(ComputeFadeMaskSeed(positionSS), unity_LODFade.x);
                    #endif
        
                    half fadeFactor = half(1.0);
                #endif
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.occlusion = half(1.0);
                surfaceData.smoothness = half(0);
        
                #ifdef _MATERIAL_AFFECTS_NORMAL
                    surfaceData.normalWS.w = half(1.0);
                #else
                    surfaceData.normalWS.w = half(0.0);
                #endif
        
        
                // copy across graph values, if defined
                surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
                surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);
        
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
                    #else
                        surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
                    #endif
                #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        float sgn = input.tangentWS.w;      // should be either +1 or -1
                        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        
                        // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                        surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
                    #else
                        surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
                    #endif
                #endif
        
                surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;
        
                // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
            }
        
            // --------------------------------------------------
            // Main
        
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl"
        
            ENDHLSL
        }
        Pass
        { 
            Name "DecalGBufferProjector"
            Tags 
            { 
                "LightMode" = "DecalGBufferProjector"
            }
        
            // Render State
            Cull Front
        Blend 0 SrcAlpha OneMinusSrcAlpha
        Blend 1 SrcAlpha OneMinusSrcAlpha
        Blend 2 SrcAlpha OneMinusSrcAlpha
        Blend 3 SrcAlpha OneMinusSrcAlpha
        ZTest Greater
        ZWrite Off
        ColorMask RGB
        ColorMask 0 1
        ColorMask RGB 2
        ColorMask RGB 3
        
            // Debug
            // <None>
        
            // --------------------------------------------------
            // Pass
        
            HLSLPROGRAM
        
            // Pragmas
            #pragma target 3.5
        #pragma vertex Vert
        #pragma fragment Frag
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma editor_sync_compilation
        
            // Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile _DECAL_NORMAL_BLEND_LOW _DECAL_NORMAL_BLEND_MEDIUM _DECAL_NORMAL_BLEND_HIGH
        #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            // GraphKeywords: <None>
        
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        
            // Defines
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_VIEWDIRECTION_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_SH
            #define VARYINGS_NEED_STATIC_LIGHTMAP_UV
            #define VARYINGS_NEED_DYNAMIC_LIGHTMAP_UV
            
        
        
            #define SHADERPASS SHADERPASS_DECAL_GBUFFER_PROJECTOR
        #define _MATERIAL_AFFECTS_ALBEDO 1
        #define _MATERIAL_AFFECTS_NORMAL 1
        #define _MATERIAL_AFFECTS_NORMAL_BLEND 1
        
            // HybridV1InjectedBuiltinProperties: <None>
        
            // -- Properties used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif
        
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"
        
            // --------------------------------------------------
            // Structs and Packing
        
            struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV : INTERP0;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV : INTERP1;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP2;
            #endif
             float4 texCoord0 : INTERP3;
             float3 normalWS : INTERP4;
             float3 viewDirectionWS : INTERP5;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
            PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.texCoord0.xyzw = input.texCoord0;
            output.normalWS.xyz = input.normalWS;
            output.viewDirectionWS.xyz = input.viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.texCoord0 = input.texCoord0.xyzw;
            output.normalWS = input.normalWS.xyz;
            output.viewDirectionWS = input.viewDirectionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
            // --------------------------------------------------
            // Graph
        
            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
        float4 Base_Map_TexelSize;
        float4 Normal_Map_TexelSize;
        float Normal_Blend;
        float _DrawOrder;
        float _DecalMeshBiasType;
        float _DecalMeshDepthBias;
        float _DecalMeshViewBias;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Base_Map);
        SAMPLER(samplerBase_Map);
        TEXTURE2D(Normal_Map);
        SAMPLER(samplerNormal_Map);
        
            // Graph Functions
            // GraphFunctions: <None>
        
            // Graph Vertex
            // GraphVertex: <None>
            
            // Graph Pixel
            struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
            float3 NormalTS;
            float NormalAlpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0 = UnityBuildTexture2DStructNoScale(Base_Map);
            float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.r;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.g;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.b;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.a;
            UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0 = UnityBuildTexture2DStructNoScale(Normal_Map);
            float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.r;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.g;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.b;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.a;
            float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0 = Normal_Blend;
            surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.xyz);
            surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7;
            surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.xyz);
            surface.NormalAlpha = _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0;
            return surface;
        }
        
            // --------------------------------------------------
            // Build Graph Inputs
        
            
        //     $features.graphPixel:   $include("SharedCode.template.hlsl")
        //                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
        // Packages/com.unity.shadergraph/Editor/Generation/Templates
        
            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */
        
        
        
            output.TangentSpaceNormal =                         float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 =                                        input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
            return output;
        }
        
            // --------------------------------------------------
            // Build Surface Data
        
            uint2 ComputeFadeMaskSeed(uint2 positionSS)
            {
                uint2 fadeMaskSeed;
        
                // Can't use the view direction, it is the same across the entire screen.
                fadeMaskSeed = positionSS;
        
                return fadeMaskSeed;
            }
        
            void GetSurfaceData(Varyings input, uint2 positionSS, float angleFadeFactor, out DecalSurfaceData surfaceData)
            {
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                    half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
                    float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
                    float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                    input.texCoord0.xy = input.texCoord0.xy * scale + offset;
                    half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
                    half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
                    half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
                    half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
                    input.normalWS.xyz = normalWS;
                #else
                    #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                        LODDitheringTransition(ComputeFadeMaskSeed(positionSS), unity_LODFade.x);
                    #endif
        
                    half fadeFactor = half(1.0);
                #endif
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.occlusion = half(1.0);
                surfaceData.smoothness = half(0);
        
                #ifdef _MATERIAL_AFFECTS_NORMAL
                    surfaceData.normalWS.w = half(1.0);
                #else
                    surfaceData.normalWS.w = half(0.0);
                #endif
        
        
                // copy across graph values, if defined
                surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
                surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);
        
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
                    #else
                        surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
                    #endif
                #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        float sgn = input.tangentWS.w;      // should be either +1 or -1
                        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        
                        // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                        surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
                    #else
                        surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
                    #endif
                #endif
        
                surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;
        
                // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
            }
        
            // --------------------------------------------------
            // Main
        
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl"
        
            ENDHLSL
        }
        Pass
        { 
            Name "DBufferMesh"
            Tags 
            { 
                "LightMode" = "DBufferMesh"
            }
        
            // Render State
            Blend 0 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
        Blend 1 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
        Blend 2 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
        ZTest LEqual
        ZWrite Off
        ColorMask RGBA
        ColorMask RGBA 1
        ColorMask 0 2
        
            // Debug
            // <None>
        
            // --------------------------------------------------
            // Pass
        
            HLSLPROGRAM
        
            // Pragmas
            #pragma target 3.5
        #pragma vertex Vert
        #pragma fragment Frag
        #pragma multi_compile_instancing
        #pragma editor_sync_compilation
        
            // Keywords
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            // GraphKeywords: <None>
        
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        
            // Defines
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_TEXCOORD2
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_TANGENT_WS
            #define VARYINGS_NEED_TEXCOORD0
            
        
        
            #define SHADERPASS SHADERPASS_DBUFFER_MESH
        #define _MATERIAL_AFFECTS_ALBEDO 1
        #define _MATERIAL_AFFECTS_NORMAL 1
        #define _MATERIAL_AFFECTS_NORMAL_BLEND 1
        
            // HybridV1InjectedBuiltinProperties: <None>
        
            // -- Properties used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif
        
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        
            // --------------------------------------------------
            // Structs and Packing
        
            struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 tangentWS : INTERP0;
             float4 texCoord0 : INTERP1;
             float3 positionWS : INTERP2;
             float3 normalWS : INTERP3;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
            PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
            // --------------------------------------------------
            // Graph
        
            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
        float4 Base_Map_TexelSize;
        float4 Normal_Map_TexelSize;
        float Normal_Blend;
        float _DrawOrder;
        float _DecalMeshBiasType;
        float _DecalMeshDepthBias;
        float _DecalMeshViewBias;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Base_Map);
        SAMPLER(samplerBase_Map);
        TEXTURE2D(Normal_Map);
        SAMPLER(samplerNormal_Map);
        
            // Graph Functions
            // GraphFunctions: <None>
        
            // Graph Vertex
            // GraphVertex: <None>
            
            // Graph Pixel
            struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
            float3 NormalTS;
            float NormalAlpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0 = UnityBuildTexture2DStructNoScale(Base_Map);
            float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.r;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.g;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.b;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.a;
            UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0 = UnityBuildTexture2DStructNoScale(Normal_Map);
            float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.r;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.g;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.b;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.a;
            float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0 = Normal_Blend;
            surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.xyz);
            surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7;
            surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.xyz);
            surface.NormalAlpha = _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0;
            return surface;
        }
        
            // --------------------------------------------------
            // Build Graph Inputs
        
            
        //     $features.graphPixel:   $include("SharedCode.template.hlsl")
        //                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
        // Packages/com.unity.shadergraph/Editor/Generation/Templates
        
            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */
        
        
        
            output.TangentSpaceNormal =                         float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 =                                        input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
            return output;
        }
        
            // --------------------------------------------------
            // Build Surface Data
        
            uint2 ComputeFadeMaskSeed(uint2 positionSS)
            {
                uint2 fadeMaskSeed;
        
                // Can't use the view direction, it is the same across the entire screen.
                fadeMaskSeed = positionSS;
        
                return fadeMaskSeed;
            }
        
            void GetSurfaceData(Varyings input, uint2 positionSS, float angleFadeFactor, out DecalSurfaceData surfaceData)
            {
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                    half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
                    float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
                    float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                    input.texCoord0.xy = input.texCoord0.xy * scale + offset;
                    half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
                    half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
                    half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
                    half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
                    input.normalWS.xyz = normalWS;
                    input.tangentWS.xyzw = half4(tangentWS, sign);
                #else
                    #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                        LODDitheringTransition(ComputeFadeMaskSeed(positionSS), unity_LODFade.x);
                    #endif
        
                    half fadeFactor = half(1.0);
                #endif
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.occlusion = half(1.0);
                surfaceData.smoothness = half(0);
        
                #ifdef _MATERIAL_AFFECTS_NORMAL
                    surfaceData.normalWS.w = half(1.0);
                #else
                    surfaceData.normalWS.w = half(0.0);
                #endif
        
        
                // copy across graph values, if defined
                surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
                surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);
        
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
                    #else
                        surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
                    #endif
                #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        float sgn = input.tangentWS.w;      // should be either +1 or -1
                        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        
                        // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                        surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
                    #else
                        surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
                    #endif
                #endif
        
                surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;
        
                // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
            }
        
            // --------------------------------------------------
            // Main
        
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl"
        
            ENDHLSL
        }
        Pass
        { 
            Name "DecalScreenSpaceMesh"
            Tags 
            { 
                "LightMode" = "DecalScreenSpaceMesh"
            }
        
            // Render State
            Blend SrcAlpha OneMinusSrcAlpha
        ZTest LEqual
        ZWrite Off
        
            // Debug
            // <None>
        
            // --------------------------------------------------
            // Pass
        
            HLSLPROGRAM
        
            // Pragmas
            #pragma target 2.5
        #pragma vertex Vert
        #pragma fragment Frag
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma editor_sync_compilation
        
            // Keywords
            #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DYNAMICLIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        #pragma multi_compile _ _CLUSTERED_RENDERING
        #pragma multi_compile _DECAL_NORMAL_BLEND_LOW _DECAL_NORMAL_BLEND_MEDIUM _DECAL_NORMAL_BLEND_HIGH
            // GraphKeywords: <None>
        
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        
            // Defines
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_TEXCOORD2
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_VIEWDIRECTION_WS
            #define VARYINGS_NEED_TANGENT_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
            #define VARYINGS_NEED_SH
            #define VARYINGS_NEED_STATIC_LIGHTMAP_UV
            #define VARYINGS_NEED_DYNAMIC_LIGHTMAP_UV
            
        
        
            #define SHADERPASS SHADERPASS_DECAL_SCREEN_SPACE_MESH
        #define _MATERIAL_AFFECTS_ALBEDO 1
        #define _MATERIAL_AFFECTS_NORMAL 1
        #define _MATERIAL_AFFECTS_NORMAL_BLEND 1
        
            // HybridV1InjectedBuiltinProperties: <None>
        
            // -- Properties used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif
        
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"
        
            // --------------------------------------------------
            // Structs and Packing
        
            struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV : INTERP0;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV : INTERP1;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP2;
            #endif
             float4 tangentWS : INTERP3;
             float4 texCoord0 : INTERP4;
             float4 fogFactorAndVertexLight : INTERP5;
             float3 positionWS : INTERP6;
             float3 normalWS : INTERP7;
             float3 viewDirectionWS : INTERP8;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
            PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            output.viewDirectionWS.xyz = input.viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            output.viewDirectionWS = input.viewDirectionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
            // --------------------------------------------------
            // Graph
        
            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
        float4 Base_Map_TexelSize;
        float4 Normal_Map_TexelSize;
        float Normal_Blend;
        float _DrawOrder;
        float _DecalMeshBiasType;
        float _DecalMeshDepthBias;
        float _DecalMeshViewBias;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Base_Map);
        SAMPLER(samplerBase_Map);
        TEXTURE2D(Normal_Map);
        SAMPLER(samplerNormal_Map);
        
            // Graph Functions
            // GraphFunctions: <None>
        
            // Graph Vertex
            // GraphVertex: <None>
            
            // Graph Pixel
            struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
            float3 NormalTS;
            float NormalAlpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0 = UnityBuildTexture2DStructNoScale(Base_Map);
            float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.r;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.g;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.b;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.a;
            UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0 = UnityBuildTexture2DStructNoScale(Normal_Map);
            float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.r;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.g;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.b;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.a;
            float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0 = Normal_Blend;
            surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.xyz);
            surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7;
            surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.xyz);
            surface.NormalAlpha = _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0;
            return surface;
        }
        
            // --------------------------------------------------
            // Build Graph Inputs
        
            
        //     $features.graphPixel:   $include("SharedCode.template.hlsl")
        //                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
        // Packages/com.unity.shadergraph/Editor/Generation/Templates
        
            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */
        
        
        
            output.TangentSpaceNormal =                         float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 =                                        input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
            return output;
        }
        
            // --------------------------------------------------
            // Build Surface Data
        
            uint2 ComputeFadeMaskSeed(uint2 positionSS)
            {
                uint2 fadeMaskSeed;
        
                // Can't use the view direction, it is the same across the entire screen.
                fadeMaskSeed = positionSS;
        
                return fadeMaskSeed;
            }
        
            void GetSurfaceData(Varyings input, uint2 positionSS, float angleFadeFactor, out DecalSurfaceData surfaceData)
            {
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                    half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
                    float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
                    float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                    input.texCoord0.xy = input.texCoord0.xy * scale + offset;
                    half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
                    half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
                    half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
                    half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
                    input.normalWS.xyz = normalWS;
                    input.tangentWS.xyzw = half4(tangentWS, sign);
                #else
                    #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                        LODDitheringTransition(ComputeFadeMaskSeed(positionSS), unity_LODFade.x);
                    #endif
        
                    half fadeFactor = half(1.0);
                #endif
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.occlusion = half(1.0);
                surfaceData.smoothness = half(0);
        
                #ifdef _MATERIAL_AFFECTS_NORMAL
                    surfaceData.normalWS.w = half(1.0);
                #else
                    surfaceData.normalWS.w = half(0.0);
                #endif
        
        
                // copy across graph values, if defined
                surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
                surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);
        
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
                    #else
                        surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
                    #endif
                #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        float sgn = input.tangentWS.w;      // should be either +1 or -1
                        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        
                        // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                        surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
                    #else
                        surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
                    #endif
                #endif
        
                surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;
        
                // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
            }
        
            // --------------------------------------------------
            // Main
        
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl"
        
            ENDHLSL
        }
        Pass
        { 
            Name "DecalGBufferMesh"
            Tags 
            { 
                "LightMode" = "DecalGBufferMesh"
            }
        
            // Render State
            Blend 0 SrcAlpha OneMinusSrcAlpha
        Blend 1 SrcAlpha OneMinusSrcAlpha
        Blend 2 SrcAlpha OneMinusSrcAlpha
        Blend 3 SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ColorMask RGB
        ColorMask 0 1
        ColorMask RGB 2
        ColorMask RGB 3
        
            // Debug
            // <None>
        
            // --------------------------------------------------
            // Pass
        
            HLSLPROGRAM
        
            // Pragmas
            #pragma target 3.5
        #pragma vertex Vert
        #pragma fragment Frag
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma editor_sync_compilation
        
            // Keywords
            #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DYNAMICLIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
        #pragma multi_compile _DECAL_NORMAL_BLEND_LOW _DECAL_NORMAL_BLEND_MEDIUM _DECAL_NORMAL_BLEND_HIGH
        #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            // GraphKeywords: <None>
        
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        
            // Defines
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_TEXCOORD1
            #define ATTRIBUTES_NEED_TEXCOORD2
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_VIEWDIRECTION_WS
            #define VARYINGS_NEED_TANGENT_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
            #define VARYINGS_NEED_SH
            #define VARYINGS_NEED_STATIC_LIGHTMAP_UV
            #define VARYINGS_NEED_DYNAMIC_LIGHTMAP_UV
            
        
        
            #define SHADERPASS SHADERPASS_DECAL_GBUFFER_MESH
        #define _MATERIAL_AFFECTS_ALBEDO 1
        #define _MATERIAL_AFFECTS_NORMAL 1
        #define _MATERIAL_AFFECTS_NORMAL_BLEND 1
        
            // HybridV1InjectedBuiltinProperties: <None>
        
            // -- Properties used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif
        
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"
        
            // --------------------------------------------------
            // Structs and Packing
        
            struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV : INTERP0;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV : INTERP1;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP2;
            #endif
             float4 tangentWS : INTERP3;
             float4 texCoord0 : INTERP4;
             float4 fogFactorAndVertexLight : INTERP5;
             float3 positionWS : INTERP6;
             float3 normalWS : INTERP7;
             float3 viewDirectionWS : INTERP8;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
            PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            output.viewDirectionWS.xyz = input.viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            output.viewDirectionWS = input.viewDirectionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
            // --------------------------------------------------
            // Graph
        
            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
        float4 Base_Map_TexelSize;
        float4 Normal_Map_TexelSize;
        float Normal_Blend;
        float _DrawOrder;
        float _DecalMeshBiasType;
        float _DecalMeshDepthBias;
        float _DecalMeshViewBias;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Base_Map);
        SAMPLER(samplerBase_Map);
        TEXTURE2D(Normal_Map);
        SAMPLER(samplerNormal_Map);
        
            // Graph Functions
            // GraphFunctions: <None>
        
            // Graph Vertex
            // GraphVertex: <None>
            
            // Graph Pixel
            struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
            float3 NormalTS;
            float NormalAlpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            UnityTexture2D _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0 = UnityBuildTexture2DStructNoScale(Base_Map);
            float4 _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0 = SAMPLE_TEXTURE2D(_Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.tex, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.samplerstate, _Property_9f1059a7a93a46ccab349515214f3ed2_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_R_4 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.r;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_G_5 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.g;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_B_6 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.b;
            float _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7 = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.a;
            UnityTexture2D _Property_360e6833e8d64d75827ab98987b2b545_Out_0 = UnityBuildTexture2DStructNoScale(Normal_Map);
            float4 _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0 = SAMPLE_TEXTURE2D(_Property_360e6833e8d64d75827ab98987b2b545_Out_0.tex, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.samplerstate, _Property_360e6833e8d64d75827ab98987b2b545_Out_0.GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_R_4 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.r;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_G_5 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.g;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_B_6 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.b;
            float _SampleTexture2D_1300b7cb738f4b18927411750039acd2_A_7 = _SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.a;
            float _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0 = Normal_Blend;
            surface.BaseColor = (_SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_RGBA_0.xyz);
            surface.Alpha = _SampleTexture2D_7388a7ddbf6648ec92c3bb54ed055048_A_7;
            surface.NormalTS = (_SampleTexture2D_1300b7cb738f4b18927411750039acd2_RGBA_0.xyz);
            surface.NormalAlpha = _Property_91d01e73dcf34e7d96a9e731fb50e308_Out_0;
            return surface;
        }
        
            // --------------------------------------------------
            // Build Graph Inputs
        
            
        //     $features.graphPixel:   $include("SharedCode.template.hlsl")
        //                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
        // Packages/com.unity.shadergraph/Editor/Generation/Templates
        
            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */
        
        
        
            output.TangentSpaceNormal =                         float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 =                                        input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
            return output;
        }
        
            // --------------------------------------------------
            // Build Surface Data
        
            uint2 ComputeFadeMaskSeed(uint2 positionSS)
            {
                uint2 fadeMaskSeed;
        
                // Can't use the view direction, it is the same across the entire screen.
                fadeMaskSeed = positionSS;
        
                return fadeMaskSeed;
            }
        
            void GetSurfaceData(Varyings input, uint2 positionSS, float angleFadeFactor, out DecalSurfaceData surfaceData)
            {
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                    half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
                    float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
                    float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                    input.texCoord0.xy = input.texCoord0.xy * scale + offset;
                    half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
                    half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
                    half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
                    half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
                    input.normalWS.xyz = normalWS;
                    input.tangentWS.xyzw = half4(tangentWS, sign);
                #else
                    #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                        LODDitheringTransition(ComputeFadeMaskSeed(positionSS), unity_LODFade.x);
                    #endif
        
                    half fadeFactor = half(1.0);
                #endif
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.occlusion = half(1.0);
                surfaceData.smoothness = half(0);
        
                #ifdef _MATERIAL_AFFECTS_NORMAL
                    surfaceData.normalWS.w = half(1.0);
                #else
                    surfaceData.normalWS.w = half(0.0);
                #endif
        
        
                // copy across graph values, if defined
                surfaceData.baseColor.xyz = half3(surfaceDescription.BaseColor);
                surfaceData.baseColor.w = half(surfaceDescription.Alpha * fadeFactor);
        
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        surfaceData.normalWS.xyz = normalize(mul((half3x3)normalToWorld, surfaceDescription.NormalTS.xyz));
                    #else
                        surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
                    #endif
                #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        float sgn = input.tangentWS.w;      // should be either +1 or -1
                        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        
                        // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                        surfaceData.normalWS.xyz = normalize(TransformTangentToWorld(surfaceDescription.NormalTS, tangentToWorld));
                    #else
                        surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
                    #endif
                #endif
        
                surfaceData.normalWS.w = surfaceDescription.NormalAlpha * fadeFactor;
        
                // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
            }
        
            // --------------------------------------------------
            // Main
        
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl"
        
            ENDHLSL
        }
        Pass
        { 
            Name "ScenePickingPass"
            Tags 
            { 
                "LightMode" = "Picking"
            }
        
            // Render State
            Cull Back
        
            // Debug
            // <None>
        
            // --------------------------------------------------
            // Pass
        
            HLSLPROGRAM
        
            // Pragmas
            #pragma target 3.5
        #pragma vertex Vert
        #pragma fragment Frag
        #pragma multi_compile_instancing
        #pragma editor_sync_compilation
        
            // Keywords
            // PassKeywords: <None>
            // GraphKeywords: <None>
        
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        
            // Defines
            
        
        
            #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENEPICKINGPASS 1
        
            // HybridV1InjectedBuiltinProperties: <None>
        
            // -- Properties used by ScenePickingPass
            #ifdef SCENEPICKINGPASS
            float4 _SelectionID;
            #endif
        
            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DecalInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderVariablesDecal.hlsl"
        
            // --------------------------------------------------
            // Structs and Packing
        
            struct Attributes
        {
             float3 positionOS : POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
            PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
            // --------------------------------------------------
            // Graph
        
            // Graph Properties
            CBUFFER_START(UnityPerMaterial)
        float4 Base_Map_TexelSize;
        float4 Normal_Map_TexelSize;
        float Normal_Blend;
        float _DrawOrder;
        float _DecalMeshBiasType;
        float _DecalMeshDepthBias;
        float _DecalMeshViewBias;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Base_Map);
        SAMPLER(samplerBase_Map);
        TEXTURE2D(Normal_Map);
        SAMPLER(samplerNormal_Map);
        
            // Graph Functions
            // GraphFunctions: <None>
        
            // Graph Vertex
            // GraphVertex: <None>
            
            // Graph Pixel
            struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
            // --------------------------------------------------
            // Build Graph Inputs
        
            
        //     $features.graphPixel:   $include("SharedCode.template.hlsl")
        //                                       ^ ERROR: $include cannot find file : SharedCode.template.hlsl. Looked into:
        // Packages/com.unity.shadergraph/Editor/Generation/Templates
        
            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            /* WARNING: $splice Could not find named fragment 'CustomInterpolatorCopyToSDI' */
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
            return output;
        }
        
            // --------------------------------------------------
            // Build Surface Data
        
            uint2 ComputeFadeMaskSeed(uint2 positionSS)
            {
                uint2 fadeMaskSeed;
        
                // Can't use the view direction, it is the same across the entire screen.
                fadeMaskSeed = positionSS;
        
                return fadeMaskSeed;
            }
        
            void GetSurfaceData(Varyings input, uint2 positionSS, float angleFadeFactor, out DecalSurfaceData surfaceData)
            {
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    half4x4 normalToWorld = UNITY_ACCESS_INSTANCED_PROP(Decal, _NormalToWorld);
                    half fadeFactor = clamp(normalToWorld[0][3], 0.0f, 1.0f) * angleFadeFactor;
                    float2 scale = float2(normalToWorld[3][0], normalToWorld[3][1]);
                    float2 offset = float2(normalToWorld[3][2], normalToWorld[3][3]);
                    half3 normalWS = TransformObjectToWorldDir(half3(0, 1, 0));
                    half3 tangentWS = TransformObjectToWorldDir(half3(1, 0, 0));
                    half3 bitangentWS = TransformObjectToWorldDir(half3(0, 0, 1));
                    half sign = dot(cross(normalWS, tangentWS), bitangentWS) > 0 ? 1 : -1;
                #else
                    #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                        LODDitheringTransition(ComputeFadeMaskSeed(positionSS), unity_LODFade.x);
                    #endif
        
                    half fadeFactor = half(1.0);
                #endif
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(input);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.occlusion = half(1.0);
                surfaceData.smoothness = half(0);
        
                #ifdef _MATERIAL_AFFECTS_NORMAL
                    surfaceData.normalWS.w = half(1.0);
                #else
                    surfaceData.normalWS.w = half(0.0);
                #endif
        
        
                // copy across graph values, if defined
        
                #if (SHADERPASS == SHADERPASS_DBUFFER_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_PROJECTOR) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_PROJECTOR)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                    #else
                        surfaceData.normalWS.xyz = normalize(normalToWorld[2].xyz);
                    #endif
                #elif (SHADERPASS == SHADERPASS_DBUFFER_MESH) || (SHADERPASS == SHADERPASS_DECAL_SCREEN_SPACE_MESH) || (SHADERPASS == SHADERPASS_DECAL_GBUFFER_MESH)
                    #if defined(_MATERIAL_AFFECTS_NORMAL)
                        float sgn = input.tangentWS.w;      // should be either +1 or -1
                        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                        half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
        
                        // We need to normalize as we use mikkt tangent space and this is expected (tangent space is not normalize)
                    #else
                        surfaceData.normalWS.xyz = normalize(half3(input.normalWS)); // Default to vertex normal
                    #endif
                #endif
        
        
                // In case of Smoothness / AO / Metal, all the three are always computed but color mask can change
            }
        
            // --------------------------------------------------
            // Main
        
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl"
        
            ENDHLSL
        }
    }
    CustomEditorForRenderPipeline "UnityEditor.Rendering.Universal.DecalShaderGraphGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
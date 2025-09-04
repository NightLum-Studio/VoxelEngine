Shader "Universal Render Pipeline/LitAtlasWrapper"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [Header(Main Texture Arrays)]
        [MainTexture] _BaseMapArray("Albedo Texture Array", 2DArray) = "white" {}
        _BaseMap("Albedo (for editor)", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        [Header(Lighting)]
        _AmbientStrength("Ambient Light Strength", Range(0.0, 1.0)) = 0.3
        _AmbientColor("Ambient Light Color", Color) = (0.2, 0.2, 0.3, 1.0)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        [Header(Surface Arrays)]
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMapArray("Metallic/Smoothness Array", 2DArray) = "white" {}
        _MetallicGlossMap("Metallic (for editor)", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMapArray("Specular Array", 2DArray) = "white" {}
        _SpecGlossMap("Specular (for editor)", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        [Header(Normal Mapping)]
        _BumpScale("Scale", Float) = 1.0
        _BumpMapArray("Normal Map Array", 2DArray) = "bump" {}
        _BumpMap("Normal Map (for editor)", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMapArray("Height Map Array", 2DArray) = "black" {}
        _ParallaxMap("Height Map (for editor)", 2D) = "black" {}

        // Occlusion is handled automatically by Unity's lighting system

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMapArray("Emission Array", 2DArray) = "white" {}
        _EmissionMap("Emission (for editor)", 2D) = "white" {}

        _DetailMaskArray("Detail Mask Array", 2DArray) = "white" {}
        _DetailMask("Detail Mask (for editor)", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMapArray("Detail Albedo x2 Array", 2DArray) = "linearGrey" {}
        _DetailAlbedoMap("Detail Albedo x2 (for editor)", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMapArray("Normal Map Array", 2DArray) = "bump" {}
        [Normal] _DetailNormalMap("Normal Map (for editor)", 2D) = "bump" {}

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

        // Texture Array controls
        [Header(Texture Array Settings)]
        _ArraySliceCount ("Array Slice Count", Float) = 64
        [ToggleUI] _EnableTiling ("Enable UV Tiling", Float) = 1
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertexArray
            #pragma fragment LitPassFragmentArray

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"


            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // Ensure Attributes has uv2 for tile index
            #define ATTRIBUTES_NEED_TEXCOORD2 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Material property declarations
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float4 _EmissionColor;
                float _AmbientStrength;
                float4 _AmbientColor;
                float _Cutoff;
                float _Smoothness;
                float _Metallic;
                float _BumpScale;
                float _Parallax;

                float _ClearCoatMask;
                float _ClearCoatSmoothness;
                float _DetailAlbedoMapScale;
                float _DetailNormalMapScale;
                float _Surface;
                float _ArraySliceCount;
                
                // Texture transform properties (required by Unity)
                float4 _BaseMap_ST;
                float4 _BaseMapArray_ST;
                float4 _BumpMapArray_ST;
                float4 _MetallicGlossMapArray_ST;
                float4 _SpecGlossMapArray_ST;

                float4 _EmissionMapArray_ST;
                float4 _DetailMaskArray_ST;
                float4 _DetailAlbedoMapArray_ST;
                float4 _DetailNormalMapArray_ST;
                float4 _ParallaxMapArray_ST;
            CBUFFER_END

            // Override texture declarations with texture arrays
            TEXTURE2D_ARRAY(_BaseMapArray);
            SAMPLER(sampler_BaseMapArray);
            TEXTURE2D_ARRAY(_BumpMapArray);
            SAMPLER(sampler_BumpMapArray);
            TEXTURE2D_ARRAY(_MetallicGlossMapArray);
            SAMPLER(sampler_MetallicGlossMapArray);
            TEXTURE2D_ARRAY(_SpecGlossMapArray);
            SAMPLER(sampler_SpecGlossMapArray);

            TEXTURE2D_ARRAY(_EmissionMapArray);
            SAMPLER(sampler_EmissionMapArray);
            TEXTURE2D_ARRAY(_DetailMaskArray);
            SAMPLER(sampler_DetailMaskArray);
            TEXTURE2D_ARRAY(_DetailAlbedoMapArray);
            SAMPLER(sampler_DetailAlbedoMapArray);
            TEXTURE2D_ARRAY(_DetailNormalMapArray);
            SAMPLER(sampler_DetailNormalMapArray);
            TEXTURE2D_ARRAY(_ParallaxMapArray);
            SAMPLER(sampler_ParallaxMapArray);

            // Helper to get texture array slice and UV for greedy meshing
            // tileIndex.x contains the slice index, uvGreedy contains the scaled UV coordinates
            float3 GetArrayUVAndSlice(float2 uvGreedy, float2 tileIndex)
            {
                // Use tile index X as the array slice index
                float sliceIndex = tileIndex.x;
                // Keep the full UV coordinates for proper tiling (don't use frac!)
                // The greedy mesher outputs UVs like (2.0, 3.0) for a 2x3 quad
                float2 uv = uvGreedy;
                return float3(uv, sliceIndex);
            }
            
            // Custom texture sampling functions for texture arrays
            // These functions properly handle UV tiling for greedy meshed quads
            float4 SampleBaseMapArray(float2 uv, float slice)
            {
                // The sampler will handle wrapping/tiling automatically
                return SAMPLE_TEXTURE2D_ARRAY(_BaseMapArray, sampler_BaseMapArray, uv, slice);
            }
            
            float3 SampleNormalArray(float2 uv, float slice, float scale = 1.0)
            {
                float4 n = SAMPLE_TEXTURE2D_ARRAY(_BumpMapArray, sampler_BumpMapArray, uv, slice);
                return UnpackNormalScale(n, scale);
            }
            
            float4 SampleMetallicSpecGlossArray(float2 uv, float slice)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_MetallicGlossMapArray, sampler_MetallicGlossMapArray, uv, slice);
            }
            

            
            float3 SampleEmissionArray(float2 uv, float slice)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_EmissionMapArray, sampler_EmissionMapArray, uv, slice).rgb;
            }

            // Macro overrides for texture array sampling
            // Note: These return float3 (UV + slice) instead of float2
            #undef GetVertexAlbedoUV
            #define GetVertexAlbedoUV(input) GetArrayUVAndSlice((input).uv, (input).uv2).xy
            
            #undef GetVertexNormalUV
            #define GetVertexNormalUV(input) GetArrayUVAndSlice((input).uv, (input).uv2).xy

            #undef GetVertexMetallicSpecGlossUV
            #define GetVertexMetallicSpecGlossUV(input) GetArrayUVAndSlice((input).uv, (input).uv2).xy

            #undef GetVertexSpecularUV
            #define GetVertexSpecularUV(input) GetArrayUVAndSlice((input).uv, (input).uv2).xy

            #undef GetVertexOcclusionUV
            #define GetVertexOcclusionUV(input) GetArrayUVAndSlice((input).uv, (input).uv2).xy

            #undef GetVertexEmissionUV
            #define GetVertexEmissionUV(input) GetArrayUVAndSlice((input).uv, (input).uv2).xy
            
            // Additional macro to get the slice index
            #define GetVertexArraySlice(input) GetArrayUVAndSlice((input).uv, (input).uv2).z

            // Custom vertex and fragment functions that handle texture arrays
            struct AttributesArray
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float2 texcoord1    : TEXCOORD1;
                float2 tileIndex    : TEXCOORD2;  // Our tile index from compute shader
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsArray
            {
                float2 uv                       : TEXCOORD0;
                float2 tileIndex               : TEXCOORD1;
                float3 positionWS               : TEXCOORD2;
                float3 normalWS                 : TEXCOORD3;
                float4 tangentWS                : TEXCOORD4;
                float3 viewDirWS                : TEXCOORD5;
                half4 fogFactorAndVertexLight   : TEXCOORD6;
                float4 shadowCoord              : TEXCOORD7;
                #if defined(LIGHTMAP_ON)
                    float2 staticLightmapUV     : TEXCOORD8;
                #endif
                #if !defined(LIGHTMAP_ON)
                    half3 vertexSH              : TEXCOORD8;
                #endif
                float4 positionCS               : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsArray LitPassVertexArray(AttributesArray input)
            {
                VaryingsArray output = (VaryingsArray)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                output.uv = input.texcoord;
                output.tileIndex = input.tileIndex;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = viewDirWS;
                output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                output.positionWS = vertexInput.positionWS;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.positionCS = vertexInput.positionCS;
                
                #if defined(LIGHTMAP_ON)
                    output.staticLightmapUV = input.texcoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                #if !defined(LIGHTMAP_ON)
                    output.vertexSH = SampleSHVertex(output.normalWS.xyz);
                #endif

                return output;
            }

            half4 LitPassFragmentArray(VaryingsArray input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 uvAndSlice = GetArrayUVAndSlice(input.uv, input.tileIndex);
                float2 uv = uvAndSlice.xy;
                float slice = uvAndSlice.z;

                // Sample textures from array
                half4 albedoAlpha = SampleBaseMapArray(uv, slice);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                half alpha = albedoAlpha.a * _BaseColor.a;
                
                // Debug: Visualize UV tiling and values (uncomment to debug)
                // if (uv.x > 1.0 || uv.y > 1.0) {
                //     albedo = lerp(albedo, float3(1, 0, 0), 0.5); // Red tint for tiled areas
                // }
                
                // Additional debug: Show UV scale as color intensity (uncomment to debug)
                // float uvScale = max(uv.x, uv.y);
                // if (uvScale > 1.0) {
                //     albedo = lerp(albedo, float3(0, uvScale/4.0, 0), 0.3); // Green based on UV scale
                // }

                #ifdef _ALPHATEST_ON
                    clip(alpha - _Cutoff);
                #endif

                // Sample normal map
                half3 normalTS = SampleNormalArray(uv, slice, _BumpScale);
                
                // Build surface data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = alpha;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1.0; // Let Unity handle AO automatically
                surfaceData.emission = SampleEmissionArray(uv, slice) * _EmissionColor.rgb;

                #ifdef _METALLICSPECGLOSSMAP
                    half4 metallicGloss = SampleMetallicSpecGlossArray(uv, slice);
                    surfaceData.metallic = metallicGloss.r;
                    surfaceData.smoothness = metallicGloss.a;
                #endif

                // Build input data
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = SafeNormalize(input.viewDirWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactorAndVertexLight.x;
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #if defined(LIGHTMAP_ON)
                    inputData.bakedGI = SampleLightmap(input.staticLightmapUV, inputData.normalWS);
                #else
                    inputData.bakedGI = SampleSH(inputData.normalWS);
                #endif

                #ifdef _NORMALMAP
                    float sgn = input.tangentWS.w;
                    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                    inputData.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
                    inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // Add custom ambient lighting to prevent pure black shadows
                half3 ambientContribution = _AmbientColor.rgb * _AmbientStrength;
                color.rgb = max(color.rgb, ambientContribution * surfaceData.albedo);
                
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, _Surface);

                return color;
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitGBufferPassVertexArray
            #pragma fragment LitGBufferPassFragmentArray

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            // Ensure Attributes carries uv2 (tile index)
            #define ATTRIBUTES_NEED_TEXCOORD2 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

            // Material property declarations for GBuffer pass
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float4 _EmissionColor;
                float _AmbientStrength;
                float4 _AmbientColor;
                float _Cutoff;
                float _Smoothness;
                float _Metallic;
                float _BumpScale;
                float _Parallax;

                float _ClearCoatMask;
                float _ClearCoatSmoothness;
                float _DetailAlbedoMapScale;
                float _DetailNormalMapScale;
                float _Surface;
                float _ArraySliceCount;
                
                // Texture transform properties (required by Unity)
                float4 _BaseMap_ST;
                float4 _BaseMapArray_ST;
                float4 _BumpMapArray_ST;
                float4 _MetallicGlossMapArray_ST;
                float4 _SpecGlossMapArray_ST;

                float4 _EmissionMapArray_ST;
                float4 _DetailMaskArray_ST;
                float4 _DetailAlbedoMapArray_ST;
                float4 _DetailNormalMapArray_ST;
                float4 _ParallaxMapArray_ST;
            CBUFFER_END

            // Texture array declarations for GBuffer pass
            TEXTURE2D_ARRAY(_BaseMapArray);
            SAMPLER(sampler_BaseMapArray);
            TEXTURE2D_ARRAY(_BumpMapArray);
            SAMPLER(sampler_BumpMapArray);
            TEXTURE2D_ARRAY(_MetallicGlossMapArray);
            SAMPLER(sampler_MetallicGlossMapArray);

            TEXTURE2D_ARRAY(_EmissionMapArray);
            SAMPLER(sampler_EmissionMapArray);

            // Custom texture sampling functions for GBuffer pass
            float4 SampleBaseMapArray_G(float2 uv, float slice)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_BaseMapArray, sampler_BaseMapArray, uv, slice);
            }
            
            float3 SampleNormalArray_G(float2 uv, float slice, float scale = 1.0)
            {
                float4 n = SAMPLE_TEXTURE2D_ARRAY(_BumpMapArray, sampler_BumpMapArray, uv, slice);
                return UnpackNormalScale(n, scale);
            }
            
            float4 SampleMetallicSpecGlossArray_G(float2 uv, float slice)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_MetallicGlossMapArray, sampler_MetallicGlossMapArray, uv, slice);
            }
            

            
            float3 SampleEmissionArray_G(float2 uv, float slice)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_EmissionMapArray, sampler_EmissionMapArray, uv, slice).rgb;
            }

            float3 GetArrayUVAndSlice_G(float2 uvGreedy, float2 tileIndex)
            {
                float sliceIndex = tileIndex.x;
                // Keep the full UV coordinates for proper tiling (don't use frac!)
                float2 uv = uvGreedy;
                return float3(uv, sliceIndex);
            }
            
            // Custom GBuffer vertex and fragment functions for texture arrays
            struct AttributesGBuffer
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float2 texcoord1    : TEXCOORD1;
                float2 tileIndex    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsGBuffer
            {
                float2 uv           : TEXCOORD0;
                float2 tileIndex    : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
                float3 normalWS     : TEXCOORD3;
                float4 tangentWS    : TEXCOORD4;
                #if defined(LIGHTMAP_ON)
                    float2 staticLightmapUV : TEXCOORD5;
                #endif
                #if !defined(LIGHTMAP_ON)
                    half3 vertexSH : TEXCOORD5;
                #endif
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsGBuffer LitGBufferPassVertexArray(AttributesGBuffer input)
            {
                VaryingsGBuffer output = (VaryingsGBuffer)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = input.texcoord;
                output.tileIndex = input.tileIndex;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;

                #if defined(LIGHTMAP_ON)
                    output.staticLightmapUV = input.texcoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                #if !defined(LIGHTMAP_ON)
                    output.vertexSH = SampleSHVertex(output.normalWS.xyz);
                #endif

                return output;
            }

            struct GBufferType
            {
                half4 GBuffer0 : SV_Target0;
                half4 GBuffer1 : SV_Target1;
                half4 GBuffer2 : SV_Target2;
                half4 GBuffer3 : SV_Target3;
            };

            GBufferType LitGBufferPassFragmentArray(VaryingsGBuffer input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 uvAndSlice = GetArrayUVAndSlice_G(input.uv, input.tileIndex);
                float2 uv = uvAndSlice.xy;
                float slice = uvAndSlice.z;

                // Sample textures from array
                half4 albedoAlpha = SampleBaseMapArray_G(uv, slice);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                half alpha = albedoAlpha.a * _BaseColor.a;
                
                // Debug: Visualize UV tiling (uncomment to debug)
                // if (uv.x > 1.0 || uv.y > 1.0) {
                //     albedo = lerp(albedo, float3(1, 0, 0), 0.5); // Red tint for tiled areas
                // }

                #ifdef _ALPHATEST_ON
                    clip(alpha - _Cutoff);
                #endif

                // Sample normal map
                half3 normalTS = SampleNormalArray_G(uv, slice, _BumpScale);
                
                // Transform normal to world space
                half3 normalWS = input.normalWS;
                #ifdef _NORMALMAP
                    float sgn = input.tangentWS.w;
                    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
                    normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                #endif
                normalWS = NormalizeNormalPerPixel(normalWS);

                // Material properties
                half metallic = _Metallic;
                half smoothness = _Smoothness;
                half occlusion = 1.0; // Let Unity handle AO automatically
                half3 emission = SampleEmissionArray_G(uv, slice) * _EmissionColor.rgb;

                #ifdef _METALLICSPECGLOSSMAP
                    half4 metallicGloss = SampleMetallicSpecGlossArray_G(uv, slice);
                    metallic = metallicGloss.r;
                    smoothness = metallicGloss.a;
                #endif

                // Add custom ambient lighting to emission for deferred rendering
                half3 ambientContribution = _AmbientColor.rgb * _AmbientStrength * albedo;
                emission += ambientContribution;

                // Pack GBuffer data
                GBufferType output;
                output.GBuffer0 = half4(albedo, occlusion);
                output.GBuffer1 = half4(0.0, 0.0, 0.0, metallic);
                output.GBuffer2 = half4(normalWS * 0.5 + 0.5, smoothness);
                output.GBuffer3 = half4(emission, 1.0);
                
                return output;
            }
            
            #pragma vertex LitGBufferPassVertexArray
            #pragma fragment LitGBufferPassFragmentArray
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #define ATTRIBUTES_NEED_TEXCOORD2 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            
            // Material property declarations for DepthNormals pass
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float4 _EmissionColor;
                float _AmbientStrength;
                float4 _AmbientColor;
                float _Cutoff;
                float _Smoothness;
                float _Metallic;
                float _BumpScale;
                float _Parallax;

                float _ClearCoatMask;
                float _ClearCoatSmoothness;
                float _DetailAlbedoMapScale;
                float _DetailNormalMapScale;
                float _Surface;
                float _ArraySliceCount;
                
                // Texture transform properties (required by Unity)
                float4 _BaseMap_ST;
                float4 _BaseMapArray_ST;
                float4 _BumpMapArray_ST;
                float4 _MetallicGlossMapArray_ST;
                float4 _SpecGlossMapArray_ST;

                float4 _EmissionMapArray_ST;
                float4 _DetailMaskArray_ST;
                float4 _DetailAlbedoMapArray_ST;
                float4 _DetailNormalMapArray_ST;
                float4 _ParallaxMapArray_ST;
            CBUFFER_END

            float3 GetArrayUVAndSlice_DN(float2 uvGreedy, float2 tileIndex)
            {
                float sliceIndex = tileIndex.x;
                // Keep the full UV coordinates for proper tiling (don't use frac!)
                float2 uv = uvGreedy;
                return float3(uv, sliceIndex);
            }
            #undef GetVertexNormalUV
            #define GetVertexNormalUV(input) GetArrayUVAndSlice_DN((input).uv, (input).uv2).xy
            #define GetVertexArraySlice_DN(input) GetArrayUVAndSlice_DN((input).uv, (input).uv2).z
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "XRMotionVectors"
            Tags { "LightMode" = "XRMotionVectors" }
            ColorMask RGBA

            // Stencil write for obj motion pixels
            Stencil
            {
                WriteMask 1
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #define APLICATION_SPACE_WARP_MOTION 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "TextureArrayShaderGUI"
}

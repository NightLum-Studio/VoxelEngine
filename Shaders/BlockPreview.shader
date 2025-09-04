Shader "VoxelEngine/BlockPreview"
{
    Properties
    {
        _TextureArray ("Texture Array", 2DArray) = "white" {}
        _TopFace ("Top Face Index", Float) = 0
        _BottomFace ("Bottom Face Index", Float) = 0
        _NorthFace ("North Face Index", Float) = 0
        _SouthFace ("South Face Index", Float) = 0
        _EastFace ("East Face Index", Float) = 0
        _WestFace ("West Face Index", Float) = 0
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.0
        _Outline ("Outline Width", Range(0, 0.1)) = 0.02
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
    }
    
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 200

        // Main Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 objectNormal : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D_ARRAY(_TextureArray);
            SAMPLER(sampler_TextureArray);

            CBUFFER_START(UnityPerMaterial)
                float _TopFace;
                float _BottomFace;
                float _NorthFace;
                float _SouthFace;
                float _EastFace;
                float _WestFace;
                float _Brightness;
                float _Outline;
                float4 _OutlineColor;
            CBUFFER_END

            float GetFaceTextureIndex(float3 normal)
            {
                // Determine which face we're on based on normal
                float3 absNormal = abs(normal);
                
                // Find dominant axis
                if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
                {
                    // Y-dominant
                    return normal.y > 0 ? _TopFace : _BottomFace;
                }
                else if (absNormal.x > absNormal.z)
                {
                    // X-dominant
                    return normal.x > 0 ? _EastFace : _WestFace;
                }
                else
                {
                    // Z-dominant
                    return normal.z > 0 ? _NorthFace : _SouthFace;
                }
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                output.objectNormal = input.normalOS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Get face-specific texture index
                float textureIndex = GetFaceTextureIndex(input.objectNormal);
                
                // Debug: Clamp texture index to valid range
                textureIndex = max(0, textureIndex);
                
                // Sample texture array with explicit mip level
                half4 albedo = SAMPLE_TEXTURE2D_ARRAY_LOD(_TextureArray, sampler_TextureArray, input.uv, textureIndex, 0);
                
                // Fallback: If texture array sampling fails, show a color pattern based on face
                if (albedo.a < 0.1) // If alpha is too low, probably failed to sample
                {
                    // Color code each face for debugging
                    float3 debugColor = float3(0.5, 0.5, 0.5); // Default gray
                    float3 absNormal = abs(input.objectNormal);
                    
                    if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
                    {
                        // Y-dominant (top/bottom)
                        debugColor = input.objectNormal.y > 0 ? float3(0.8, 1, 0.8) : float3(0.8, 0.8, 1); // Green for top, blue for bottom
                    }
                    else if (absNormal.x > absNormal.z)
                    {
                        // X-dominant (east/west)
                        debugColor = input.objectNormal.x > 0 ? float3(1, 0.9, 0.8) : float3(0.9, 0.8, 1); // Orange for east, purple for west
                    }
                    else
                    {
                        // Z-dominant (north/south)
                        debugColor = input.objectNormal.z > 0 ? float3(1, 0.8, 0.9) : float3(0.9, 1, 0.8); // Pink for north, yellow-green for south
                    }
                    
                    albedo = half4(debugColor, 1);
                }
                
                // Simple lighting
                Light mainLight = GetMainLight();
                float3 normalWS = normalize(input.normalWS);
                float NdotL = saturate(dot(normalWS, mainLight.direction)) * 0.7 + 0.3; // Add some ambient
                
                // Apply lighting and brightness
                half3 color = albedo.rgb * NdotL * _Brightness;
                
                // Add subtle outline
                if (_Outline > 0)
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                    float rim = 1.0 - saturate(dot(viewDir, normalWS));
                    float outline = smoothstep(0.6, 0.8, rim);
                    color = lerp(color, _OutlineColor.rgb, outline * _Outline * 10);
                }
                
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Outline Pass (alternative method)
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Outline;
                float4 _OutlineColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Expand vertices along normal for outline effect
                float3 expandedPos = input.positionOS.xyz + input.normalOS * _Outline;
                output.positionHCS = TransformObjectToHClip(expandedPos);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

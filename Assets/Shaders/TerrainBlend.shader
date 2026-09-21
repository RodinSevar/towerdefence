// Blends up to 7 ground textures per pixel from per-vertex weights (weights 0-3 in the vertex colour, 4-6 in uv0.xyz).
// Textures are projected from world space, so they never stretch on slopes:
//  - ground (_IsWall = 0): projected from above (xz)
//  - cliff walls (_IsWall = 1): projected horizontally along the wall, so strata stay horizontal
// The same shader is used for the ground material (7 tiles) and the wall material (cliff textures in slots 0 and 1).
// One shared sampler state keeps the sampler count well under the platform limit.
Shader "Wintermaul/TerrainBlend"
{
    Properties
    {
        _Tex0("Tile 0", 2D) = "white" {}
        _Tex1("Tile 1", 2D) = "white" {}
        _Tex2("Tile 2", 2D) = "white" {}
        _Tex3("Tile 3", 2D) = "white" {}
        _Tex4("Tile 4", 2D) = "white" {}
        _Tex5("Tile 5", 2D) = "white" {}
        _Tex6("Tile 6", 2D) = "white" {}
        _Nrm0("Normal 0", 2D) = "bump" {}
        _Nrm1("Normal 1", 2D) = "bump" {}
        _Nrm2("Normal 2", 2D) = "bump" {}
        _Nrm3("Normal 3", 2D) = "bump" {}
        _Nrm4("Normal 4", 2D) = "bump" {}
        _Nrm5("Normal 5", 2D) = "bump" {}
        _Nrm6("Normal 6", 2D) = "bump" {}
        _TileSize("World units per texture repeat", Float) = 8
        _IsWall("Wall projection (0 = ground, 1 = wall)", Float) = 0
        _NormalStrength("Normal map strength", Float) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _TileSize;
            float _IsWall;
            float _NormalStrength;
            float _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Tex0); SAMPLER(sampler_Tex0);
            TEXTURE2D(_Tex1); TEXTURE2D(_Tex2); TEXTURE2D(_Tex3); TEXTURE2D(_Tex4); TEXTURE2D(_Tex5); TEXTURE2D(_Tex6);
            TEXTURE2D(_Nrm0); TEXTURE2D(_Nrm1); TEXTURE2D(_Nrm2); TEXTURE2D(_Nrm3); TEXTURE2D(_Nrm4); TEXTURE2D(_Nrm5); TEXTURE2D(_Nrm6);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 weights0 : COLOR;      // tile weights 0..3
                float4 weights1 : TEXCOORD0;  // tile weights 4..6 in xyz
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 weights0 : TEXCOORD2;
                float4 weights1 : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.weights0 = input.weights0;
                o.weights1 = input.weights1;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);

                float2 uv;
                float3 T, B;
                if (_IsWall > 0.5)
                {
                    bool xFacing = abs(N.x) > abs(N.z);
                    uv = float2(xFacing ? i.positionWS.z : i.positionWS.x, i.positionWS.y) / _TileSize;
                    T = xFacing ? float3(0, 0, 1) : float3(1, 0, 0);
                    B = float3(0, 1, 0);
                }
                else
                {
                    uv = i.positionWS.xz / _TileSize;
                    T = float3(1, 0, 0);
                    B = float3(0, 0, 1);
                }

                float w[7] = { i.weights0.x, i.weights0.y, i.weights0.z, i.weights0.w, i.weights1.x, i.weights1.y, i.weights1.z };
                float total = w[0] + w[1] + w[2] + w[3] + w[4] + w[5] + w[6];
                float inv = 1.0 / max(total, 1e-4);

                // All textures are sampled unconditionally (sampling inside branches breaks screen-space derivatives)
                half3 albedo =
                    SAMPLE_TEXTURE2D(_Tex0, sampler_Tex0, uv).rgb * (w[0] * inv) +
                    SAMPLE_TEXTURE2D(_Tex1, sampler_Tex0, uv).rgb * (w[1] * inv) +
                    SAMPLE_TEXTURE2D(_Tex2, sampler_Tex0, uv).rgb * (w[2] * inv) +
                    SAMPLE_TEXTURE2D(_Tex3, sampler_Tex0, uv).rgb * (w[3] * inv) +
                    SAMPLE_TEXTURE2D(_Tex4, sampler_Tex0, uv).rgb * (w[4] * inv) +
                    SAMPLE_TEXTURE2D(_Tex5, sampler_Tex0, uv).rgb * (w[5] * inv) +
                    SAMPLE_TEXTURE2D(_Tex6, sampler_Tex0, uv).rgb * (w[6] * inv);

                float2 nrm =
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_Nrm0, sampler_Tex0, uv), _NormalStrength).xy * (w[0] * inv) +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_Nrm1, sampler_Tex0, uv), _NormalStrength).xy * (w[1] * inv) +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_Nrm2, sampler_Tex0, uv), _NormalStrength).xy * (w[2] * inv) +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_Nrm3, sampler_Tex0, uv), _NormalStrength).xy * (w[3] * inv) +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_Nrm4, sampler_Tex0, uv), _NormalStrength).xy * (w[4] * inv) +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_Nrm5, sampler_Tex0, uv), _NormalStrength).xy * (w[5] * inv) +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_Nrm6, sampler_Tex0, uv), _NormalStrength).xy * (w[6] * inv);

                float3 normalWS = normalize(N + T * nrm.x + B * nrm.y);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = i.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(ShadowVaryings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(DepthVaryings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}

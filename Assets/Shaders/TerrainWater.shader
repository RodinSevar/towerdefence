// Animated, semi-transparent water: two scrolling copies of a tileable water texture (colour + normal), lit by the
// main light with a specular highlight. Alpha-blended, no depth write.
Shader "Wintermaul/TerrainWater"
{
    Properties
    {
        _WaterTex("Water", 2D) = "white" {}
        _WaterNrm("Water normal", 2D) = "bump" {}
        _Tint("Tint", Color) = (0.55, 0.75, 0.95, 1)
        _Alpha("Opacity", Range(0, 1)) = 0.82
        _TileSize("World units per texture repeat", Float) = 10
        _Speed("Scroll speed", Float) = 0.6
        _Shininess("Highlight sharpness", Float) = 90
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WaterTex); SAMPLER(sampler_WaterTex);
            TEXTURE2D(_WaterNrm);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Alpha;
                float _TileSize;
                float _Speed;
                float _Shininess;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uvA = i.positionWS.xz / _TileSize + float2(0.020, 0.013) * t;
                float2 uvB = i.positionWS.xz / (_TileSize * 0.6) + float2(-0.014, 0.019) * t;

                half3 albedo = (SAMPLE_TEXTURE2D(_WaterTex, sampler_WaterTex, uvA).rgb +
                                SAMPLE_TEXTURE2D(_WaterTex, sampler_WaterTex, uvB).rgb) * 0.5 * _Tint.rgb;
                float2 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaterNrm, sampler_WaterTex, uvA), 1.0).xy +
                           UnpackNormalScale(SAMPLE_TEXTURE2D(_WaterNrm, sampler_WaterTex, uvB), 1.0).xy;
                float3 normalWS = normalize(float3(n.x * 0.5, 1.0, n.y * 0.5));

                Light mainLight = GetMainLight();
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                half3 diffuse = saturate(dot(normalWS, mainLight.direction)) * mainLight.color;
                half3 ambient = SampleSH(normalWS);
                half3 spec = pow(saturate(dot(normalWS, halfDir)), _Shininess) * mainLight.color * 0.6;

                half3 rgb = albedo * (ambient + diffuse * 0.8) + spec;
                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, _Alpha);
            }
            ENDHLSL
        }
    }
}

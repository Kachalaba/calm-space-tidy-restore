Shader "CalmSpace/CleanableSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Clean Surface", 2D) = "white" {}
        [MainColor] _BaseColor("Clean Tint", Color) = (0.9, 0.95, 1, 1)
        _DirtColor("Dirt Tint", Color) = (0.22, 0.16, 0.10, 1)
        _DirtStrength("Dirt Strength", Range(0, 1)) = 0.85
        [HideInInspector] _CleanMask("Clean Mask", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CleanMask);
            SAMPLER(sampler_CleanMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DirtColor;
                half _DirtStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(
                    input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(
                    output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 cleanSurface = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv) * _BaseColor;
                half cleanCoverage = saturate(
                    SAMPLE_TEXTURE2D(
                        _CleanMask,
                        sampler_CleanMask,
                        input.uv).r);
                half3 dirtySurface = lerp(
                    cleanSurface.rgb,
                    cleanSurface.rgb * _DirtColor.rgb,
                    _DirtStrength);
                half3 color = lerp(
                    dirtySurface,
                    cleanSurface.rgb,
                    cleanCoverage);
                color = MixFog(color, input.fogFactor);
                return half4(color, cleanSurface.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

Shader "Hidden/CalmSpace/CoverageDownsample"
{
    Properties
    {
        [HideInInspector] _SourceMask("Source Mask", 2D) = "black" {}
        [HideInInspector] _CoverageSampleStepUv("Coverage Sample Step UV", Vector) = (0.00390625, 0.00390625, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "CoverageDownsample"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SourceMask);
            SAMPLER(sampler_SourceMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoverageSampleStepUv;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv =
                    GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const float offsets[4] =
                {
                    -1.5,
                    -0.5,
                    0.5,
                    1.5
                };

                float coverage = 0.0;
                [unroll]
                for (int y = 0; y < 4; y++)
                {
                    [unroll]
                    for (int x = 0; x < 4; x++)
                    {
                        float2 uv = input.uv +
                            float2(offsets[x], offsets[y]) *
                            _CoverageSampleStepUv.xy;
                        coverage += SAMPLE_TEXTURE2D(
                            _SourceMask,
                            sampler_SourceMask,
                            saturate(uv)).r;
                    }
                }

                coverage *= 0.0625;
                return half4(
                    coverage,
                    coverage,
                    coverage,
                    coverage);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

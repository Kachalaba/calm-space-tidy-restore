Shader "Hidden/CalmSpace/MaskBrush"
{
    Properties
    {
        [HideInInspector] _SourceMask("Source Mask", 2D) = "black" {}
        [HideInInspector] _BrushUvRadiusHardness("Brush UV Radius Hardness", Vector) = (0.5, 0.5, 0.05, 1.0)
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
            Name "MaskBrush"

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
                float4 _BrushUvRadiusHardness;
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
                float existingMask = SAMPLE_TEXTURE2D(
                    _SourceMask,
                    sampler_SourceMask,
                    input.uv).r;
                float2 brushUv = _BrushUvRadiusHardness.xy;
                float outerRadius = _BrushUvRadiusHardness.z;
                float innerRadius =
                    outerRadius * saturate(_BrushUvRadiusHardness.w);
                float distanceToBrush = distance(input.uv, brushUv);
                float brushAlpha;

                if (outerRadius - innerRadius <= 1e-6)
                {
                    brushAlpha =
                        1.0 - step(outerRadius, distanceToBrush);
                }
                else
                {
                    brushAlpha = 1.0 - smoothstep(
                        innerRadius,
                        outerRadius,
                        distanceToBrush);
                }

                float mask = max(existingMask, brushAlpha);
                return half4(mask, mask, mask, mask);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

Shader "Four Corners/Wheel Evidence"
{
    Properties
    {
        [HideInInspector] _EvidenceTime("Evidence clock", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+15" }

        Pass
        {
            Name "Wheel Evidence"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _EvidenceTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 tint : COLOR;
                float2 uv : TEXCOORD0;
                float3 details : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 tint : COLOR;
                float2 uv : TEXCOORD0;
                float3 details : TEXCOORD1;
                float2 ground : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 position = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(position);
                output.tint = input.tint;
                output.uv = input.uv;
                output.details = input.details;
                output.ground = position.xz;
                return output;
            }

            float Noise(float2 p)
            {
                return frac(sin(dot(p, float2(13.37, 143.81))) * 43758.5453);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float kind = input.details.x;
                float slide = input.details.y;
                float2 uv = input.uv;
                float side = abs(uv.x * 2.0 - 1.0);
                float noise = Noise(floor(input.ground * 24.0));
                float edge = (1.0 - smoothstep(0.66, 1.0, side + (noise - 0.5) * 0.15));
                float caps = smoothstep(0.0, 0.2, uv.y) * (1.0 - smoothstep(0.8, 1.0, uv.y));
                float mask = edge * caps;
                float3 color = input.tint.rgb;

                if (kind < 0.5)
                {
                    float groove = Noise(float2(floor(uv.x * 12.0), 4.0));
                    mask *= 0.64 + 0.22 * groove + 0.14 * noise;
                }
                else if (kind < 1.5)
                {
                    float blade = frac(uv.x * 15.0 - uv.y * 2.5 + noise * 0.20);
                    mask *= lerp(0.48, 1.0, smoothstep(0.24, 0.6, blade));
                    color *= 0.68 + 0.55 * noise;
                }
                else if (kind < 2.5)
                {
                    float wall = smoothstep(0.43, 0.58, side) * (1.0 - smoothstep(0.61, 0.82, side));
                    float slick = smoothstep(0.54, 0.98, noise) * (1.0 - side);
                    color += wall * float3(0.11, 0.077, 0.044) + slick * float3(0.12, 0.10, 0.084);
                }
                else if (kind < 3.5)
                {
                    float tread = frac(uv.y * 4.0 + side * 0.62 + noise * 0.055);
                    float lug = smoothstep(0.12, 0.23, tread) * (1.0 - smoothstep(0.60, 0.77, tread));
                    float shoulder = smoothstep(0.68, 0.80, side);
                    mask *= lerp(0.22 + 0.76 * lug + 0.28 * shoulder, 0.68 + 0.32 * noise, slide);
                    color *= 0.76 + 0.28 * noise;
                }
                else
                {
                    float age = max(0.0, _EvidenceTime - input.details.z);
                    float remaining = saturate(1.0 - age / 1.46);
                    float radius = lerp(0.22, 0.86, saturate(age / 1.46));
                    float2 ripple = uv * 2.0 - 1.0;
                    float distance = length(ripple);
                    float ring = 1.0 - smoothstep(0.027, 0.105, abs(distance - radius));
                    float inner = 1.0 - smoothstep(0.018, 0.075, abs(distance - radius * 0.57));
                    mask = (ring + inner * 0.34) * remaining * (0.51 + 0.49 * noise);
                }

                return half4(color, input.tint.a * saturate(mask));
            }
            ENDHLSL
        }
    }
}

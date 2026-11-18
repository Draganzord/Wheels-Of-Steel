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
                float4 details : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 tint : COLOR;
                float2 uv : TEXCOORD0;
                float4 details : TEXCOORD1;
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
                float accent = input.details.w;
                float2 uv = input.uv;
                float side = abs(uv.x * 2.0 - 1.0);
                float noise = Noise(floor(input.ground * 24.0));
                float edge = (1.0 - smoothstep(0.66, 1.0, side + (noise - 0.5) * 0.15));
                float longitudinalEdge = min(uv.y, 1.0 - uv.y);
                float caps = smoothstep(0.0, 0.13, longitudinalEdge);
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
                    // A sliding tire cuts a broader, darker trough and pushes up larger clay shoulders.
                    float wall = smoothstep(0.48, 0.63, side) * (1.0 - smoothstep(0.73, 0.95, side));
                    float trough = 1.0 - smoothstep(0.24, 0.69, side);
                    float slick = smoothstep(0.52, 0.94, noise) * trough;
                    color *= 1.0 - trough * lerp(0.38, 0.61, accent);
                    color += wall * float3(0.14, 0.093, 0.053) * (0.62 + 0.38 * accent);
                    color += slick * float3(0.085, 0.072, 0.054);
                    mask *= lerp(0.76, 1.0, trough);
                }
                else if (kind < 3.5)
                {
                    float tread = frac(uv.y * 4.0 + side * 0.62 + noise * 0.055);
                    float lug = smoothstep(0.12, 0.23, tread) * (1.0 - smoothstep(0.60, 0.77, tread));
                    float shoulder = smoothstep(0.68, 0.80, side);
                    float dryPattern = 0.17 + 0.74 * lug + 0.22 * shoulder;
                    float wetPattern = 0.66 + 0.34 * lug;
                    float pattern = lerp(dryPattern, wetPattern, sqrt(accent));
                    mask *= lerp(pattern, 0.68 + 0.32 * noise, slide * (1.0 - 0.38 * accent));
                    color *= 0.74 + 0.30 * noise;
                    color += float3(0.040, 0.053, 0.055) * accent * smoothstep(0.69, 0.99, noise);
                }
                else
                {
                    float age = max(0.0, _EvidenceTime - input.details.z);
                    if (accent > 0.5)
                    {
                        float phase = saturate(age / 0.86);
                        float radius = lerp(0.18, 0.91, sqrt(phase));
                        float ringDistance = length(uv * 2.0 - 1.0);
                        float crest = 1.0 - smoothstep(0.031, 0.115, abs(ringDistance - radius));
                        float inner = 1.0 - smoothstep(0.023, 0.088, abs(ringDistance - radius * 0.60));
                        color = lerp(color, float3(0.91, 0.98, 0.95), 0.7);
                        mask = (crest + inner * 0.46) * (1.0 - phase) * (0.57 + 0.43 * noise);
                    }
                    else
                    {
                        // Paired foam lines open away from the leading point of each wheel's wake.
                        float phase = saturate(age / 1.38);
                        float open = saturate((0.88 - uv.y) / 0.78);
                        float wave = sin(uv.y * 27.0 + input.ground.y * 2.3) * 0.026;
                        float wing = 0.045 + open * lerp(0.55, 0.80, phase) + wave;
                        float crest = 1.0 - smoothstep(0.035, 0.16, abs(side - wing));
                        float turbulence = (1.0 - smoothstep(0.02, 0.42, side)) * (1.0 - uv.y);
                        float froth = (crest * 0.87 + turbulence * 0.32) * (0.54 + 0.46 * noise);
                        color = lerp(color, float3(0.90, 0.98, 0.96), crest * 0.72);
                        mask = froth * caps * (1.0 - phase) * (1.0 - phase * 0.3);
                    }
                }

                return half4(color, input.tint.a * saturate(mask));
            }
            ENDHLSL
        }
    }
}

Shader "FourCorners/Shallow Water"
{
    Properties
    {
        _Stream ("Stream", Color) = (0.11, 0.41, 0.43, 1)
        _Shallows ("Shallows", Color) = (0.30, 0.63, 0.54, 1)
        _Glint ("Glint", Color) = (0.58, 0.79, 0.70, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Stream;
            half4 _Shallows;
            half4 _Glint;
            CBUFFER_END

            struct Input
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float across : TEXCOORD1;
            };

            Varyings Vert(Input input)
            {
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                Varyings output;
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.across = input.uv.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float time = _Time.y;
                float drift = p.y - time * 0.85;
                float waves = sin(drift * 5.4 + sin(p.x * 1.8 - time) * 1.7)
                    + sin(p.x * 5.6 - drift * 3.3) * 0.65;
                float ripple = pow(saturate(waves * 0.45), 5.0);
                float edge = abs(input.across * 2.0 - 1.0);
                float shallows = smoothstep(0.20, 0.97, edge + 0.08 * sin(p.y * 1.9));
                half3 water = lerp(_Stream.rgb, _Shallows.rgb, shallows * 0.76 + 0.09 * sin(drift * 0.9 + p.x));
                float shore = smoothstep(0.87, 0.99, edge) * ripple;
                water = lerp(water, _Glint.rgb, ripple * 0.4 + shore * 0.45);
                return half4(water, 1);
            }
            ENDHLSL
        }
    }
}

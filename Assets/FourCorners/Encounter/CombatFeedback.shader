Shader "Four Corners/Combat Feedback"
{
    Properties
    {
        _Streak("Streak", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+28" }

        Pass
        {
            Name "Combat Feedback"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half _Streak;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 tint : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 tint : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.tint = input.tint;
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv * 2 - 1;
                float roundMask = 1 - smoothstep(0.05, 1, dot(uv, uv));
                float streak = 1 - smoothstep(0.05, 0.97, abs(uv.y));
                float mask = lerp(roundMask * roundMask, streak, _Streak);
                return half4(input.tint.rgb, input.tint.a * mask);
            }
            ENDHLSL
        }
    }
}

Shader "Four Corners/Disturbed Soil"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-5" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 displacedSoil : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 ground : TEXCOORD0;
                half2 displacedSoil : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.ground = world.xz;
                output.displacedSoil = input.displacedSoil.rg;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half bowl = input.displacedSoil.x;
                half rim = input.displacedSoil.y;
                half noise = frac(sin(dot(floor(input.ground * 18.0), float2(12.9898, 78.233))) * 43758.5453);
                half tint = saturate(bowl * 0.76 + rim * 0.48);
                clip(tint - 0.012);
                half3 darkClay = half3(0.071, 0.032, 0.015);
                half3 edgeClay = half3(0.27, 0.10, 0.04);
                half3 soil = lerp(darkClay, edgeClay, saturate(rim + noise * 0.32));
                return half4(soil, tint * (0.80 + noise * 0.20));
            }
            ENDHLSL
        }
    }
}

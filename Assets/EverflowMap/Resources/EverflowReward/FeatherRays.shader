Shader "EVERFLOW/Feather Rays"
{
    Properties
    {
        _BurstStart ("Burst start", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BurstStart;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 offset : TEXCOORD0;
            };

            float BurstProgress()
            {
                return saturate((_Time.y - _BurstStart) / 0.40);
            }

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings output;
                float3 center = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                float radius = _BurstStart < 0 ? 1.80 : lerp(1.80, 2.85, BurstProgress());
                output.positionCS = mul(UNITY_MATRIX_P, float4(center + float3(positionOS.xy * radius, 0.08), 1));
                output.offset = positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.offset);
                float angle = atan2(input.offset.y, input.offset.x + 0.00001);
                float ray = pow(saturate(cos(angle * 6.0 + 0.3)), 5.0);
                float reach = 0.87 + 0.11 * cos(angle * 3.0);
                float taper = smoothstep(0.10, 0.24, radius) * (1.0 - smoothstep(0.24, reach, radius));
                float halo = 1.0 - smoothstep(0.0, 0.58, radius);
                float intensity = _BurstStart < 0 ? 0.84 + 0.16 * sin(_Time.y * 3.14159)
                    : 1.65 * (1.0 - BurstProgress());
                half3 gold = lerp(half3(1.65, 0.88, 0.28), half3(2.4, 1.9, 1.1), halo);
                return half4(gold, saturate((ray * taper * 0.46 + halo * 0.24) * intensity));
            }
            ENDHLSL
        }
    }
}

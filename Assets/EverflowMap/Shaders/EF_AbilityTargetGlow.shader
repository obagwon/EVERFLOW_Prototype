Shader "EVERFLOW/Ability Target Glow"
{
    Properties
    {
        _GlowColor("Ability color",Color)=(.59,1,.25,1)
        _Strength("Strength",Float)=.5
        _Pulse("Pulse",Float)=1
        [HideInInspector] _WaveProgress("Wave progress",Float)=-1
        [HideInInspector] _WaveOrigin("Wave origin and inverse radius",Vector)=(0,0,0,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent"}
        Pass
        {
            Name "Visible ability surface"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float4 _WaveOrigin;
                float _Strength,_Pulse,_WaveProgress;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
            V vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 frag(V i):SV_Target
            {
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),SafeNormalize(GetWorldSpaceViewDir(i.world))))),2);
                float alpha = (.07 + rim * .74) * _Strength * _Pulse;
                if (_WaveProgress >= 0)
                {
                    float distance01 = distance(i.world, _WaveOrigin.xyz) * _WaveOrigin.w;
                    float front = lerp(.04, 1.18, _WaveProgress);
                    float band = 1 - smoothstep(.04, .18, abs(distance01 - front));
                    alpha = band * (1 - smoothstep(.65, 1, _WaveProgress)) * _Strength;
                }
                return half4(_GlowColor.rgb*(1+rim*.5),alpha);
            }
            ENDHLSL
        }
    }
}

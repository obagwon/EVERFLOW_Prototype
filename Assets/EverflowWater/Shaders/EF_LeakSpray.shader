Shader "EVERFLOW/Water/Leak Spray"
{
    SubShader
    {
        Tags {"Queue"="Transparent+1" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(V i):SV_Target{float d=length((i.uv-.5)*2);return half4(i.color.rgb,i.color.a*(1-smoothstep(.2,1,d)));}
            ENDHLSL
        }
    }
}

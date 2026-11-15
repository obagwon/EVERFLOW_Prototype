Shader "EVERFLOW/Water/Leak Prototype"
{
    Properties
    {
        _WaterTime("Local time",Float)=0
        _Wet("Supply",Float)=1
        _NormalMap("Normal",2D)="bump"{}
        _Tint("Water tint",Color)=(.10,.36,.37,1)
        _Foam("Foam tint",Color)=(.68,.85,.81,1)
    }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                float _WaterTime,_Wet; float4 _Tint,_Foam;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct V {float4 pos:SV_POSITION;float3 ws:TEXCOORD0;float3 n:TEXCOORD1;float3 t:TEXCOORD2;float2 uv:TEXCOORD3;float4 color:COLOR;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            V vert(A i)
            {
                V o;float3 ws=TransformObjectToWorld(i.positionOS.xyz);
                float fall=i.color.r;
                // Movement is pinned at the upstream join and follows one continuous UV field.
                float ripple=sin(i.uv.y*6-_WaterTime*5+i.uv.x*4)*.008;
                ws+=TransformObjectToWorldNormal(i.normalOS)*ripple*i.color.b;
                ws.x+=sin(i.uv.y*8-_WaterTime*4)*.022*fall*fall;
                o.ws=ws;o.pos=TransformWorldToHClip(ws);o.n=TransformObjectToWorldNormal(i.normalOS);
                o.t=TransformObjectToWorldDir(i.tangentOS.xyz);o.uv=i.uv;o.color=i.color;return o;
            }
            half4 frag(V i):SV_Target
            {
                clip(_Wet-.5);
                float t=_WaterTime;
                float2 flow=float2(i.uv.x,i.uv.y-t*.72);
                float fall=i.color.r,impact=i.color.g;
                float a=noise(flow*float2(4,10));
                float b=noise(flow*float2(11,23)+5.3);
                float streak=smoothstep(.51,.79,a*.65+b*.35);
                float3 n0=normalize(i.n),tx=normalize(i.t),ty=normalize(cross(n0,tx));
                float3 nm=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,flow*.65));
                float3 n=normalize(n0+(tx*nm.x+ty*nm.y)*.28);
                float3 view=SafeNormalize(GetWorldSpaceViewDir(i.ws));
                float fres=pow(1-abs(dot(n,view)),3);
                float2 screen=GetNormalizedScreenSpaceUV(i.pos);
                half3 back=SampleSceneColor(clamp(screen+nm.xy*.002,.002,.998));
                // Same body colour throughout; slope changes detail gradually, not the material.
                half3 col=lerp(back,_Tint.rgb,.72+fall*.1);
                col+=float3(.07,.12,.12)*fres;
                Light sun=GetMainLight();
                float spec=pow(saturate(dot(n,SafeNormalize(view+sun.direction))),48);
                col+=sun.color*spec*.16;
                float foam=streak*lerp(.12,.3,fall);
                // Impact turbulence and radial expanding ripples on the downstream surface.
                float2 d=(i.ws.xz-float2(-35,-29.62))*float2(1,.7);
                float radius=length(d);
                float ring=pow(saturate(sin(radius*18-t*5)),10)*exp(-radius*1.8);
                foam+=impact*(.38+.35*a)+ring*(1-fall)*.3;
                col=lerp(col,_Foam.rgb,saturate(foam));
                float edge=i.color.a;
                // Broken sheet edges instead of a perfectly straight translucent curtain.
                edge*=lerp(1,smoothstep(.02,.12,edge+noise(float2(i.uv.y*5-t*2,3))*.08),fall);
                float alpha=(.79+fres*.12+impact*.08)*edge;
                return half4(col,saturate(alpha));
            }
            ENDHLSL
        }
    }
}

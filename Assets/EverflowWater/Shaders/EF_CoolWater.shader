// EVERFLOW derivative of EmaceArt EA_CoolWater, vendor source left untouched.
// All animation uses controller-local time + continuous integrated world offset.
Shader "EVERFLOW/Water/CoolWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color",Color)=(.12,.42,.43,.70)
        _DeepColor ("Deep Color",Color)=(.035,.17,.22,.94)
        _WaterDepth ("Depth Tint Distance (m)",Float)=2.5
        _ColorBands ("Depth Bands",Range(1,8))=5
        _ShoreGlowColor ("Shore Tint",Color)=(.40,.69,.64,1)
        _ShoreGlowWidth ("Shore Width (m)",Range(.01,.5))=.12
        _NormalMap ("Normal Map",2D)="bump"{}
        _NormalTiling ("Normal Repeats Per Metre",Range(.05,2))=.5
        _NormalStrength ("Normal Strength",Range(0,2))=.45
        _WaveHeight ("World Wave Height (m)",Range(0,.12))=.018
        _WaveSpeed ("Ripple Rate",Range(0,3))=.6
        _FoamTex ("Foam/Caustic Texture",2D)="white"{}
        [KeywordEnum(Texture,Cloudy,Blobs,Scatter,Cellular)] _FoamStyle ("Foam Style",Float)=1
        _FoamColor ("Foam Color",Color)=(.72,.86,.79,.65)
        _FoamTiling ("Foam Repeats Per Metre",Range(.05,2))=.8
        _FoamCutoff ("Foam Threshold",Range(0,1))=.64
        _FoamDepth ("Contact Foam Depth (m)",Range(.01,.5))=.16
        _FoamAmount ("Surface Foam",Range(0,1))=.24
        _CausticsStrength ("Caustic Strength",Range(0,.5))=.12
        _CausticsScale ("Caustic Repeats Per Metre",Range(.1,2))=.65
        _CausticsColor ("Caustic Tint",Color)=(.55,.8,.65,1)
        _RefractionStr ("Refraction",Range(0,.03))=.005
        _SpecularColor ("Specular Color",Color)=(.8,.86,.75,1)
        _SpecularPower ("Specular Power",Range(10,100))=65
        _SpecularCutoff ("Specular Softness",Range(.01,.8))=.3
        _SpecularScale ("Specular Strength",Range(0,.8))=.12
        _FresnelPower ("Fresnel Power",Range(.1,5))=3
        _IceColor ("Ice Tint",Color)=(.46,.73,.80,.95)
        [HideInInspector] _FlowOffset ("Integrated World Offset",Vector)=(0,0,0,0)
        [HideInInspector] _SurfaceRight ("Projection U",Vector)=(1,0,0,0)
        [HideInInspector] _SurfaceForward ("Projection V",Vector)=(0,0,1,0)
        [HideInInspector] _ArrivalDirection ("Arrival Direction",Vector)=(0,0,1,0)
        [HideInInspector] _SegmentCenter ("Segment Center",Vector)=(0,0,0,0)
        [HideInInspector] _SegmentLength ("Segment Length",Float)=1
        [HideInInspector] _WaterTime ("Local Water Time",Float)=0
        [HideInInspector] _Frozen ("Ice",Float)=0
        [HideInInspector] _Wet ("Supplied",Float)=1
        [HideInInspector] _Arrival ("Arrival Front",Float)=1
        [HideInInspector] _Vertical ("Waterfall Projection",Float)=0
        [HideInInspector] _FoamOnly ("Foam Geometry",Float)=0
        [HideInInspector] _PathFlow ("Distance UV ribbon",Float)=0
        [HideInInspector] _PathSpeed ("Ribbon speed",Float)=1.2
    }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Name "UniversalForward"
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #pragma shader_feature_local _FOAMSTYLE_TEXTURE _FOAMSTYLE_CLOUDY _FOAMSTYLE_BLOBS _FOAMSTYLE_SCATTER _FOAMSTYLE_CELLULAR
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor,_DeepColor,_ShoreGlowColor,_FoamColor,_CausticsColor,_SpecularColor,_IceColor;
                float4 _FlowOffset,_SurfaceRight,_SurfaceForward,_ArrivalDirection,_SegmentCenter;
                float _WaterDepth,_ColorBands,_ShoreGlowWidth,_NormalTiling,_NormalStrength,_WaveHeight,_WaveSpeed;
                float _FoamTiling,_FoamCutoff,_FoamDepth,_FoamAmount,_CausticsStrength,_CausticsScale;
                float _RefractionStr,_SpecularPower,_SpecularCutoff,_SpecularScale,_FresnelPower;
                float _SegmentLength,_WaterTime,_Frozen,_Wet,_Arrival,_Vertical,_FoamOnly,_PathFlow,_PathSpeed;
            CBUFFER_END
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            struct Attributes {float4 positionOS:POSITION;float4 vertColor:COLOR;float2 uv:TEXCOORD0;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float4 screenPos:TEXCOORD0;float3 positionWS:TEXCOORD1;float4 vertColor:TEXCOORD2;float2 pathUV:TEXCOORD3;float3 normalWS:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
            // ── Triple-sine UV warp ───────────────────────────────────────────
            float2 DistortUV(float2 uv, float amount, float t)
            {
                float a = amount * 0.01;
                float b = amount * 0.12;
                uv.y += a * (sin(uv.x * 3.5 + t * 0.35) + sin(uv.x * 4.8 + t * 1.05) + sin(uv.x * 7.3 + t * 0.45)) / 3.0;
                uv.x += b * (sin(uv.y * 4.0 + t * 0.50) + sin(uv.y * 6.8 + t * 0.75) + sin(uv.y * 11.3 + t * 0.20)) / 3.0;
                uv.y += b * (sin(uv.x * 4.2 + t * 0.64) + sin(uv.x * 6.3 + t * 1.65) + sin(uv.x * 8.2 + t * 0.45)) / 3.0;
                return uv;
            }

            // ── Photoshop Overlay blend (branch-free per-component) ───────────
            half3 OverlayBlend(half3 base, half3 blend)
            {
                half3 lo = 2.0h * base * blend;
                half3 hi = 1.0h - 2.0h * (1.0h - base) * (1.0h - blend);
                return lerp(lo, hi, step(0.5h, base));
            }

            // ── Reconstruct world position from screen UV + raw depth ─────────
            float3 ReconstructWorldPos(float2 uv, float depth)
            {
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            // ── Procedural foam helpers ───────────────────────────────────────
            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5);
                float b = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5);
                float c = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5);
                float d = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float WorleyMinDist(float2 p)
            {
                float2 pi = floor(p);
                float2 pf = frac(p);
                float minD = 1.0;
                for (int wx = -1; wx <= 1; wx++)
                {
                    for (int wy = -1; wy <= 1; wy++)
                    {
                        float2 g = float2(wx, wy);
                        float2 o = float2(
                            frac(sin(dot(pi + g, float2(127.1, 311.7))) * 43758.5),
                            frac(sin(dot(pi + g, float2(269.5, 183.3))) * 12345.6)
                        );
                        minD = min(minD, length(g + o - pf));
                    }
                }
                return minD;
            }

            // Cloudy — soft low-frequency wisps (FBM), animated by t
            float FoamCloudy(float2 p, float t)
            {
                float2 dA = float2( t * 0.45,  t * 0.30);
                float2 dB = float2(-t * 0.32,  t * 0.50);
                float v = VNoise(p * 0.8 + dA) * 0.55
                        + VNoise(p * 1.7 + dB + 3.1) * 0.30
                        + VNoise(p * 3.3 + dA * 0.6 + 7.4) * 0.15;
                return v; // EVERFLOW: threshold once, in WaterFrag
            }

            // Blobs — large organic shapes, hard binary toon edge
            float FoamBlobs(float2 p, float t)
            {
                float2 d = float2(t * 0.18, t * 0.12);
                float v = VNoise(p * 0.45 + d) * 0.60
                        + VNoise(p * 0.95 - d * 0.6 + 2.7) * 0.40;
                return smoothstep(0.47, 0.50, v);     // hard edge
            }

            // Scatter — small high-freq specks
            float FoamScatter(float2 p, float t)
            {
                float2 d = float2(t * 0.85, t * 0.65);
                float v = VNoise(p * 4.5 + d)
                        * (0.6 + 0.4 * VNoise(p * 0.7 - d * 0.4));
                return smoothstep(0.64, 0.67, v);     // hard speckles
            }

            // Cellular — Worley voronoi cells, animated scroll
            float FoamCellular(float2 p, float t)
            {
                float2 d = float2(t * 0.25, t * 0.18);
                float dist = WorleyMinDist(p * 1.2 + d);
                return smoothstep(0.42, 0.36, dist);  // sharp cell interior
            }

            // ── Caustics — organic tongue/streak refraction lines ─────────────
            // Real pool caustics look like irregular bright tongues, NOT a regular
            // diamond grid. We distort the sample position with low-freq noise
            // before computing the wavefront interference — straight ridges bend
            // into organic streaks. 1 - abs(sum) gives bright RIDGES where waves
            // cross zero; pow() sharpens into thin tongues with continuous coverage.
            float Caustic(float2 p, float t)
            {
                float ct = t * 0.55;
                // Low-freq distortion → bends straight ridges into organic tongues
                float2 dist = float2(
                    VNoise(p * 0.35 + ct * 0.08),
                    VNoise(p * 0.35 + ct * 0.10 + 5.1)
                ) - 0.5;
                float2 pd = p + dist * 0.7;
                // Four wave fronts at irrational angles → non-repeating interference
                float w1 = sin(dot(pd, float2( 1.000,  0.000)) * 2.0 + ct * 0.60);
                float w2 = sin(dot(pd, float2( 0.500,  0.866)) * 2.2 + ct * 0.45);
                float w3 = sin(dot(pd, float2(-0.500,  0.866)) * 1.8 + ct * 0.55);
                float w4 = sin(dot(pd, float2( 0.707,  0.707)) * 2.7 + ct * 0.40);
                float v     = (w1 + w2 + w3 + w4) * 0.25;     // -1 … +1
                float ridge = 1.0 - abs(v);                   // 0 (valley) … 1 (ridge)
                ridge       = pow(ridge, 4.0);                // thin tongues
                // Soft flicker for shimmer (not high-freq speckle)
                float flicker = 0.75 + 0.25 * sin(ct * 3.0 + p.x * 1.3 + p.y * 0.9);
                return saturate(ridge * flicker * 1.4);
            }

            // Two-tap union blend — two caustic networks at different scale/phase
            // overlap into a richer organic pattern. max() preserves brightness on
            // both networks rather than carving (min() would erase most of it).
            float CausticBlend(float2 p, float t)
            {
                float cA = Caustic(p,              t);
                float cB = Caustic(p * 1.43 + 2.7, t * 0.78);
                return max(cA, cB);
            }

            float SampleFoamStyled(float2 uv, float t, TEXTURE2D_PARAM(tex, smp))
            {
                #if defined(_FOAMSTYLE_CLOUDY)
                    return FoamCloudy(uv, t);
                #elif defined(_FOAMSTYLE_BLOBS)
                    return FoamBlobs(uv, t);
                #elif defined(_FOAMSTYLE_SCATTER)
                    return FoamScatter(uv, t);
                #elif defined(_FOAMSTYLE_CELLULAR)
                    return FoamCellular(uv, t);
                #else  // _FOAMSTYLE_TEXTURE
                    return SAMPLE_TEXTURE2D(tex, smp, uv).r;
                #endif
            }


            float2 Project(float3 p) {return float2(dot(p,_SurfaceRight.xyz),dot(p,_SurfaceForward.xyz));}
            Varyings WaterVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 ws=TransformObjectToWorld(IN.positionOS.xyz);
                float2 p=Project(ws-_FlowOffset.xyz);
                // Metres in world space: FBX local scale cannot amplify displacement.
                ws.y+=(sin(p.x*2+_WaterTime*_WaveSpeed)*.5+sin(p.y*1.7-_WaterTime*_WaveSpeed*.6)*.5)*_WaveHeight;
                // Small travelling ripples bend the sheet; taper to zero at joins.
                float pathRipple=sin(IN.uv.y*4-_WaterTime*_PathSpeed*4+IN.uv.x*3)*.025;
                ws+=TransformObjectToWorldNormal(IN.normalOS)*pathRipple*_PathFlow*IN.vertColor.a*(1-abs(TransformObjectToWorldNormal(IN.normalOS).y));
                OUT.positionWS=ws;OUT.positionCS=TransformWorldToHClip(ws);
                OUT.screenPos=ComputeScreenPos(OUT.positionCS);OUT.vertColor=IN.vertColor;
                OUT.pathUV=IN.uv;OUT.normalWS=TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 WaterFrag(Varyings IN):SV_Target
            {
                clip(_Wet-.5);
                float3 posWS=IN.positionWS;
                float progress=dot(posWS-_SegmentCenter.xyz,_ArrivalDirection.xyz)/max(.1,_SegmentLength)+.5;
                clip(_Arrival>=.999?1:_Arrival-progress);
                float2 uv=GetNormalizedScreenSpaceUV(IN.positionCS);
                float rawDepth=SampleSceneDepth(uv);
                float3 sceneWS=ReconstructWorldPos(uv,rawDepth);
                float depthDiff=max(0,posWS.y-sceneWS.y);
                // Sky/far plane gets a finite deep tint. Falls use sheet thickness,
                // never the horizontal depth rule or screen-edge tap foam.
                depthDiff=min(depthDiff,_WaterDepth*4);
                float vertical=lerp(_Vertical,1-abs(normalize(IN.normalWS).y),_PathFlow);
                depthDiff=lerp(depthDiff,.7,vertical);
                float depthT=saturate(depthDiff/max(.01,_WaterDepth));
                float2 p=Project(posWS-_FlowOffset.xyz);
                // Falls use stretched vertical streaks rather than horizontal pool blobs.
                p*=lerp(float2(1,1),float2(1.5,.24),_Vertical);
                p=lerp(p,float2(IN.pathUV.x,IN.pathUV.y-_WaterTime*_PathSpeed),_PathFlow);
                float2 nUV=p*_NormalTiling;
                // Bounded ripples also animate standing pools; their small oscillation
                // cannot overpower the downstream advection of channel foam.
                nUV+=float2(sin(_WaterTime*.7+p.y),cos(_WaterTime*.6+p.x))*.025;
                float3 a=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,nUV));
                float3 b=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,nUV*.73+2.3));
                float3 tn=normalize(a+b);
                float3 baseNormal=normalize(cross(_SurfaceForward.xyz,_SurfaceRight.xyz));
                baseNormal=normalize(lerp(baseNormal,IN.normalWS,_PathFlow));
                float3 normal=normalize(baseNormal+(_SurfaceRight.xyz*tn.x+_SurfaceForward.xyz*tn.y)*_NormalStrength);
                float bands=max(1,_ColorBands);
                half3 tint=lerp(_ShallowColor.rgb,_DeepColor.rgb,floor(depthT*bands)/bands);
                float shore=1-smoothstep(0,max(.01,_ShoreGlowWidth),depthDiff);
                tint=lerp(tint,_ShoreGlowColor.rgb,shore*.28);
                float2 refractUV=clamp(uv+tn.xy*_RefractionStr*(1-_Vertical)*(1-_Frozen),.002,.998);
                half3 refracted=SampleSceneColor(refractUV);
                // Limit background influence; a dark underwater wall is still tinted water.
                half3 col=lerp(refracted,tint,lerp(.55,.92,depthT));
                float2 cUV=p*_CausticsScale;
                float c=CausticBlend(cUV,0)*SAMPLE_TEXTURE2D(_FoamTex,sampler_FoamTex,cUV*.7).r;
                col+=_CausticsColor.rgb*c*_CausticsStrength*(1-depthT)*(1-_Vertical);
                Light light=GetMainLight();
                float3 view=SafeNormalize(GetWorldSpaceViewDir(posWS));
                float ndh=saturate(dot(normal,SafeNormalize(view+light.direction)));
                float spec=smoothstep(.5-_SpecularCutoff*.3,.5+_SpecularCutoff*.3,pow(ndh,_SpecularPower));
                col+=_SpecularColor.rgb*light.color*spec*_SpecularScale*(1-_Frozen*.8);
                float fresnel=pow(1-saturate(abs(dot(normal,view))),_FresnelPower);
                col=lerp(col,_ShoreGlowColor.rgb,fresnel*.12);
                float2 foamUV=DistortUV(p*_FoamTiling,.2,0);
                // No independent foam drift: every octave is advected by the same offset.
                float noise=SampleFoamStyled(foamUV,0,TEXTURE2D_ARGS(_FoamTex,sampler_FoamTex));
                float flecks=smoothstep(_FoamCutoff,min(.99,_FoamCutoff+.2),noise);
                float streak=pow(1-abs(sin(p.x*5+VNoise(p*float2(1,.4))*2)),5);
                streak=lerp(streak,streak*(.55+.45*VNoise(float2(p.x*2,p.y*2.5)))+.12*flecks,_PathFlow);
                flecks=lerp(flecks,streak,_Vertical);
                float contact=(1-smoothstep(0,max(.01,_FoamDepth),depthDiff))*(1-_Vertical);
                float foam=saturate(max(contact*.5,flecks*_FoamAmount));
                foam=lerp(foam,(.35+.65*smoothstep(.35,.65,noise))*_FoamAmount,_FoamOnly);
                foam*=1-_Frozen;
                col=lerp(col,_FoamColor.rgb,foam*_FoamColor.a);
                float icePattern=smoothstep(.76,.9,VNoise(p*1.8));
                half3 ice=lerp(_IceColor.rgb,_IceColor.rgb*1.2,icePattern*.35);
                col=lerp(col,ice,_Frozen*.86);
                float alpha=lerp(_ShallowColor.a,_DeepColor.a,depthT);
                if(_PathFlow>.5)
                {
                    // Soft banks and submerged tail, without a rectangular bottom cap.
                    alpha*=IN.vertColor.a;
                    float impact=IN.vertColor.r;
                    col=lerp(col,_FoamColor.rgb,impact*(.3+.3*noise));
                }
                alpha=lerp(alpha,.96,_Frozen);
                if(_FoamOnly>.5){col=_FoamColor.rgb;alpha=foam*_FoamColor.a*.8*IN.vertColor.a;}
                return half4(col,saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}

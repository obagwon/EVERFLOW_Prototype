Shader "Custom/EmaceArt/URP_ArtWater"
{
    Properties
    {
        _WaterColor       ("Color Water",       Color)      = (0.13, 0.68, 0.63, 0.72)
        _FoamColor        ("Color Foam",        Color)      = (0.95, 0.97, 1.00, 1.00)
        _Depth            ("Depth",             Float)      = 2.0
        _OpacityIntensity ("Opacity Intensity", Range(0,1)) = 0.50
        _SizeFoam         ("Size Foam",         Range(0,1)) = 0.14
        _Gloss            ("Gloss",             Range(0,1)) = 0.11
        _Specular         ("Specular",          Range(0,1)) = 0.65
        _NoiseTex         ("Noise",             2D)         = "white" {}
        _Speed            ("Speed",             Float)      = 0.20
        _NoiseIntensity   ("Noise Intensity",   Range(0,1)) = 0.23
        _ScaleNoise       ("Scale Noise",       Float)      = 0.35

        [Space(6)]
        [Header(Style)]
        [Toggle] _CelEnabled  ("  Cel Shading",     Float)        = 1
        _DepthColor           ("  Color Bands",      Color)        = (1.00, 1.00, 1.00, 1.00)
        _CelSteps             ("  Cel Steps",        Range(1,8))   = 4
        _CelBias              ("  Shore Zone (m)",   Range(0.02,1.0)) = 0.15

        [Space(6)]
        [Header(Reflections)]
        [Toggle] _ReflEnabled ("  Enable",           Float)        = 0
        _ReflScale            ("  Scale",            Range(0.1,6)) = 2.0
        _ReflDensity          ("  Density",          Range(0,1))   = 0.55
        _ReflBrightness       ("  Brightness",       Range(0,1))   = 0.70
        _ReflSpeed            ("  Speed",            Float)        = 0.15
        _ReflSharpness        ("  Sharpness",        Range(1,32))  = 12.0

        [Space(6)]
        [Header(Caustics)]
        [Toggle] _CausticEnabled ("  Enable",        Float)        = 1
        _CausticStrength      ("  Strength",         Range(0,0.5)) = 0.12
        _CausticScale         ("  Scale",            Range(0.1,6)) = 1.0
        _CausticSharpness     ("  Sharpness",        Range(1,20))  = 8.0

        [Space(6)]
        [Header(Waves)]
        _WaveHeight       ("  Height",          Float)      = 0.05
        _WaveSpeed        ("  Speed",           Float)      = 0.8
        _WaveScale        ("  Scale",           Float)      = 1.0

        [Space(6)]
        [Header(Normal Map)]
        [Normal] _NormalMap   ("  Normal Map",  2D)           = "bump" {}
        _NormalStrength       ("  Strength",    Range(0,1))   = 0.30
        _NormalScale          ("  Scale",       Range(0.1,4)) = 1.0
        _NormalSpeed          ("  Speed",       Float)        = 0.08

        [Space(6)]
        [Header(Low Poly)]
        [Toggle] _PolyEnabled  ("  Enable",    Float)         = 1
        _PolyStrength          ("  Strength",  Range(0,0.15)) = 0.025
        _PolyDensity           ("  Density",   Range(1,40))   = 10.0
        _PolySpeed             ("  Speed",     Float)         = 0.4

        [HideInInspector] _Mem_Cel_Steps  ("", Float) = -1
        [HideInInspector] _Mem_Cel_Bias   ("", Float) = -1
        [HideInInspector] _Mem_Grad_Steps ("", Float) = -1
        [HideInInspector] _Mem_Grad_Bias  ("", Float) = -1

        [Space(6)]
        [Header(SWAMP)]
        [Toggle(_SWAMP)]  _SwampOn        ("  Enable Swamp",  Float)      = 0
        _SwampWaterColor  ("  Color Water", Color)      = (0.28, 0.40, 0.14, 0.90)
        _SwampDepthColor  ("  Color Depth", Color)      = (0.10, 0.20, 0.08, 1.00)
        _SwampFoamColor   ("  Color Foam",  Color)      = (0.62, 0.65, 0.42, 1.00)
        _SwampMurkColor   ("  Murk Color",  Color)      = (0.20, 0.28, 0.10, 1.00)
        _SwampMurkiness   ("  Murkiness",   Range(0,1)) = 0.40
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma shader_feature_local _SWAMP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _WaterColor;
                half4  _DepthColor;
                half4  _FoamColor;
                float  _Depth;
                float  _OpacityIntensity;
                float  _SizeFoam;
                float  _Gloss;
                float  _Specular;
                float4 _NoiseTex_ST;
                float  _Speed;
                float  _NoiseIntensity;
                float  _ScaleNoise;
                float  _NormalStrength;
                float  _NormalScale;
                float  _NormalSpeed;
                float  _CelEnabled;
                float  _CelSteps;
                float  _CelBias;
                float  _ReflEnabled;
                float  _ReflScale;
                float  _ReflDensity;
                float  _ReflBrightness;
                float  _ReflSpeed;
                float  _ReflSharpness;
                float  _CausticEnabled;
                float  _CausticStrength;
                float  _CausticScale;
                float  _CausticSharpness;
                float  _WaveHeight;
                float  _WaveSpeed;
                float  _WaveScale;
                float  _PolyEnabled;
                float  _PolyStrength;
                float  _PolyDensity;
                float  _PolySpeed;
                float  _SwampOn;
                half4  _SwampWaterColor;
                half4  _SwampDepthColor;
                half4  _SwampFoamColor;
                half4  _SwampMurkColor;
                float  _SwampMurkiness;
                float  _Mem_Cel_Steps;
                float  _Mem_Cel_Bias;
                float  _Mem_Grad_Steps;
                float  _Mem_Grad_Bias;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Value noise 0..1 ──────────────────────────────────────────────
            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float va = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5);
                float vb = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5);
                float vc = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5);
                float vd = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5);
                return lerp(lerp(va, vb, u.x), lerp(vc, vd, u.x), u.y);
            }

            // ── Worley (cellular) noise — returns (f1, f2, cellID) ────────────
            float3 Worley(float2 p)
            {
                float2 pi = floor(p);
                float2 pf = frac(p);
                float wf1  = 8.0;
                float wf2  = 8.0;
                float wcid = 0.0;
                for (int wx = -1; wx <= 1; wx++)
                {
                    for (int wy = -1; wy <= 1; wy++)
                    {
                        float2 g  = float2(wx, wy);
                        float2 o  = float2(
                            frac(sin(dot(pi + g, float2(127.1, 311.7))) * 43758.5),
                            frac(sin(dot(pi + g, float2(269.5, 183.3))) * 12345.6)
                        );
                        float wd = length(g + o - pf);
                        if (wd < wf1)
                        {
                            wf2  = wf1;
                            wf1  = wd;
                            wcid = frac(sin(dot(pi + g, float2(41.3, 289.1))) * 7654.3);
                        }
                        else if (wd < wf2)
                        {
                            wf2 = wd;
                        }
                    }
                }
                return float3(wf1, wf2, wcid);
            }

            // ── Reconstruct world position from screen UV + raw depth ─────────
            float3 ReconstructWorldPos(float2 suv, float rawDepth)
            {
                float4 posCS = float4(suv * 2.0 - 1.0, rawDepth, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    posCS.y = -posCS.y;
                #endif
                float4 posWS4 = mul(UNITY_MATRIX_I_VP, posCS);
                return posWS4.xyz / posWS4.w;
            }

            // ── Depth difference — world-space vertical Y (camera-independent) ──
            // Zwraca metry pionowe woda→dno; eliminuje "taniec" pasów przy ruchu kamery.
            float SampleDiff(float2 suv, float waterY)
            {
                float  raw  = SampleSceneDepth(suv);
                float3 wpos = ReconstructWorldPos(suv, raw);
                if (wpos.y > waterY + 0.08)
                    return 99999.0;
                return max(0.0, waterY - wpos.y);   // pionowe metry, nie eye-depth
            }

            // ── Wave displacement ─────────────────────────────────────────────
            float WaveY(float2 xz, float t)
            {
                float sc = _WaveScale;
                return (  sin(xz.x * sc         + t * _WaveSpeed)       * 0.6
                        + sin(xz.y * sc * 1.3   + t * _WaveSpeed * 0.7) * 0.4
                       ) * _WaveHeight;
            }

            // ── Vertex ────────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 pos = IN.positionOS.xyz;
                pos.y += WaveY(pos.xz, _Time.y);

                // Low-poly faceted displacement — hash per grid cell, stable per vertex
                // _PolyDensity controls grid resolution: higher = more, smaller triangles
                if (_PolyEnabled > 0.5)
                {
                    float2 pGrid = floor(pos.xz * _PolyDensity);
                    float  ph    = frac(sin(dot(pGrid, float2(127.1, 311.7))) * 43758.5);
                    pos.y += (ph - 0.5) * _PolyStrength
                           * (0.8 + 0.2 * sin(_Time.y * _PolySpeed + ph * 6.283));
                }

                VertexPositionInputs vi = GetVertexPositionInputs(pos);
                OUT.positionCS = vi.positionCS;
                OUT.screenPos  = ComputeScreenPos(vi.positionCS);
                OUT.positionWS = vi.positionWS;
                return OUT;
            }

            // ── Fragment ──────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;
                float  wY = IN.positionWS.y;   // world-space Y powierzchni wody

                // Depth neighbourhood (5-tap cross for foam around objects)
                // Głębokość w metrach pionowych Y — stabilna przy ruchu kamery
                float2 ts  = 1.0 / _ScreenParams.xy;
                float  sp  = 4.0;
                float  d0  = SampleDiff(uv,                          wY);
                float  d1  = SampleDiff(uv + ts * float2( sp,  0.0), wY);
                float  d2  = SampleDiff(uv + ts * float2(-sp,  0.0), wY);
                float  d3  = SampleDiff(uv + ts * float2( 0.0, sp),  wY);
                float  d4  = SampleDiff(uv + ts * float2( 0.0,-sp),  wY);
                float  minDiff   = min(min(d0, d1), min(min(d2, d3), d4));

                // ── ABOVE-WATER DISCARD ────────────────────────────────────────
                // SampleDiff zwraca sentinel 99999 dla pikseli, gdzie scena pod
                // wodnym meshem jest POWYŻEJ poziomu wody (np. skały wystające,
                // mesh wody większy niż faktyczny basen). Bez tego shader malował
                // tam czysty cWater (cyjan) → cyjanowe obwódki dookoła skał, które
                // wyglądały jak "głębia po brzegach". Discard ukrywa te piksele —
                // widoczna jest tylko prawdziwa tafla wody.
                if (d0 > _Depth * 50.0) discard;

                // d0  → rzeczywista głębokość piksela (gradient kolorów, cel, alpha)
                // minDiff → minimum sąsiedztwa, wyłącznie do wykrywania piany przy obiektach
                float  depthN    = saturate(d0       / max(0.001, _Depth));
                float  depthFoam = saturate(minDiff  / max(0.001, _Depth));

                // Cel / gradient shading — ODWRÓCONA logika:
                // depthCel = 1 PRZY BRZEGU → kolor cDepth (Color Bands, np. biały)
                // depthCel = 0 W GŁĘBI    → kolor cWater (główny kolor zbiornika)
                // _CelBias = szerokość STREFY BRZEGOWEJ w metrach. Poza nią → czysty cWater.
                // _CelSteps = liczba widocznych pasów wewnątrz strefy brzegowej.
                // Każdy pas = shoreZone/celSteps metrów. Pasy przylegają do brzegu, nie do centrum.
                float celSteps = max(1.0, _CelSteps);
                float depthCel;
                if (_CelEnabled > 0.5)
                {
                    float shoreM = max(0.001, _CelBias);
                    float t      = saturate(d0 / shoreM);          // 0 przy brzegu, 1 na krawędzi strefy
                    float stepN  = floor(t * celSteps);            // który krok [0..celSteps]
                    depthCel     = 1.0 - stepN / celSteps;         // 1 przy brzegu → 0 za strefą
                }
                else
                {
                    depthCel = 1.0 - depthN;                       // płynny gradient: brzeg=cDepth, głębia=cWater
                }

                // Swamp modifiers
                float swampMurk    = 0.0;
                float fresnelScale = 1.0;
                float causticScale = 1.0;
                #if defined(_SWAMP)
                    swampMurk    = _SwampMurkiness;
                    depthCel     = lerp(depthCel, 1.0 - depthN, swampMurk * 0.75);  // murk → gładki gradient
                    causticScale = saturate(1.0 - swampMurk * 0.90);
                    fresnelScale = 1.0 + swampMurk * 0.65;
                #endif

                // Colour set
                #if defined(_SWAMP)
                    half4 cWater = _SwampWaterColor;
                    half4 cDepth = _SwampDepthColor;
                    half4 cFoam  = _SwampFoamColor;
                #else
                    half4 cWater = _WaterColor;
                    half4 cDepth = _DepthColor;
                    half4 cFoam  = _FoamColor;
                #endif

                // Base water colour — ODWRÓCONE: cWater dominuje, cDepth tylko jako akcent
                // depthCel=1 przy brzegu → cWater (główny kolor wody)
                // depthCel=0 w głębi    → cDepth (akcent w głębi / przy ostrych krawędziach)
                half4 col = lerp(cDepth, cWater, depthCel);
                col.a     = lerp(cWater.a, 1.0, depthN * _OpacityIntensity);  // głębia kryje, brzeg półprzezroczysty

                // Geometric face normal — ddx/ddy must stay in uniform control flow
                float3 geoNormal  = normalize(cross(ddx(IN.positionWS), ddy(IN.positionWS)));
                geoNormal         = (geoNormal.y < 0.0) ? -geoNormal : geoNormal;
                float3 surfNormal = (_PolyEnabled > 0.5) ? geoNormal : float3(0.0, 1.0, 0.0);

                // Normal map — dual-layer scroll, world XZ projection
                // TBN dla poziomej powierzchni: T=(1,0,0), B=(0,0,1), N=(0,1,0)
                // tangent→world: (tn.x, tn.z, tn.y)
                if (_NormalStrength > 0.001)
                {
                    float2 nmBase = IN.positionWS.xz * _NormalScale;
                    float2 nmUV1  = nmBase       + _Time.y * _NormalSpeed * float2( 1.0,  0.6);
                    float2 nmUV2  = nmBase * 0.7 + _Time.y * _NormalSpeed * float2(-0.4,  1.0);
                    float3 tnA    = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nmUV1));
                    float3 tnB    = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nmUV2));
                    float3 tn     = normalize(tnA + tnB);
                    float3 nmWorld = normalize(float3(tn.x, tn.z, tn.y));
                    surfNormal     = normalize(lerp(surfNormal, nmWorld, _NormalStrength));
                }

                // Fresnel
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  fresnel = pow(1.0 - saturate(dot(surfNormal, viewDir)), 3.0);
                col.a          = saturate(col.a + fresnel * 0.35 * fresnelScale);
                col.rgb        = lerp(col.rgb, cDepth.rgb, fresnel * 0.22 * fresnelScale * depthCel);

                // Swamp murk
                #if defined(_SWAMP)
                    float murkFade = _SwampMurkiness * (1.0 - depthN * 0.55);
                    col.rgb = lerp(col.rgb, _SwampMurkColor.rgb, murkFade);
                    col.a   = saturate(col.a + _SwampMurkiness * 0.25);
                #endif

                // Foam noise (value noise + texture)
                float2 noiseBase = IN.positionWS.xz * _ScaleNoise
                                 + _Time.y * _Speed * float2(1.0, 0.6);
                float  nA    = VNoise(noiseBase * 12.0);
                float  nB    = VNoise(noiseBase *  6.0 + float2(3.7, 1.4));
                float  nTex  = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseBase * 0.45).r;
                float  noise = (nA * 0.60 + nB * 0.40) * nTex;

                // Shore / object foam — oparte na depthFoam (min sąsiedztwa)
                // WAŻNE: noise mnoży _SizeFoam (proporcjonalnie), NIE dodaje do niego.
                // Stara formuła: SizeFoam + noise*NoiseIntensity → przy NoiseIntensity=1
                // foamEdge mogło wynosić 1.035, co białiło CAŁY zbiornik gdy depthFoam<1.
                // Nowa: foamEdge = SizeFoam * (1 + noise*NoiseIntensity) — zawsze blisko brzegu.
                float foamEdge = _SizeFoam * (1.0 + noise * _NoiseIntensity);
                float fw       = fwidth(depthFoam);
                float foamMask = 1.0 - smoothstep(foamEdge - fw, foamEdge + fw, depthFoam);
                col.rgb = lerp(col.rgb, cFoam.rgb, foamMask * cFoam.a);
                col.a   = max(col.a, foamMask * 0.96);

                // Caustics — sine interference (three axis pairs, no noise swirls)
                if (_CausticEnabled > 0.5)
                {
                    float2 cp = IN.positionWS.xz;
                    float  ck = _ScaleNoise * _CausticScale * 6.0;
                    float  ct = _Time.y * _Speed * 0.4;

                    float cA = saturate(sin(cp.x * ck + ct * 0.60) * sin(cp.y * ck + ct * 0.45) * 0.5 + 0.5);
                    float cB = saturate(sin((cp.x * 0.866 + cp.y * 0.500) * ck + ct * 0.50)
                                      * sin((cp.x * 0.500 - cp.y * 0.866) * ck + ct * 0.38) * 0.5 + 0.5);
                    float cC = saturate(sin((cp.x * 0.500 + cp.y * 0.866) * ck + ct * 0.42)
                                      * sin((cp.x * 0.866 - cp.y * 0.500) * ck + ct * 0.55) * 0.5 + 0.5);

                    float caustic  = pow(cA * cB * cC, _CausticSharpness * 0.35);
                    float cShimmer = 0.6 + 0.4 * sin(ct * 6.0 + cp.x * 1.3 + cp.y * 0.9);
                    caustic        = saturate(caustic * cShimmer * 2.5);
                    float cZone    = (1.0 - depthN) * (1.0 - foamMask);
                    col.rgb       += caustic * cZone * (_CausticStrength * causticScale);
                }

                // Reflections — Worley specular spots
                // All Worley + fwidth computed here (uniform control flow required for fwidth)
                float2 rBase   = IN.positionWS.xz * _ReflScale;
                float  rT      = _Time.y * _ReflSpeed;
                float2 rUV     = rBase + float2(rT * 0.7, rT * 0.5);
                float3 wcell   = Worley(rUV);
                float  rf1     = wcell.x;
                float  rCellID = wcell.z;
                float  rWfw    = fwidth(rf1) * 1.5;

                if (_ReflEnabled > 0.5)
                {
                    float rRadius  = _ReflDensity * 0.45;
                    float discMask = 1.0 - smoothstep(rRadius - rWfw, rRadius + rWfw, rf1);

                    float rLifePhase = rCellID * 6.283;
                    float rLifeRate  = 0.9 + rCellID * 1.2;
                    float rLife      = smoothstep(0.2, 0.8,
                                        0.5 + 0.5 * sin(rT * rLifeRate + rLifePhase));
                    float rFlash     = pow(0.5 + 0.5 * sin(rT * 7.0 + rCellID * 17.3), _ReflSharpness);
                    float rGlint     = discMask * rLife * (0.3 + 0.7 * rFlash);
                    rGlint          *= saturate(1.0 - depthN * 0.5) * (1.0 - foamMask * 0.95);
                    col.rgb         += rGlint * _ReflBrightness;
                }

                // Specular highlight (Blinn-Phong)
                Light  mainLight = GetMainLight();
                float3 halfDir   = normalize(mainLight.direction + viewDir);
                float  ndoth     = max(0.0, dot(surfNormal, halfDir));
                float  spec      = pow(ndoth, max(1.0, _Gloss * 256.0))
                                 * _Specular * (1.0 - foamMask);
                col.rgb += mainLight.color * spec;

                return col;
            }
            ENDHLSL
        }
    }

    CustomEditor "ArtWaterEditor"
    FallBack "Hidden/InternalErrorShader"
}

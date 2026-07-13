// URP stylized underwater rock for the procedural formations (boulders, spires,
// arches, grottos). No textures: colour ramp + strata bands + baked vertex AO
// (rgb = tint variation, a = occlusion — grotto interiors bake dark).
// Fades into the shared underwater fog/gradient like the sand and water do.
// Globals come from UnderwaterEnvironment.cs.
Shader "Custom/UnderwaterRock"
{
    Properties
    {
        _ColorLow ("Rock Colour (base/shade)", Color) = (0.16, 0.18, 0.22, 1)
        _ColorHigh ("Rock Colour (lit tops)", Color) = (0.45, 0.44, 0.42, 1)
        _StrataScale ("Strata Band Frequency", Float) = 2.2
        _StrataStrength ("Strata Band Strength", Range(0, 1)) = 0.35

        _CausticsColor ("Caustics Colour", Color) = (0.85, 1.0, 0.95, 1)
        _CausticsIntensity ("Caustics Intensity", Range(0, 3)) = 0.7
        _CausticsScale ("Caustics Scale", Float) = 0.6
        _CausticsSpeed ("Caustics Speed", Float) = 0.5

        _RimColor ("Rim Colour", Color) = (0.4, 0.75, 0.8, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorLow;
                half4 _ColorHigh;
                half4 _CausticsColor;
                half4 _RimColor;
                float _StrataScale;
                float _StrataStrength;
                float _CausticsIntensity;
                float _CausticsScale;
                float _CausticsSpeed;
                float _RimStrength;
            CBUFFER_END

            // Globals driven by UnderwaterEnvironment.cs
            half4 _UnderwaterFogColor;
            half4 _UnderwaterColorSurface;
            half4 _UnderwaterColorDeep;
            half4 _UnderwaterSunGlow;
            float _UnderwaterFogDensity;
            float _UnderwaterFadeStart;
            float _UnderwaterFadeEnd;
            float _UnderwaterLevel;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  color      : COLOR;
            };

            // Same interference caustics as the sand shader — visual continuity.
            half Caustics(float2 uv, float time)
            {
                const float sharpness = 0.005;
                float2 p = fmod(uv, TWO_PI) - 250.0;
                float2 i = p;
                float  c = 1.0;

                UNITY_UNROLL
                for (int n = 0; n < 3; n++)
                {
                    float t = time * (1.0 - (3.5 / float(n + 1)));
                    i = p + float2(cos(t - i.x) + sin(t + i.y),
                                   sin(t - i.y) + cos(t + i.x));
                    c += 1.0 / length(float2(p.x / (sin(i.x + t) / sharpness),
                                             p.y / (cos(i.y + t) / sharpness)));
                }
                c = 1.17 - pow(c / 3.0, 1.4);
                return pow(abs(c), 8.0);
            }

            float UnderwaterFog(float3 positionWS)
            {
                float dist      = distance(positionWS, _WorldSpaceCameraPos);
                float fadeEnd   = _UnderwaterFadeEnd > 0.01 ? _UnderwaterFadeEnd : 1e5;
                float fadeStart = min(_UnderwaterFadeStart, fadeEnd - 0.01);
                float expFog    = 1.0 - exp(-pow(dist * _UnderwaterFogDensity, 2.0));
                return max(expFog, smoothstep(fadeStart, fadeEnd, dist));
            }

            // Must stay identical to Custom/UnderwaterSkybox (see that file).
            half3 UnderwaterBackground(float3 viewDir)
            {
                half3 col = lerp(_UnderwaterFogColor.rgb, _UnderwaterColorSurface.rgb,
                                 smoothstep(0.0, 0.7, viewDir.y));
                col = lerp(col, _UnderwaterColorDeep.rgb,
                           smoothstep(0.0, 0.6, -viewDir.y));

                float3 L = _MainLightPosition.xyz;
                float sunAmount = saturate(dot(viewDir, L));
                col += _UnderwaterSunGlow.rgb *
                       (pow(sunAmount, 12.0) * 0.5 + pow(sunAmount, 90.0) * 0.8);
                return col;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 N = normalize(IN.normalWS);
                half ao = IN.color.a;

                // Colour ramp: lit tops vs shaded flanks, plus sediment strata
                // bands so tall formations read as layered rock.
                half topness = saturate(N.y * 0.5 + 0.5);
                half3 albedo = lerp(_ColorLow.rgb, _ColorHigh.rgb, topness) * IN.color.rgb;
                half strata = 0.5 + 0.5 * sin(IN.positionWS.y * _StrataScale);
                albedo *= lerp(1.0, 0.8 + 0.3 * strata, _StrataStrength);

                // Soft wrapped diffuse — matches the sand's gentle look.
                Light mainLight = GetMainLight();
                half halfLambert = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                half3 lighting = mainLight.color * halfLambert + SampleSH(N);
                half3 color = albedo * lighting * ao;

                // Caustics dance on up-facing surfaces only (and never inside
                // caves, thanks to the baked AO).
                float2 cuv = IN.positionWS.xz * _CausticsScale;
                half caustic = saturate(Caustics(cuv, _Time.y * _CausticsSpeed));
                color += _CausticsColor.rgb * mainLight.color *
                         (caustic * _CausticsIntensity * saturate(N.y) * ao);

                // Cool rim so silhouettes melt into the water instead of
                // cutting out hard against the fog.
                half3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                half rim = pow(1.0 - saturate(dot(N, V)), 3.0);
                color += _RimColor.rgb * (rim * _RimStrength * ao);

                color = lerp(color, UnderwaterBackground(-V), UnderwaterFog(IN.positionWS));
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half frag(Varyings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }
    }
}

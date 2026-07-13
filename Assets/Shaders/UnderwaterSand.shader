// URP underwater sand: world-space tiling, animated caustics, fade into the
// shared underwater fog/gradient. Globals come from UnderwaterEnvironment.cs.
Shader "Custom/UnderwaterSand"
{
    Properties
    {
        _BaseMap ("Sand Albedo", 2D) = "white" {}
        _BaseColor ("Sand Tint", Color) = (1.0, 0.96, 0.86, 1)
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.6
        _WorldTiling ("Texture Tiling (tiles per metre)", Float) = 0.35
        _DetileStrength ("Anti-Tiling Blend", Range(0, 1)) = 0.4

        _DeepColor ("Deep Water Tint", Color) = (0.30, 0.65, 0.75, 1)
        _DepthTintStrength ("Depth Tint Strength", Range(0, 1)) = 0.5
        _DepthTintRange ("Depth Tint Range (m)", Float) = 5

        _CausticsColor ("Caustics Colour", Color) = (0.85, 1.0, 0.95, 1)
        _CausticsIntensity ("Caustics Intensity", Range(0, 3)) = 1.0
        _CausticsScale ("Caustics Scale", Float) = 0.6
        _CausticsSpeed ("Caustics Speed", Float) = 0.5
        _CausticsChroma ("Caustics Rainbow Split", Range(0, 2)) = 1
        _SparkleIntensity ("Sand Sparkle", Range(0, 2)) = 0.6
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

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _CausticsColor;
                float _NormalStrength;
                float _WorldTiling;
                float _DetileStrength;
                float _DepthTintStrength;
                float _DepthTintRange;
                float _CausticsIntensity;
                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsChroma;
                float _SparkleIntensity;
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            // Animated interference pattern approximating sunlight caustics.
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
                float dist     = distance(positionWS, _WorldSpaceCameraPos);
                float fadeEnd  = _UnderwaterFadeEnd > 0.01 ? _UnderwaterFadeEnd : 1e5;
                float fadeStart = min(_UnderwaterFadeStart, fadeEnd - 0.01);
                float expFog   = 1.0 - exp(-pow(dist * _UnderwaterFogDensity, 2.0));
                // smoothstep guarantees full fog before the last chunk ends
                return max(expFog, smoothstep(fadeStart, fadeEnd, dist));
            }

            // View-dependent backdrop colour; must match Custom/UnderwaterSkybox.
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
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionWS.xz * _WorldTiling;

                // Two samples at different scales hide the texture repeat on endless sand.
                half3 albedo  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half3 albedo2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * 0.137 + 0.5).rgb;
                albedo = lerp(albedo, albedo2, _DetileStrength) * _BaseColor.rgb;

                // Normal mapping using a world-axis tangent frame (fine for a heightfield).
                half3 nTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalStrength);
                half3 Ngeo = normalize(IN.normalWS);
                half3 T = normalize(cross(half3(0, 0, 1), Ngeo));
                half3 B = cross(Ngeo, T);
                half3 N = normalize(nTS.x * T + nTS.y * B + nTS.z * Ngeo);

                // Soft, wrapped diffuse — underwater light is diffuse and gentle.
                Light mainLight = GetMainLight();
                half  halfLambert = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                half3 lighting = mainLight.color * halfLambert + SampleSH(N);
                half3 color = albedo * lighting;

                // Caustics with a subtle chromatic split (two phase-shifted samples).
                float2 cuv = IN.positionWS.xz * _CausticsScale;
                float  ct  = _Time.y * _CausticsSpeed;
                half cA = saturate(Caustics(cuv, ct));
                half cB = saturate(Caustics(cuv + 0.04 * _CausticsChroma, ct + 0.05));
                half3 caustic = half3(cA, 0.5 * (cA + cB), cB);
                color += _CausticsColor.rgb * mainLight.color *
                         (caustic * _CausticsIntensity * saturate(Ngeo.y));

                // Tiny specular glints riding on the caustic highlights — wet sand.
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                half3 H = normalize(mainLight.direction + V);
                half spec = pow(saturate(dot(N, H)), 48.0);
                color += mainLight.color * (spec * _SparkleIntensity * (0.3 + cA));

                // Lower sand shifts toward the deep water colour.
                float depthBelow = saturate((_UnderwaterLevel - IN.positionWS.y) /
                                            max(_DepthTintRange, 0.01));
                color = lerp(color, color * _DeepColor.rgb * 1.6,
                             depthBelow * _DepthTintStrength);

                // Fade into the same view-dependent backdrop the skybox draws.
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

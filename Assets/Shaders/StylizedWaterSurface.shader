// URP translucent water surface seen from below: waves, fresnel, animated
// pattern, fades into the shared underwater fog at distance.
Shader "Custom/StylizedWaterSurface"
{
    Properties
    {
        _WaterColor ("Water Colour", Color) = (0.16, 0.62, 0.72, 1)
        _Opacity ("Base Opacity", Range(0, 1)) = 0.45

        _WaveAmplitude ("Wave Amplitude (m)", Float) = 0.12
        _WaveLength ("Wave Length (m)", Float) = 4.0
        _WaveSpeed ("Wave Speed", Float) = 0.8

        _PatternScale ("Light Pattern Scale", Float) = 0.3
        _PatternIntensity ("Light Pattern Intensity", Range(0, 2)) = 0.7
        _PatternSpeed ("Light Pattern Speed", Float) = 0.5

        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                float _Opacity;
                float _WaveAmplitude;
                float _WaveLength;
                float _WaveSpeed;
                float _PatternScale;
                float _PatternIntensity;
                float _PatternSpeed;
                float _FresnelPower;
            CBUFFER_END

            // Globals driven by UnderwaterEnvironment.cs
            half4 _UnderwaterFogColor;
            half4 _UnderwaterColorSurface;
            half4 _UnderwaterColorDeep;
            half4 _UnderwaterSunGlow;
            float _UnderwaterFogDensity;
            float _UnderwaterFadeStart;
            float _UnderwaterFadeEnd;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            // Same interference pattern as the sand caustics — visual continuity.
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
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                // Two world-space sine waves; world-space phase means the surface can
                // follow the viewer without the waves visibly "swimming" with it.
                float  k  = TWO_PI / max(_WaveLength, 0.01);
                float  t  = _Time.y * _WaveSpeed;
                float2 d1 = normalize(float2(1.0, 0.6));
                float2 d2 = normalize(float2(-0.7, 1.0));
                float  p1 = dot(posWS.xz, d1) * k + t;
                float  p2 = dot(posWS.xz, d2) * (k * 1.7) - t * 1.3;

                posWS.y += (sin(p1) + 0.6 * sin(p2)) * _WaveAmplitude;

                // Analytic wave normal (derivative of the height function).
                float dx = (cos(p1) * d1.x * k + 0.6 * cos(p2) * d2.x * k * 1.7) * _WaveAmplitude;
                float dz = (cos(p1) * d1.y * k + 0.6 * cos(p2) * d2.y * k * 1.7) * _WaveAmplitude;
                OUT.normalWS = normalize(float3(-dx, 1.0, -dz));

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                half3 N = normalize(IN.normalWS);

                // abs() so the fresnel works identically from above and below.
                half ndv     = abs(dot(N, V));
                half fresnel = pow(1.0 - ndv, _FresnelPower);

                half pattern = saturate(Caustics(IN.positionWS.xz * _PatternScale,
                                                 _Time.y * _PatternSpeed));

                Light mainLight = GetMainLight();
                half3 color = _WaterColor.rgb * (0.6 + 0.4 * mainLight.color);
                color += pattern * _PatternIntensity * mainLight.color * 0.6;
                color += fresnel * _WaterColor.rgb * 0.5;

                half alpha = saturate(_Opacity + fresnel * 0.35 + pattern * 0.15);

                // Melt into the same view-dependent backdrop the skybox draws
                // (alpha -> 1 so the horizon band is solid and seamless).
                float fog = UnderwaterFog(IN.positionWS);
                color = lerp(color, UnderwaterBackground(-V), fog);
                alpha = lerp(alpha, 1.0, fog);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

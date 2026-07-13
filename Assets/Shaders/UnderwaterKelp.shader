// URP kelp/seagrass ribbon shader: vertex-shader current sway (weighted by
// uv.y so roots stay planted), root→tip colour ramp, translucent backlight
// when the sun shines through a blade, shared underwater fog. Two-sided.
// Globals come from UnderwaterEnvironment.cs.
Shader "Custom/UnderwaterKelp"
{
    Properties
    {
        _RootColor ("Root Colour", Color) = (0.03, 0.14, 0.07, 1)
        _TipColor ("Tip Colour", Color) = (0.14, 0.5, 0.2, 1)
        _Translucency ("Sun Through Blades", Range(0, 2)) = 0.8

        _SwayAmplitude ("Sway Amplitude (m)", Float) = 0.22
        _SwaySpeed ("Sway Speed", Float) = 0.9
        _SwayWavelength ("Current Wavelength (m)", Float) = 9.0
        _FlutterStrength ("Tip Flutter", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RootColor;
                half4 _TipColor;
                float _Translucency;
                float _SwayAmplitude;
                float _SwaySpeed;
                float _SwayWavelength;
                float _FlutterStrength;
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
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half4  color      : COLOR;
            };

            // One broad current wave rolling through the world plus a small
            // per-blade flutter. Weighted by uv.y² so roots stay planted.
            float3 Sway(float3 posWS, float2 uv)
            {
                float w = uv.y * uv.y;
                float k = TWO_PI / max(_SwayWavelength, 0.01);
                float t = _Time.y * _SwaySpeed;

                float phase = dot(posWS.xz, normalize(float2(1.0, 0.35))) * k;
                float2 dir = normalize(float2(1.0, 0.35));

                float bend = sin(phase + t) + 0.4 * sin(phase * 2.3 - t * 1.7);
                float flutter = sin(posWS.x * 5.1 + t * 3.3 + uv.x * 4.0) *
                                sin(posWS.z * 4.3 - t * 2.9);

                posWS.xz += dir * (bend * _SwayAmplitude * w);
                posWS.x  += flutter * (_FlutterStrength * _SwayAmplitude * 0.35 * w);
                // Slight settle-down as the blade bends over.
                posWS.y  -= abs(bend) * _SwayAmplitude * 0.18 * w;
                return posWS;
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
                posWS = Sway(posWS, IN.uv);

                OUT.positionWS = posWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 N = normalize(IN.normalWS) * IS_FRONT_VFACE(cullFace, 1, -1);
                half3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                half3 albedo = lerp(_RootColor.rgb, _TipColor.rgb, IN.uv.y) * IN.color.rgb;

                Light mainLight = GetMainLight();
                half halfLambert = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                half3 color = albedo * (mainLight.color * halfLambert + SampleSH(N));

                // Sun bleeding through the blade when it's between us and the
                // light — cheap subsurface glow, strongest at the thin tips.
                half back = pow(saturate(dot(V, -mainLight.direction)), 4.0);
                color += _TipColor.rgb * mainLight.color *
                         (back * _Translucency * IN.uv.y);

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
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RootColor;
                half4 _TipColor;
                float _Translucency;
                float _SwayAmplitude;
                float _SwaySpeed;
                float _SwayWavelength;
                float _FlutterStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings { float4 positionCS : SV_POSITION; };

            // Depth must bend with the same sway as the colour pass.
            float3 Sway(float3 posWS, float2 uv)
            {
                float w = uv.y * uv.y;
                float k = TWO_PI / max(_SwayWavelength, 0.01);
                float t = _Time.y * _SwaySpeed;

                float phase = dot(posWS.xz, normalize(float2(1.0, 0.35))) * k;
                float2 dir = normalize(float2(1.0, 0.35));

                float bend = sin(phase + t) + 0.4 * sin(phase * 2.3 - t * 1.7);
                float flutter = sin(posWS.x * 5.1 + t * 3.3 + uv.x * 4.0) *
                                sin(posWS.z * 4.3 - t * 2.9);

                posWS.xz += dir * (bend * _SwayAmplitude * w);
                posWS.x  += flutter * (_FlutterStrength * _SwayAmplitude * 0.35 * w);
                posWS.y  -= abs(bend) * _SwayAmplitude * 0.18 * w;
                return posWS;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS = Sway(posWS, IN.uv);
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half frag(Varyings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }
    }
}

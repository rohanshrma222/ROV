// Full-screen UI overlay that merges the real AR camera passthrough with the
// underwater atmosphere: a soft colour-graded gradient plus animated procedural
// caustics and god rays, blended on top of the live camera feed rather than
// replacing it. Reads the same _Underwater* globals UnderwaterEnvironment.cs
// already drives every frame, so the palette always matches the rest of the scene.
Shader "Custom/UnderwaterScreenOverlay"
{
    Properties
    {
        // Unused by the effect itself, but uGUI's CanvasRenderer always pushes the
        // Image's sprite texture into a "_MainTex" slot and warns if it's missing.
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _CausticScale ("Caustic Scale", Float) = 6
        _CausticSpeed ("Caustic Speed", Float) = 0.6
        _CausticIntensity ("Caustic Intensity", Range(0, 1)) = 0.25
        _GodRayIntensity ("God Ray Intensity", Range(0, 1)) = 0.25
        _GodRaySpeed ("God Ray Speed", Float) = 0.35
        _OverlayAlpha ("Overlay Blend Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _CausticScale;
                float _CausticSpeed;
                float _CausticIntensity;
                float _GodRayIntensity;
                float _GodRaySpeed;
                float _OverlayAlpha;
            CBUFFER_END

            // Globals driven by UnderwaterEnvironment.cs, shared with Custom/UnderwaterSkybox.
            half4 _UnderwaterFogColor;
            half4 _UnderwaterColorSurface;
            half4 _UnderwaterColorDeep;
            half4 _UnderwaterSunGlow;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Vertical colour grade: surface glow near the top of the frame,
                // fading toward the deep tone near the bottom, tied into the
                // same fog colour used everywhere else underwater.
                half3 col = lerp(_UnderwaterColorDeep.rgb, _UnderwaterColorSurface.rgb, saturate(uv.y));
                col = lerp(col, _UnderwaterFogColor.rgb, 0.5);

                // Procedural caustics: two drifting overlapping sine grids.
                float t = _Time.y * _CausticSpeed;
                float2 p = uv * _CausticScale;
                float caustic = sin(p.x + t) * sin(p.y - t * 0.7);
                caustic += sin((p.x + p.y) * 0.6 - t * 1.3) * 0.5;
                caustic = saturate(caustic * 0.5 + 0.5);
                caustic = pow(caustic, 3.0);
                col += _UnderwaterSunGlow.rgb * caustic * _CausticIntensity;

                // Soft god-ray shafts drifting sideways, strongest near the top.
                float rayT = _Time.y * _GodRaySpeed;
                float rays = sin(uv.x * 18.0 + rayT * 0.6) * sin(uv.x * 7.0 - rayT * 0.3);
                rays = pow(saturate(rays), 4.0) * saturate(1.0 - uv.y * 0.6);
                col += _UnderwaterSunGlow.rgb * rays * _GodRayIntensity;

                float alpha = _OverlayAlpha * IN.color.a;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

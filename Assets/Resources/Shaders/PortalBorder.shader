Shader "Tecnocampus/PortalBorder"
{
    Properties
    {
        _Cutout ("Cutout Threshold", Range(0.0, 1.0)) = 0.5
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.0

        [Header(Circular Mask)]
        [Toggle(USE_CIRCULAR_MASK)] _UseCircularMask ("Use Circular Mask", Float) = 1
        _CircleRadius ("Circle Radius", Range(0.0, 1.0)) = 0.45
        _CircleSoftness ("Circle Edge Softness", Range(0.0, 0.5)) = 0.05
        _AspectRatio ("Aspect Ratio (width/height)", Float) = 1.0

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.2, 0.8, 1.0, 1)
        _OutlineThickness ("Outline Thickness", Range(0.0, 0.2)) = 0.03
        _OutlineEmission ("Outline Emission Strength", Range(0.0, 5.0)) = 1.0

        [Header(Opening Effect)]
        _PortalOpen ("Portal Open", Range(0.0, 1.0)) = 1.0
        _OpeningNoiseScale ("Opening Noise Scale", Range(1.0, 50.0)) = 10.0
        _OpeningNoiseSpeed ("Opening Noise Speed", Float) = 2.0
        _NoiseColor ("Noise Color", Color) = (1.0, 0.5, 0.0, 1)
        _NoiseEmission ("Noise Emission Strength", Range(0.0, 5.0)) = 1.0
        _NoiseTransparency ("Noise Transparency", Range(0.0, 1.0)) = 0.7

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0.2, 0.8, 1.0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0.0, 10.0)) = 2.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Cull Back          // or Off if your mesh is single-sided
            ZWrite Off
            ZTest Less         // Win z-fighting when at same depth
            Offset -1, -1      // Depth bias to ensure portal renders on top
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ USE_CIRCULAR_MASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---- constants
            #define NOISE_SCALE_INVERT 51.0
            #define HOLE_RADIUS_FACTOR 1.0
            #define NOISE_INFLUENCE 0.1
            #define SMOOTHNESS_MIN 0.0
            #define SMOOTHNESS_MAX 0.15
            #define VORTEX_STRENGTH 2.0

            struct Attributes { 
                float4 positionOS : POSITION; 
                float2 uv : TEXCOORD0; 
                float3 normalOS : NORMAL;
            };
            struct Varyings { 
                float4 positionHCS : SV_POSITION; 
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Cutout;
                float _Brightness;
                float _UseCircularMask;   // driven by the [Toggle], not read but keeps SRP Batcher happy
                float _CircleRadius;
                float _CircleSoftness;
                float _AspectRatio;
                float4 _OutlineColor;
                float _OutlineThickness;
                float _OutlineEmission;
                float _PortalOpen;
                float _OpeningNoiseScale;
                float _OpeningNoiseSpeed;
                float4 _NoiseColor;
                float _NoiseEmission;
                float _NoiseTransparency;
                float4 _EmissionColor;
                float _EmissionIntensity;
            CBUFFER_END

            // hash + value noise
            float rand(float2 co) { return frac(sin(dot(co, float2(12.9898,78.233))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p); fp = fp*fp*(3.0-2.0*fp);
                float a = rand(ip);
                float b = rand(ip+float2(1,0));
                float c = rand(ip+float2(0,1));
                float d = rand(ip+float2(1,1));
                return lerp(lerp(a,b,fp.x), lerp(c,d,fp.x), fp.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);
                
                OUT.positionHCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.normalWS = normalInput.normalWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half finalAlpha = 1.0;
                half outlineMask = 0.0;

                #if defined(USE_CIRCULAR_MASK)
                    float2 centeredUV = IN.uv - 0.5;
                    centeredUV.x *= _AspectRatio;
                    float dist = length(centeredUV);

                    finalAlpha = 1.0 - smoothstep(_CircleRadius - _CircleSoftness, _CircleRadius + _CircleSoftness, dist);

                    float outlineStart = _CircleRadius - _OutlineThickness * 0.5;
                    float outlineEnd   = _CircleRadius + _OutlineThickness * 1.5;
                    outlineMask = smoothstep(outlineEnd, outlineStart, dist) * smoothstep(outlineStart - _CircleSoftness, outlineStart, dist);
                    finalAlpha = max(finalAlpha, outlineMask);
                    clip(finalAlpha - 0.01);

                    // use SRP time
                    float t = _TimeParameters.y;

                    float angle = atan2(centeredUV.y, centeredUV.x);
                    float normalizedDist = dist / _CircleRadius;

                    float rotatedAngle = angle + normalizedDist * VORTEX_STRENGTH - t * _OpeningNoiseSpeed;

                    float2 noiseCoord = float2(cos(rotatedAngle), sin(rotatedAngle)) * normalizedDist;
                    noiseCoord *= (NOISE_SCALE_INVERT - _OpeningNoiseScale);

                    float noiseValue = noise(noiseCoord + t * _OpeningNoiseSpeed * 0.5) * 0.5;
                    noiseValue += noise(noiseCoord * 0.5 + t * _OpeningNoiseSpeed * 0.7) * 0.25;
                    noiseValue += noise(noiseCoord * 2.0 + t * _OpeningNoiseSpeed * 0.3) * 0.25;
                    noiseValue = frac(noiseValue);

                    float holeRadius = _PortalOpen * (HOLE_RADIUS_FACTOR + noiseValue * NOISE_INFLUENCE);
                    float smoothRange = lerp(SMOOTHNESS_MIN, SMOOTHNESS_MAX, _PortalOpen);
                    float insideHole = smoothstep(holeRadius + smoothRange, holeRadius - smoothRange, normalizedDist);
                    insideHole = (_PortalOpen > 0.01) ? insideHole : 0.0;

                    half3 noiseColor = _NoiseColor.rgb * _NoiseEmission * noiseValue;
                    half4 noiseCol   = half4(noiseColor, 1.0); // Full opacity for noise

                    // Make hole area transparent (show world behind), keep noise fully opaque outside
                    half4 baseCol = noiseCol;
                    baseCol.a = (1.0 - insideHole); // Fully opaque outside hole, transparent inside

                    half3 portalContent = baseCol.rgb * _Brightness * finalAlpha;
                    half3 outlineCol    = _OutlineColor.rgb * outlineMask * _OutlineEmission;
                    half3 finalColor    = lerp(portalContent, outlineCol, outlineMask);

                    // Calculate emission - combine outline and noise emission
                    half3 emission = outlineCol * _EmissionColor.rgb * _EmissionIntensity;
                    emission += portalContent * _EmissionColor.rgb * _EmissionIntensity * 0.5;
                    
                    // Add emission to final color
                    finalColor += emission;

                    // Ensure outline and border area is fully opaque
                    half finalAlphaWithNoise = max(finalAlpha * baseCol.a, outlineMask);
                    return half4(finalColor, finalAlphaWithNoise);
                #else
                    clip(1.0 - _Cutout);
                    return half4(0,0,0,0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}

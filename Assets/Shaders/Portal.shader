Shader "Tecnocampus/Portal"
{
    Properties
    {
        _MainTex ("Portal View", 2D) = "white" {}
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
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Lighting Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature USE_CIRCULAR_MASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Portal effect constants
            #define NOISE_SCALE_INVERT 51.0
            #define HOLE_RADIUS_FACTOR 1.0
            #define NOISE_INFLUENCE 0.1
            #define SMOOTHNESS_MIN 0.0
            #define SMOOTHNESS_MAX 0.15
            #define NOISE_COLOR_MIN 0.0
            #define NOISE_COLOR_MAX 1.0
            #define NOISE_ALPHA_MIN 0.3
            #define NOISE_ALPHA_MAX 1.0
            #define VORTEX_STRENGTH 2.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex); 
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float _Cutout;
            float _Brightness;
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

            // Simple pseudo-random function
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Simple noise function
            float noise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                fp = fp * fp * (3.0 - 2.0 * fp);
                
                float a = rand(ip);
                float b = rand(ip + float2(1.0, 0.0));
                float c = rand(ip + float2(0.0, 1.0));
                float d = rand(ip + float2(1.0, 1.0));
                
                float ab = lerp(a, b, fp.x);
                float cd = lerp(c, d, fp.x);
                return lerp(ab, cd, fp.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                half finalAlpha = 1.0;
                half outlineMask = 0.0;

                #ifdef USE_CIRCULAR_MASK
                    float2 centeredUV = IN.uv - 0.5;
                    centeredUV.x *= _AspectRatio;
                    float dist = length(centeredUV);

                    // Interior del portal
                    finalAlpha = 1.0 - smoothstep(_CircleRadius - _CircleSoftness, _CircleRadius + _CircleSoftness, dist);

                    // Outline: simple y eficiente
                    float outlineStart = _CircleRadius - _OutlineThickness * 0.5;
                    float outlineEnd = _CircleRadius + _OutlineThickness * 1.5;
                    outlineMask = smoothstep(outlineEnd, outlineStart, dist) * smoothstep(outlineStart - _CircleSoftness, outlineStart, dist);
                    
                    finalAlpha = max(finalAlpha, outlineMask);
                    clip(finalAlpha - 0.01);

                    // Efecto de apertura: ruido turbulento con movimiento hacia el centro
                    float angle = atan2(centeredUV.y, centeredUV.x);
                    float normalizedDist = dist / _CircleRadius;
                    
                    // Animar el ángulo para crear efecto de vórtice (ruido girando hacia el centro)
                    float rotatedAngle = angle + normalizedDist * VORTEX_STRENGTH - _Time.y * _OpeningNoiseSpeed;
                    
                    // Convertir coordenadas polares a cartesianas para el noise
                    float2 noiseCoord = float2(cos(rotatedAngle), sin(rotatedAngle)) * normalizedDist;
                    noiseCoord *= (NOISE_SCALE_INVERT - _OpeningNoiseScale);
                    
                    // Fractional Brownian Motion para noise suave pero con detalle
                    float noiseValue = noise(noiseCoord + _Time.y * _OpeningNoiseSpeed * 0.5) * 0.5;
                    noiseValue += noise(noiseCoord * 0.5 + _Time.y * _OpeningNoiseSpeed * 0.7) * 0.25;
                    noiseValue += noise(noiseCoord * 2.0 + _Time.y * _OpeningNoiseSpeed * 0.3) * 0.25;
                    noiseValue = frac(noiseValue);
                    
                    // Distancia normalizada desde el centro
                    // normalizedDist ya está definido arriba
                    
                    // Agujero abierto desde el centro
                    float holeRadius = _PortalOpen * (HOLE_RADIUS_FACTOR + noiseValue * NOISE_INFLUENCE);
                    
                    // Máscara con bordes difusos - usar smoothRange dinámico basado en apertura
                    float smoothRange = lerp(SMOOTHNESS_MIN, SMOOTHNESS_MAX, _PortalOpen);
                    float insideHole = smoothstep(holeRadius + smoothRange, holeRadius - smoothRange, normalizedDist);
                    
                    // Asegurar que el agujero esté completamente cerrado cuando _PortalOpen = 0
                    insideHole = _PortalOpen > 0.01 ? insideHole : 0.0;
                    
                    // Textura del portal (RenderTexture)
                    half4 renderTexture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                    
                    // Color del ruido con mucha más variación basada en el noise
                    half3 noiseColor = _NoiseColor.rgb * _NoiseEmission * noiseValue;
                    
                    // Modular la opacidad con el noise para más variación
                    half noiseAlpha = _NoiseTransparency * (noiseValue * 0.7 + 0.3);
                    half4 noiseCol = half4(noiseColor, noiseAlpha);
                    
                    // Mezclar: el agujero muestra RenderTexture, el resto muestra ruido
                    half4 baseCol = lerp(noiseCol, renderTexture, insideHole);
                    
                    half3 portalContent = baseCol.rgb * _Brightness * finalAlpha;
                    half3 outlineCol = _OutlineColor.rgb * outlineMask * _OutlineEmission;
                    
                    // El outline reemplaza el contenido en su área (no se suma)
                    half3 finalColor = lerp(portalContent, outlineCol, outlineMask);
                    
                    // Aplicar la transparencia consistentemente
                    half finalAlphaWithNoise = finalAlpha * lerp(_NoiseTransparency, 1.0, insideHole);
                    return half4(finalColor, finalAlphaWithNoise);
                #else
                    clip(1.0 - _Cutout);
                    return half4(0, 0, 0, 0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}

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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            float4 _MainTex_ST;
            float4 _MaskTex_ST;
            float _Cutout;
            float _Brightness;
            float _CircleRadius;
            float _CircleSoftness;
            float _AspectRatio;

            float4 _OutlineColor;
            float _OutlineThickness;
            float _OutlineEmission;

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
                half alpha = 1.0;

                float outlineMask = 0.0;

                #ifdef USE_CIRCULAR_MASK
                    float2 centeredUV = IN.uv - 0.5;
                    centeredUV.x *= _AspectRatio;
                    float dist = length(centeredUV);

                    // Interior del portal
                    alpha = 1.0 - smoothstep(_CircleRadius - _CircleSoftness, _CircleRadius + _CircleSoftness, dist);

                    // Outline: comienza en el borde y se extiende hacia afuera con difuminado
                    float outlineStart = _CircleRadius - _OutlineThickness * 0.5;
                    float outlineEnd = _CircleRadius + _OutlineThickness * 1.5;
                    outlineMask = 1.0 - smoothstep(outlineStart, outlineEnd, dist);
                    outlineMask *= smoothstep(outlineStart - _CircleSoftness, outlineStart, dist);

                    // Permitir que el outline se renderice fuera del círculo
                    float combinedAlpha = max(alpha, outlineMask);
                    clip(combinedAlpha - 0.01);
                #else
                    half4 maskColor = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv);
                    alpha = maskColor.a;
                    clip(alpha - _Cutout);
                #endif

                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                
                // Contenido del portal (solo dentro del círculo)
                half3 portalContent = baseCol.rgb * _Brightness * alpha;
                
                // Outline (color emisivo que se difumina hacia afuera)
                half3 outlineCol = _OutlineColor.rgb * outlineMask * _OutlineEmission;
                
                // Combinar: el outline se suma al contenido del portal
                half3 finalColor = portalContent + outlineCol;
                
                // El alpha final combina el portal y el outline
                half finalAlpha = max(alpha, outlineMask);
                
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

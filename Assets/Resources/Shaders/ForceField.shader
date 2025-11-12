Shader "Tecnocampus/ForceField"
{
    Properties
    {
        [Header(Base Color)]
        _BaseColor ("Base Color", Color) = (0.2, 0.5, 1.0, 0.3)
        _Intensity ("Intensity", Range(0.0, 5.0)) = 1.5
        
        [Header(Fresnel Effect)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10.0)) = 2.0
        _FresnelIntensity ("Fresnel Intensity", Range(0.0, 5.0)) = 2.0
        _FresnelColor ("Fresnel Color", Color) = (0.3, 0.7, 1.0, 1.0)
        
        [Header(Energy Waves)]
        _WaveSpeed ("Wave Speed", Range(0.0, 5.0)) = 1.0
        _WaveScale ("Wave Scale", Range(0.1, 50.0)) = 10.0
        _WaveIntensity ("Wave Intensity", Range(0.0, 1.0)) = 0.3
        
        [Header(Noise Pattern)]
        _NoiseScale ("Noise Scale", Range(0.1, 50.0)) = 5.0
        _NoiseSpeed ("Noise Speed", Range(0.0, 5.0)) = 0.5
        _NoiseIntensity ("Noise Intensity", Range(0.0, 1.0)) = 0.2
        
        [Header(Edge Glow)]
        _EdgeGlowPower ("Edge Glow Power", Range(0.1, 10.0)) = 3.0
        _EdgeGlowIntensity ("Edge Glow Intensity", Range(0.0, 5.0)) = 1.5
        
        [Header(Transparency)]
        _Transparency ("Transparency", Range(0.0, 1.0)) = 0.7
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _FresnelPower;
                half _FresnelIntensity;
                half4 _FresnelColor;
                half _WaveSpeed;
                half _WaveScale;
                half _WaveIntensity;
                half _NoiseScale;
                half _NoiseSpeed;
                half _NoiseIntensity;
                half _EdgeGlowPower;
                half _EdgeGlowIntensity;
                half _Transparency;
            CBUFFER_END

            // Simple noise function
            float noise(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
            }

            // Smooth noise
            float smoothNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n = i.x + i.y * 57.0 + 113.0 * i.z;
                return lerp(
                    lerp(
                        lerp(noise(float3(n + 0.0, 0.0, 0.0)), noise(float3(n + 1.0, 0.0, 0.0)), f.x),
                        lerp(noise(float3(n + 57.0, 0.0, 0.0)), noise(float3(n + 58.0, 0.0, 0.0)), f.x),
                        f.y
                    ),
                    lerp(
                        lerp(noise(float3(n + 113.0, 0.0, 0.0)), noise(float3(n + 114.0, 0.0, 0.0)), f.x),
                        lerp(noise(float3(n + 170.0, 0.0, 0.0)), noise(float3(n + 171.0, 0.0, 0.0)), f.x),
                        f.y
                    ),
                    f.z
                );
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);
                
                OUT.positionHCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.normalWS = normalInput.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                OUT.uv = IN.uv;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Normalize vectors
                half3 normalWS = normalize(IN.normalWS);
                half3 viewDirWS = normalize(IN.viewDirWS);
                
                // Fresnel effect (brighter at edges)
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half fresnelEffect = fresnel * _FresnelIntensity;
                
                // Edge glow (stronger fresnel)
                half edgeGlow = pow(fresnel, _EdgeGlowPower) * _EdgeGlowIntensity;
                
                // Animated noise pattern
                half3 noiseCoord = IN.positionWS * _NoiseScale + _Time.y * _NoiseSpeed;
                half noiseValue = smoothNoise(noiseCoord) * _NoiseIntensity;
                
                // Energy waves (vertical scrolling effect)
                half waveCoord = IN.positionWS.y * _WaveScale + _Time.y * _WaveSpeed;
                half wave = sin(waveCoord) * 0.5 + 0.5;
                half waveEffect = wave * _WaveIntensity;
                
                // Combine effects
                half3 baseColor = _BaseColor.rgb * _Intensity;
                half3 fresnelColor = _FresnelColor.rgb * fresnelEffect;
                half3 edgeColor = _FresnelColor.rgb * edgeGlow;
                
                // Add noise and wave variations
                half3 finalColor = baseColor + fresnelColor + edgeColor;
                finalColor += noiseValue * _FresnelColor.rgb;
                finalColor += waveEffect * _FresnelColor.rgb * 0.5;
                
                // Calculate alpha
                half baseAlpha = _BaseColor.a * _Transparency;
                half alpha = baseAlpha + fresnel * (1.0 - baseAlpha) * 0.5 + edgeGlow * 0.3;
                alpha = saturate(alpha);
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}


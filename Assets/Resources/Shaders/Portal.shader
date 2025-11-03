Shader "Tecnocampus/Portal"
{
    Properties
    {
        _MainTex ("Portal View", 2D) = "white" {}
        _FullScreen ("Full Screen Mode", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { 
                float4 positionOS : POSITION; 
            };
            
            struct Varyings { 
                float4 positionHCS : SV_POSITION; 
                float4 screenPos : TEXCOORD0; 
                float4 positionCS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FullScreen;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionCS = OUT.positionHCS;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV;
                
                // Si FullScreen está activado, usar coordenadas de pantalla completas
                if (_FullScreen > 0.5) {
                    // Coordenadas de pantalla completas (0,0 a 1,1)
                    screenUV = IN.positionCS.xy / IN.positionCS.w;
                    screenUV = screenUV * 0.5 + 0.5;
                    screenUV.y = 1.0 - screenUV.y; // Flip Y para Unity
                } else {
                    // Modo normal: usar screenPos del cubo como máscara
                    screenUV = IN.screenPos.xy / IN.screenPos.w;
                }
                
                half4 portalView = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                return portalView;
            }
            ENDHLSL
        }
    }

    FallBack Off
}

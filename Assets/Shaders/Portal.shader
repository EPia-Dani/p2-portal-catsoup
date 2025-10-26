Shader "Custom/Portal"
{
    Properties
    {
        _MainTex ("Portal View", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _Cutout ("Cutout Threshold", Range(0.0, 1.0)) = 0.5
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.0
    }
    
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
        
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            
            ZWrite On
            ZTest Less
            Cull Back
            Lighting Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
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
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            
            float4 _MainTex_ST;
            float4 _MaskTex_ST;
            float _Cutout;
            float _Brightness;
            
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
                // Normalize screen position
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                
                // Sample mask texture and apply cutout
                half4 maskColor = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv);
                
                // Discard pixels that don't pass cutout threshold
                if (maskColor.a < _Cutout)
                    discard;
                
                // Sample main portal texture using screen UV
                half4 portalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                
                // Apply brightness
                portalColor.rgb *= _Brightness;
                
                return portalColor;
            }
            ENDHLSL
        }
    }
}

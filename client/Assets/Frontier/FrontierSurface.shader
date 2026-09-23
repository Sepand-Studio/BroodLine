Shader "Broodline/FrontierSurface"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _BaseColor ("Base", Color) = (1,1,1,1)
        _BaseMap ("Base map", 2D) = "white" {}
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
        _Smoothness ("Smoothness", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint, _BaseColor;
                float4 _BaseMap_ST;
                float _Cutoff, _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 surface:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 shadowCoord:TEXCOORD2; float4 color:COLOR; float polish:TEXCOORD3; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(p);
                output.color = input.color;
                output.polish = input.surface.x;
                // Mesh palette values are authored as sRGB hex colors.
                #ifndef UNITY_COLORSPACE_GAMMA
                    output.color.rgb = SRGBToLinear(output.color.rgb);
                #endif
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float3 n = normalize(input.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = input.shadowCoord;
                #else
                    // Select the cascade per fragment on the large ground mesh.
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                float diffuse = smoothstep(-.35, .8, dot(n, light.direction));
                float3 lighting = lerp(float3(.48,.56,.66), float3(1.0,.96,.83), diffuse);
                lighting *= lerp(.65, 1.0, light.shadowAttenuation);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // A soft skin response, satin armor and a sharp eye reflection, on one material.
                float polish = saturate(input.polish);
                float spec = pow(saturate(dot(n, normalize(light.direction + view))), lerp(12, 120, polish))
                    * lerp(.035, .65, polish * polish) * light.shadowAttenuation;
                float rim = pow(1-saturate(dot(n,view)), 4) * .055;
                float3 baseColor = input.color.rgb * _Tint.rgb;
                return half4(baseColor * lighting * light.color + spec + rim, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}

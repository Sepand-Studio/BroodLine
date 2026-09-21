Shader "Broodline/Creature"
{
    // Phase 9 design §3.6: half-Lambert through a warm key and a cool fill,
    // a rim so a body separates from a near-white card, a two-tone body
    // whose underside darkens (bible 10.4's value structure, and what Pale
    // needs to exist on paper), a desaturation float for damage-as-posture
    // (bible 10.7), and a tint. Hand-written so it is text under review.
    Properties
    {
        _BaseColor ("Base", Color) = (1, 1, 1, 1)
        _UnderColor ("Underside", Color) = (0.5, 0.5, 0.5, 1)
        _KeyColor ("Key light", Color) = (1.0, 0.97, 0.9, 1)
        _FillColor ("Fill light", Color) = (0.66, 0.7, 0.84, 1)
        _RimColor ("Rim", Color) = (1, 1, 1, 1)
        _RimPower ("Rim power", Range(1, 8)) = 3
        _RimStrength ("Rim strength", Range(0, 1)) = 0.35
        _Desaturate ("Desaturate", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _UnderColor, _KeyColor, _FillColor, _RimColor, _Tint;
                float _RimPower, _RimStrength, _Desaturate;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes a)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction) * 0.5 + 0.5);
                float3 ramp = lerp(_FillColor.rgb, _KeyColor.rgb, smoothstep(0.25, 0.85, ndl));
                float3 body = lerp(_BaseColor.rgb, _UnderColor.rgb, saturate(-n.y));
                float3 col = body * ramp * L.color;
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim;
                float grey = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, grey.xxx, _Desaturate);
                col *= _Tint.rgb;
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback Off
}

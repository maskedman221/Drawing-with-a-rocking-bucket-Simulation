Shader "Fluid/WetPlanePaint"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (1, 0.08, 0.04, 0.9)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _Fresnel ("Wet Edge Highlight", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BaseColor;
            float _Smoothness;
            float _Fresnel;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();

                float ndl = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = _BaseColor.rgb * input.color.rgb * (0.34 + ndl * 0.66) * mainLight.color;

                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), lerp(24.0, 160.0, _Smoothness));
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 4.0) * _Fresnel;
                float3 highlight = (spec * 0.45 + fresnel) * mainLight.color;

                float alpha = saturate(_BaseColor.a * input.color.a);
                return half4(diffuse + highlight, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "Fluid/SurfaceMesh"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.18, 0.5, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BaseColor;
            float _Smoothness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();

                float diffuse = saturate(dot(normalWS, mainLight.direction));
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float specularPower = lerp(12.0, 96.0, _Smoothness);
                float specular = pow(saturate(dot(normalWS, halfDir)), specularPower) * _Smoothness;

                float3 ambient = SampleSH(normalWS) * 0.45;
                float3 lit = _BaseColor.rgb * (ambient + mainLight.color * (0.35 + diffuse * 0.65));
                lit += mainLight.color * specular * 0.25;

                return half4(lit, _BaseColor.a);
            }

            ENDHLSL
        }
    }
}

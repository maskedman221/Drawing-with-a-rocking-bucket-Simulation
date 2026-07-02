Shader "Fluid/WorldBlob"
{
    Properties
    {
        _Radius ("Radius", Float) = 0.08
        _Color ("Color", Color) = (0.18, 0.5, 1, 1)
        _Softness ("Softness", Range(0.01, 1)) = 0.45
        _Opacity ("Opacity", Range(0, 1)) = 0.98
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> Positions;

            float _Radius;
            float4 _Color;
            float _Softness;
            float _Opacity;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 localPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 center = Positions[v.instanceID].xyz;
                float2 localPos = v.vertex.xy * 2.0;

                float3 centerVS = TransformWorldToView(center);
                float3 viewPos = centerVS + float3(localPos * _Radius, 0.0);

                o.pos = TransformWViewToHClip(viewPos);
                o.localPos = localPos;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float r = length(i.localPos);
                if (r > 1.0)
                {
                    discard;
                }

                float edgeStart = saturate(1.0 - _Softness);
                float alpha = 1.0 - smoothstep(edgeStart, 1.0, r);

                float3 normalVS = normalize(float3(-i.localPos.x, -i.localPos.y, sqrt(saturate(1.0 - r * r))));
                float3 lightVS = normalize(float3(0.35, 0.7, 0.45));
                float lighting = 0.62 + 0.38 * saturate(dot(normalVS, lightVS));

                return half4(_Color.rgb * lighting, alpha * _Color.a * _Opacity);
            }

            ENDHLSL
        }
    }
}

Shader "Hidden/Fluid/SceneDepth"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ReversedZ"

            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask R
            Blend One One
            BlendOp Max

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float depth : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.depth = o.positionCS.z / o.positionCS.w;
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                return i.depth;
            }

            ENDHLSL
        }

        Pass
        {
            Name "NormalZ"

            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask R
            Blend One One
            BlendOp Min

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float depth : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.depth = o.positionCS.z / o.positionCS.w;
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                return i.depth;
            }

            ENDHLSL
        }
    }
}

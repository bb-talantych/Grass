Shader "_BB/GrassInstancing"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            StructuredBuffer<float3> positionBuffer;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Color;

            float _OffsetY;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;

                float3 offset = positionBuffer[id];
                offset.y += _OffsetY;
                float4 worldPos = float4(v.vertex.xyz + offset, 1.0);
                o.vertex = UnityObjectToClipPos(worldPos);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 mainTex = tex2D(_MainTex, i.uv);
                return mainTex * _Color;
            }
            ENDCG
        }
    }
}


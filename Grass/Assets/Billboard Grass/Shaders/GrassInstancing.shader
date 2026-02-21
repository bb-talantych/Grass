Shader "_BB/GrassInstancing"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityPBSLighting.cginc"

            struct GrassData 
            {
                float3 position;
                float2 uv;
                float displacement;
                float normalizedHeight;
            };

            StructuredBuffer<GrassData> grassDataBuffer;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _Rotation, _Protrusion, _LowGrassAnimationSpeed, _HighGrassAnimationSpeed;
            float3 _ProtrusionDir, _WindDir;

            #define OFFSET_Y 0.5f

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

                float normalizedHeight : TEXCOORD1;
            };

            float3 RotateAroundY(float3 _vertex, float _deg)
            {
                float rad = radians(_deg);
                float cosine = cos(rad);
                float sine = sin(rad);

                float3 rotatedVertex = _vertex;
                rotatedVertex.x = _vertex.x * cosine - _vertex.z * sine;
                rotatedVertex.z = _vertex.x * sine + _vertex.z * cosine;

                return rotatedVertex;
            }
            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;

                float3 offset = grassDataBuffer[id].position;
                float displacement = grassDataBuffer[id].displacement;
                float normalizedHeight = grassDataBuffer[id].normalizedHeight;

                #if defined (OFFSET_Y)
                    offset.y += OFFSET_Y;
                #endif
                if(_Protrusion != 0)
                {
                    float3 rotatedProtrusionDir = RotateAroundY(_ProtrusionDir, _Rotation);
                    offset += rotatedProtrusionDir * _Protrusion;
                }

                offset.y -= (displacement * (1 - v.uv.y));

                float animationSpeed = lerp(_LowGrassAnimationSpeed, _HighGrassAnimationSpeed, normalizedHeight);
                float normalizedAnimationTime = sin(_Time.y * animationSpeed) * 0.5 + 0.5;
                float animationTime = lerp(-0.47, 1, normalizedAnimationTime);
                offset += normalize(_WindDir) * animationTime * v.uv.y;

                float3 rotatedVertex = RotateAroundY(v.vertex.xyz, _Rotation);
                float4 worldPos = float4(rotatedVertex + offset, 1.0f);
                o.vertex = UnityObjectToClipPos(worldPos);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalizedHeight = normalizedHeight;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 mainTex = tex2D(_MainTex, i.uv);

                if(mainTex.a < 0.1)
                    discard;

                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float ndotl = DotClamped(lightDir, normalize(float3(0, 1, 0)));

                float4 finalColor = lerp(0, mainTex, i.uv.y);
                float4 topColor = lerp(mainTex, float4(1, 0.8, 0, 1), i.normalizedHeight);
                finalColor = lerp(finalColor, topColor, i.uv.y);
                finalColor = finalColor * ndotl;

                return finalColor;
            }
            ENDCG
        }
    }
}


Shader "_BB/3D Grass Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0)
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

            #pragma target 4.6

            #include "UnityPBSLighting.cginc"

            struct GrassData3D 
            {
                float3 position;
                float2 uv;
                float displacement;
            };
            StructuredBuffer<GrassData3D> grassDataBuffer;

            float3 _BaseColor, _TipColor;
            float4 _Color;

            float2 _WindDir;
            sampler2D _WindTex;
            float  _DisplacementStrength;

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

                float normalizedDisplacement : TEXCOORD1;
                float2 grassUV : TEXCOORD2;
            };

            float hash(uint n) 
            {
                // integer hash copied from Hugo Elias
	            n = (n << 13U) ^ n;
                n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
                return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
            }
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

                // get values from command buffer
                float3 offset = grassDataBuffer[id].position;
                float2 grassUV = grassDataBuffer[id].uv;
                float normalizedDisplacement = grassDataBuffer[id].displacement;
                float displacement = normalizedDisplacement * _DisplacementStrength;                

                // adjust vertex position
                offset.y += (displacement * v.uv.y);

                // animation
                float4 windSampleUV = float4(grassUV, 0, 0);
                float4 windTex = tex2Dlod(_WindTex, windSampleUV);
                float windValue = LinearRgbToLuminance(windTex.rgb);
                float windPower = lerp(-0.47, 1, windValue)
                * lerp(1, 0.65, normalizedDisplacement);

                offset.xz += _WindDir * windPower * v.uv.y;

                // calcuate vertex world position and distance to Camera
                int seed = 3235523;
                float rotAngle = hash(seed + (grassUV.x * 1000 + grassUV.y * 10000)) * 360;
                float3 rotatedVertex = RotateAroundY(v.vertex.xyz, rotAngle);
                float4 worldPos = float4(rotatedVertex + offset, 1.0f);

                // sending values to fragment
                o.vertex = UnityObjectToClipPos(worldPos);
                o.uv = v.uv;
                o.normalizedDisplacement = normalizedDisplacement;
                o.grassUV = grassUV;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float ndotl = DotClamped(lightDir, normalize(float3(0, 1, 0)));

                float3 finalColor = _BaseColor;

                float3 tipColor = lerp(_BaseColor, _TipColor, i.normalizedDisplacement);
                float tipUV = i.uv.y * (1.37f * i.normalizedDisplacement);
                finalColor = lerp(finalColor, tipColor, tipUV);

                float3 AO_Color = float3(0, 0, 0);
                float AO_UV = (1 - i.uv.y) * (1 - i.uv.y) * (1 - i.uv.y) * 1.47f;
                finalColor = lerp(finalColor, AO_Color, AO_UV);

                finalColor = finalColor * ndotl;

                finalColor.rgb = lerp(finalColor, _Color, _Color.a);

                //finalColor.rgb = tex2D(_WindTex, i.grassUV).r;

                return float4(finalColor, 1);
            }
            ENDCG
        }
    }
}


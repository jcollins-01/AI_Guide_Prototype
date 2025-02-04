Shader "AlignedGames/SpecialToon"
{
    Properties
    {
        _TextureMap ("Texture", 2D) = "" {}
        _TileAmount ("Scale Multiplier", float) = 1
        _ShadowBrightness ("Shadow Brightness", Range(0, 1)) = 0
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0, 5)) = 1 // Control brightness
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1) // Rim color
        _RimPower ("Rim Power", Range(1, 15)) = 5 // Rim intensity
    }

    SubShader
    {
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain
            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"

            sampler2D _TextureMap;
            float _TileAmount;
            float _ShadowBrightness;
            fixed4 _Color;
            float _Smoothness; // Smoothness variable
            float _Brightness; // Brightness variable
            fixed4 _RimColor;  // Rim color variable
            float _RimPower;   // Rim power/intensity variable

            struct SHADERDATA
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 _ShadowCoord : TEXCOORD1;
                float3 worldNormal : NORMAL;  // To calculate rim lighting
                float3 worldPos : TEXCOORD2;  // To calculate view direction
            };

            float4 ComputeScreenPos(float4 p)
            {
                float4 o = p * 0.5;
                return float4(float2(o.x, o.y * _ProjectionParams.x) + o.w, p.zw);
            }

            SHADERDATA VSMain(float4 vertex : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0)
            {
                SHADERDATA vs;
                vs.position = UnityObjectToClipPos(vertex);
                vs.uv = uv;
                vs._ShadowCoord = ComputeScreenPos(vs.position);
                vs.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, normal)); // Transform normal to world space
                vs.worldPos = mul(unity_ObjectToWorld, vertex).xyz; // Calculate world position
                return vs;
            }

            fixed4 PSMain(SHADERDATA ps) : SV_TARGET
            {
                fixed4 col = tex2D(_TextureMap, ps.uv * _TileAmount) * _Color;

                // Adjust brightness
                col.rgb *= _Brightness;

                // Shadowing logic
                float shadow = step(0.5, SHADOW_ATTENUATION(ps));
                col.rgb = lerp(col.rgb * float4(_ShadowBrightness, _ShadowBrightness, _ShadowBrightness, 1), col.rgb, shadow);

                // Adjust smoothness
                col.rgb = lerp(col.rgb, col.rgb * (1 - _Smoothness), _Smoothness); // Smoothness effect

                // Rim lighting effect
                float3 viewDir = normalize(_WorldSpaceCameraPos - ps.worldPos); // Calculate view direction
                float rimFactor = 1.0 - saturate(dot(viewDir, ps.worldNormal)); // Dot product for rim effect
                float rimIntensity = pow(rimFactor, _RimPower); // Adjust rim intensity
                col.rgb += _RimColor.rgb * rimIntensity; // Add rim color to final output

                return col;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain

            float4 VSMain(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            float4 PSMain(float4 vertex : SV_POSITION) : SV_TARGET
            {
                return 0;
            }
            ENDCG
        }
    }
}

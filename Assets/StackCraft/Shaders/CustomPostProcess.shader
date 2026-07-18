Shader "Crying Snow/StackCraft/CustomPostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VignettePower ("Vignette Power", Range(0, 3)) = 1.2
        _GrayscaleAmount ("Grayscale Amount", Range(0, 1)) = 1.0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float _VignettePower;
            float _GrayscaleAmount;

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Sample the Screen
                fixed4 col = tex2D(_MainTex, i.uv);

                // 2. Grayscale Math (Luma Conversion)
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float3 grayCol = float3(lum, lum, lum);
                col.rgb = lerp(col.rgb, grayCol, _GrayscaleAmount);

                // 3. Vignette Math
                float2 dist = i.uv - 0.5;
                float len = length(dist);
                float vignette = smoothstep(0.8, 0.2, len * _VignettePower);
                
                col.rgb *= vignette;

                return col;
            }
            ENDCG
        }
    }
}

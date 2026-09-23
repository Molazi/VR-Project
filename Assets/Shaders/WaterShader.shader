Shader "Custom/WaterSimple"
{
    Properties
    {
        _Color ("÷вет воды", Color) = (0.3, 0.5, 0.9, 0.8)
        _WaveSpeed ("—корость волн", Float) = 0.5
        _WaveScale ("„астота волн", Float) = 5.0
        _FresnelPower ("—ила ‘ренел€", Float) = 2.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            float4 _Color;
            float _WaveSpeed;
            float _WaveScale;
            float _FresnelPower;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ¬олнова€ р€бь через изменение альфы по UV и времени
                float wave = sin(i.uv.x * _WaveScale + _Time.y * _WaveSpeed) * 
                             cos(i.uv.y * _WaveScale + _Time.y * _WaveSpeed);
                float alpha = _Color.a * (0.7 + 0.3 * wave); // р€бь вли€ет на прозрачность

                float3 worldViewDir = normalize(i.viewDir);
                float fresnel = pow(1 - abs(dot(float3(0,1,0), worldViewDir)), _FresnelPower);
                alpha *= fresnel;

                fixed4 col = _Color;
                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}
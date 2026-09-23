Shader "Custom/WaterSimple"
{
    Properties
    {
        _Color ("Цвет воды", Color) = (0.3, 0.5, 0.9, 0.8)
        _WaveSpeed ("Скорость ряби", Float) = 0.5
        _WaveScale ("Частота ряби", Float) = 5.0
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
            };

            float4 _Color;
            float _WaveSpeed;
            float _WaveScale;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Анимированная рябь меняет прозрачность
                float wave = sin(i.uv.x * _WaveScale + _Time.y * _WaveSpeed) * 
                             cos(i.uv.y * _WaveScale + _Time.y * _WaveSpeed);
                // Альфа всегда не ниже 0.4, чтобы вода точно была видна
                float alpha = _Color.a * (0.6 + 0.4 * wave);

                fixed4 col = _Color;
                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
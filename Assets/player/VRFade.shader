Shader "VR/FadeOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0
    }

    SubShader
    {
        // Рендерим ПОВЕРХ всего
        Tags { "Queue" = "Overlay+1000" "RenderType" = "Transparent" }

        Pass
        {
            // Без записи в Z-буфер
            ZWrite Off
            ZTest Always

            // Рендерим ВНУТРЕННЮЮ сторону сферы (Front = отсекаем переднюю)
            Cull Front

            // Прозрачность
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            fixed4 _Color;
            float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, _Alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
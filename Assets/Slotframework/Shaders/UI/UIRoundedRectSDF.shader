Shader "UI/RoundedRectSDF"
{
    Properties
    {
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [HideInInspector] _RectLbrt ("Rect L B R T", Vector) = (0,0,0,0)
        [HideInInspector] _Radii ("R TR BR TL BL packed as TR BR TL BL", Vector) = (0,0,0,0)
        [HideInInspector] _EdgeAaScale ("Edge AA Scale", Float) = 1

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                fixed4 color    : COLOR;
                float2 uv0      : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex           : SV_POSITION;
                fixed4 color            : COLOR;
                float2 uv0              : TEXCOORD0;
                float4 worldPosition    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;
            float4 _RectLbrt;
            float4 _Radii;
            float _EdgeAaScale;

            float sdRoundBox(float2 p, float2 b, float4 r)
            {
                r.xy = (p.x > 0.0) ? r.xy : r.zw;
                r.x = (p.y > 0.0) ? r.x : r.y;
                float2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv0 = v.uv0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float l = _RectLbrt.x;
                float b = _RectLbrt.y;
                float r = _RectLbrt.z;
                float t = _RectLbrt.w;

                float2 p = float2(lerp(l, r, i.uv0.x), lerp(b, t, i.uv0.y));
                float2 c = float2(0.5 * (l + r), 0.5 * (b + t));
                float2 pc = p - c;
                float2 halfSize = float2(0.5 * (r - l), 0.5 * (t - b));

                float4 rad = _Radii;
                float d = sdRoundBox(pc, halfSize, rad);

                float aa = fwidth(d) * max(_EdgeAaScale, 0.35);
                float fill = 1.0 - smoothstep(-aa, aa, d);

                fixed4 col = i.color;
                col.a *= saturate(fill);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}

//
// _IslandIndex仅呈现一个 Square，或,
// _FullRender渲染全彩色图像，或
//
Shader "Black/Single Island Shader"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _A1Tex ("A1 Texture", 2D) = "white" {}
        _A2Tex ("A2 Texture", 2D) = "white" {}
        _PaletteTex ("Palette Texture", 2D) = "white" {}
        _FullRender ("Full Render", Float) = 0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        
    }
    
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
        Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency
        ColorMask [_ColorMask]
        
//https://docs.unity.cn/cn/2021.1/Manual/SL-ColorMask.html
// 对此通道启用仅写入 RGB 通道，这会禁用向 Alpha 通道写入
//ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            //#pragma multi_compile_fog

            #include "UnityCG.cginc"
            
            

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex; // 我不使用它，但如果我没有它，我会收到一条警告消息，所以我不想看到它，所以我把它放进去。
            sampler2D_float _A1Tex;
            sampler2D_float _A2Tex;
            sampler2D_float _PaletteTex;
            
            float4 _A1Tex_ST;
            float4 _Palette[64];
            int _IslandIndex;
            float _FullRender;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _A1Tex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float a1 = tex2D(_A1Tex, i.uv).a * 255.0f;
                float a2 = tex2D(_A2Tex, i.uv).a * 255.0f;
                
                float paletteIndex = fmod(a1, 64.0f);
                int islandIndex = (int)((a1 / 64.0f) + (a2 * 4.0f));
                
                //float4 col = _Palette[paletteIndex];
                float2 paletteUv;
                paletteUv.x = (paletteIndex + 0.5f) / 64.0f;
                paletteUv.y = 0;
                float4 col = tex2D(_PaletteTex, paletteUv);
                
                // FULL VERSION
                //col.a = lerp(lerp(islandIndex <= _IslandIndex ? 1 : 0, 1 - abs(islandIndex - _IslandIndex), _SingleIsland), 1, _FullRender);
                
                // OPTIMIZED VERSION
                col.a = 1 - saturate(abs(islandIndex - _IslandIndex)) + _FullRender;
                
                return col;
            }
            ENDCG
        }
    }
}

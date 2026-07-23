Shader "Custom/UI/CardSplash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Card Frame (RGBA)", 2D) = "white" {}
        _MaskTex ("Splash Mask (R)", 2D) = "white" {}
        _SplashTex ("Splash Art", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SplashTiling ("Splash Tiling/Offset (xy tiling, zw offset)", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);   SAMPLER(sampler_MaskTex);
            TEXTURE2D(_SplashTex); SAMPLER(sampler_SplashTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _SplashTiling;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 frame = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half mask   = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).r;

                float2 splashUV = IN.uv * _SplashTiling.xy + _SplashTiling.zw;
                half4 splash = SAMPLE_TEXTURE2D(_SplashTex, sampler_SplashTex, splashUV);

                half4 col;
                col.rgb = lerp(frame.rgb, splash.rgb, mask * splash.a);
                col.a   = frame.a; // sylwetka/kontur karty zawsze z ramki
                col *= IN.color;

                return col;
            }
            ENDHLSL
        }
    }
}

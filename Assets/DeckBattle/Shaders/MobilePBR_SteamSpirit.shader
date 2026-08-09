Shader "SteamSpirit/UnlitCelShade"
{
    // ============================================================
    // Unlit + cel-shade pod URP (hand-written HLSL, bez Shader Graph)
    //
    // Założenia projektowe:
    // - Brak PBR - żadnego BRDF, Fresnela, metal/roughness. To jest
    //   celowo "unlit" w sensie art-style: światło liczone jest tylko
    //   po to, żeby wybrać pasmo na ramp teksturze, a nie żeby
    //   symulować fizyczne odbicie.
    // - 1 main light (kierunkowe, z cieniami) napędza NdotL -> proceduralne pasmo.
    // - Brak ambient/SH - cień "ambientowy" to po prostu _ShadowColor, więc
    //   dostrajasz go suwakiem zamiast malować teksturę rampy.
    // - Cel-shade liczony w 100% w shaderze (3 kolory + 2 progi + smoothstep),
    //   zero dodatkowej tekstury rampy - jeden sample mniej niż poprzednia wersja.
    // - Emission to teraz stały kolor (_EmissionColor) bez maski teksturowej -
    //   dodaje blask na całym meshu jednolicie. Jeśli potrzebujesz emisji tylko
    //   na fragmencie modelu (np. lampka, rura pary), rozważ osobny prosty mesh/
    //   submesh z tym materiałem zamiast maski w kanale tekstury.
    // - Outline jako osobny pass (inverted hull), niezmieniony względem
    //   poprzedniej wersji - w pełni opcjonalny toggle _OUTLINE_ON,
    //   z fixem na pękanie na UV seamach przez _OUTLINE_SMOOTH_NORMALS
    //   (patrz BakeSmoothNormalsToVertexColor.cs).
    // - Koszt fragmentu: 1 sample tekstury (_BaseMap) - to najtańszy wariant
    //   z całej serii, PBR liczył ich 5-6, poprzedni cel-shade z ramp texturą 2.
    // ============================================================

    Properties
    {
        [MainTexture] _BaseMap ("Albedo (RGB) Alpha (A)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Brightness ("Base Map Brightness", Range(0,4)) = 1.0

        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)

        _ShadowColor ("Shadow Color Tint", Color) = (0.4, 0.4, 0.55, 1)
        _MidColor    ("Midtone Color Tint", Color) = (0.75, 0.75, 0.82, 1)
        _LightColor  ("Light Color Tint", Color) = (1, 1, 1, 1)
        _ShadowThreshold ("Shadow -> Midtone Threshold", Range(0,1)) = 0.35
        _LightThreshold  ("Midtone -> Light Threshold", Range(0,1)) = 0.65
        _RampSmoothness ("Band Edge Smoothness", Range(0.001, 0.3)) = 0.05

        [Toggle(_OUTLINE_ON)] _OutlineEnabled ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.08, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.01
        [Toggle(_OUTLINE_SMOOTH_NORMALS)] _OutlineSmoothNormals ("Outline: Use Baked Smooth Normals (vertex color)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _OUTLINE_SMOOTH_NORMALS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct OutlineAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // Uśredniona normalna sprzed podziału na UV seamy, upakowana [0,1]
                // przez BakeSmoothNormalsToVertexColor.cs. Bez tego bake'u kanał
                // będzie pusty/biały - wtedy używamy fallbacku na zwykły normalOS.
                half4 color       : COLOR;
            };

            struct OutlineVaryings
            {
                float4 positionHCS : SV_POSITION;
            };

            half4 _OutlineColor;
            float _OutlineWidth;

            OutlineVaryings vertOutline(OutlineAttributes IN)
            {
                OutlineVaryings OUT;

                #if defined(_OUTLINE_ON)
                    #if defined(_OUTLINE_SMOOTH_NORMALS)
                        // Rozpakowanie [0,1] -> [-1,1] uśrednionej normalnej z vertex color -
                        // ciągła na szwach, więc outline się tam nie rozjeżdża
                        float3 extrudeDir = normalize(IN.color.rgb * 2.0 - 1.0);
                    #else
                        float3 extrudeDir = normalize(IN.normalOS);
                    #endif
                    float3 positionOS = IN.positionOS.xyz + extrudeDir * _OutlineWidth;
                #else
                    float3 positionOS = IN.positionOS.xyz;
                #endif

                OUT.positionHCS = TransformObjectToHClip(positionOS);
                return OUT;
            }

            half4 fragOutline(OutlineVaryings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "UnlitCelShade"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Tylko cienie głównego światła - to jedyna dynamiczna dana, jakiej
            // potrzebuje cel-shade. Brak LIGHTMAP_ON/SH - unlit nie korzysta z ambientu.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Brightness;
                float4 _EmissionColor;
                half4 _ShadowColor;
                half4 _MidColor;
                half4 _LightColor;
                float _ShadowThreshold;
                float _LightThreshold;
                float _RampSmoothness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- Sample tekstury (tylko _BaseMap - 1 sample w całym fragmencie) ---
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                baseSample.rgb *= _Brightness; // rozjaśnienie/przyciemnienie całej głównej tekstury

                half3 albedo = baseSample.rgb;

                half3 N = normalize(IN.normalWS);

                // --- Main light (tylko po to, żeby wybrać pasmo na rampie) ---
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half3 L = normalize(mainLight.direction);
                half NdotL = dot(N, L) * 0.5 + 0.5; // wrap lighting - miększe przejście na sylwetce niż saturate(NdotL)

                // Cień rzucany przez inne obiekty też wpływa na pasmo rampy
                half shadowedNdotL = NdotL * mainLight.shadowAttenuation;

                // --- Proceduralne pasma cel-shade (bez tekstury) ---
                // Dwa progi dzielą [0,1] na 3 strefy: cień -> półcień -> światło.
                // smoothstep na obu progach z tą samą szerokością (_RampSmoothness)
                // daje miękkie, symetryczne przejścia bez artefaktów na krawędziach pasm.
                half bandShadowToMid = smoothstep(_ShadowThreshold - _RampSmoothness, _ShadowThreshold + _RampSmoothness, shadowedNdotL);
                half bandMidToLight  = smoothstep(_LightThreshold - _RampSmoothness, _LightThreshold + _RampSmoothness, shadowedNdotL);

                half3 rampColor = lerp(_ShadowColor.rgb, _MidColor.rgb, bandShadowToMid);
                rampColor = lerp(rampColor, _LightColor.rgb, bandMidToLight);

                half3 shaded = albedo * rampColor;

                // --- Emission: stały kolor, bez maski teksturowej ---
                half3 emission = _EmissionColor.rgb;

                half3 finalColor = shaded + emission;

                return half4(finalColor, baseSample.a);
            }
            ENDHLSL
        }

        // Standardowe passy pomocnicze URP - potrzebne pod cienie i depth prepass
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Unlit"
}

Shader "Three Souls/Village/Pixel Grass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _GrassColor ("Grass", Color) = (0.40, 0.61, 0.42, 1)
        _ShadowColor ("Grass Shadow", Color) = (0.30, 0.49, 0.32, 1)
        _HighlightColor ("Grass Highlight", Color) = (0.53, 0.70, 0.44, 1)
        _BorderStrength ("Tile Edge", Range(0, 0.4)) = 0.14
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXY : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _GrassColor;
                half4 _ShadowColor;
                half4 _HighlightColor;
                half4 _Color;
                half _BorderStrength;
            CBUFFER_END

            float Hash(float2 value)
            {
                float3 p = frac(float3(value.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.worldXY = TransformObjectToWorld(input.positionOS).xy;
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Grid 0.5 / PPU 64: 32 crisp source pixels per tile.
                float2 pixel = floor(input.worldXY * 64.0 + 0.001);
                float2 cell = floor(pixel / 32.0);
                float2 local = pixel - cell * 32.0;
                half3 color = _GrassColor.rgb * lerp(0.94, 1.05, Hash(cell));
                float2 patch = floor(local / 8.0);
                float seed = Hash(patch + cell * 7.17);
                float2 offset = floor(float2(seed, Hash(patch + cell * 3.7 + 13.0)) * 4.0) + 2.0;
                float2 blade = local - patch * 8.0 - offset;
                bool stem = blade.x == 0 && blade.y >= 0 && blade.y <= 2;
                bool leaf = (blade.x == -1 && blade.y == 2) || (blade.x == 1 && blade.y == 1);
                if (seed < 0.56 && (stem || leaf))
                {
                    color = stem ? _ShadowColor.rgb : _HighlightColor.rgb;
                }
                if (Hash(pixel + 71.0) > 0.99)
                {
                    color = lerp(color, _HighlightColor.rgb, 0.65);
                }
                // Broken one-pixel borders keep individual tiles readable.
                if ((local.x == 0 || local.y == 0) && Hash(pixel + 3.0) > 0.18)
                {
                    color *= 1.0 - _BorderStrength;
                }
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
                return half4(color * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}

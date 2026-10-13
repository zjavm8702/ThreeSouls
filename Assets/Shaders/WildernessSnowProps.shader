Shader "Three Souls/Wilderness Snow Props"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyTolerance ("Background tolerance", Range(0,0.05)) = 0.008
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex SnowVertex
            #pragma fragment SnowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _KeyTolerance;
            CBUFFER_END
            Varyings SnowVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 SnowFragment(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // 원본의 단색 눈 배경 RGB(203,245,237)만 화면에서 제거합니다. PNG는 유지합니다.
                half3 key = half3(203.0,245.0,237.0) / 255.0;
                #ifndef UNITY_COLORSPACE_GAMMA
                    key = SRGBToLinear(key);
                #endif
                clip(distance(source.rgb, key) - _KeyTolerance);
                return source * input.color;
            }
            ENDHLSL
        }
    }
}

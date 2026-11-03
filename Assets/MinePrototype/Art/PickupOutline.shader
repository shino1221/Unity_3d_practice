Shader "MinePrototype/Pickup Outline"
{
    Properties
    {
        [HDR] _Color ("Outline Color", Color) = (1.5, 1.5, 1.5, 1)
        _Width ("Outline Width", Float) = 0.009
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Width;
            CBUFFER_END
            float4 Vert(float3 position : POSITION, float3 normal : NORMAL) : SV_POSITION
            {
                return TransformObjectToHClip(position + normal * _Width);
            }
            half4 Frag() : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}

Shader "RaftSharkDive/Grass Indirect"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.16, 0.38, 0.12, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4x4> _InstanceMatrices;
            float4x4 _RootMatrix;
            float4 _BaseColor;
            float3 _PlayerPosition;
            float _HideDistance;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4x4 model = mul(_RootMatrix, _InstanceMatrices[input.instanceID]);
                float3 worldPosition = mul(model, input.positionOS).xyz;
                float2 delta = worldPosition.xz - _PlayerPosition.xz;
                if (dot(delta, delta) < _HideDistance * _HideDistance) worldPosition.y -= 1000.0;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.normalWS = normalize(mul((float3x3)model, input.normalOS));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half light = saturate(dot(normalize(input.normalWS), normalize(float3(0.35, 0.8, 0.25)))) * 0.5 + 0.5;
                half3 color = _BaseColor.rgb * light;
                return half4(MixFog(color, input.fogFactor), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}

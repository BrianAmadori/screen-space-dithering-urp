Shader "Hidden/Post FX/Grain Generator"
{
    CGINCLUDE
        
        #pragma exclude_renderers d3d11_9x
        #pragma target 3.0
        #include "UnityCG.cginc"
        #include "GrainUtils.cginc"
        #include "Common_Grain.cginc"
    
        float _Phase;
    
        float4 FragGrain(VaryingsDefault i) : SV_Target
        {
            float grain = Step3BW(i.uv * float2(192.0, 192.0), _Phase);
            return float4(grain.xxx, 1.0);
        }

        float4 FragGrainColored(VaryingsDefault i) : SV_Target
        {
            float3 grain = Step3(i.uv * float2(192.0, 192.0), _Phase);
            return float4(grain, 1.0);
        }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragGrain

            ENDCG
        }

        Pass
        {
            CGPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragGrainColored

            ENDCG
        }
    }
}

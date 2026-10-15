Shader "Campus/Teaching Prop Foreground"
{
    Properties { _BaseColor ("Colour",Color)=(1,1,1,1) _BaseMap ("Texture",2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        Pass
        {
            ZTest Always ZWrite Off Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float3 n:TEXCOORD0; float2 uv:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a) { V v; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v); v.p=TransformObjectToHClip(a.p.xyz); v.n=TransformObjectToWorldNormal(a.n); v.uv=a.uv; return v; }
            half4 frag(V v):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v); half light=.55+.45*saturate(dot(normalize(v.n),normalize(float3(-.3,.8,-.5)))); half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv); return half4(_BaseColor.rgb*tex.rgb*light,_BaseColor.a*tex.a); }
            ENDHLSL
        }
    }
}

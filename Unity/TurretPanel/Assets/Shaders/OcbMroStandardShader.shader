Shader "OCBNET/MroStandardShader"
{
    Properties
    {
        _Color("Albedo Color", Color) = (1,1,1,0.5)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Alpha("Global Alpha", Range(0, 1)) = 0
        // Use one single texture to optimize memory
        // Red Channel: Metalicness (white = 100% Metallic)
        // Green Channel: Roughness (Invert of Smoothness)
        // Blue Channel: Ambient occlusion
        _MROMap("Met(R) Rough(G) AO(B)", 2D) = "white" {}
        // Scales applied to MRO (RGB) channels
        _Metallic("Metallic", Range(0,1)) = 1
        _Glossiness("Roughness", Range(0,1)) = 1
        _Occlusion("Occlusion", Range(0,1)) = 1
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("NormalScale", Float) = 1.0
        _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _EmissionMap("Emission", 2D) = "black" {}

    }
    SubShader
    {
        Tags { 
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }
        // Blend One One
        LOD 200

        cull Back
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
        // Include the actual surface shader
        #include "Surface/MroStandardSurface.cginc"
        ENDCG
    }
    FallBack "Diffuse"
}

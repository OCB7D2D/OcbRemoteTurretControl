Shader "OCBNET/MroHoloScreenShader"
{
    Properties
    {
        _Mode("Mode", Int) = 0
        _Seed("Seed", Range(500, 800)) = 625
        _EffectColor1("Effect Color 1", Color) = (1,1,1,0.5)
        _EffectColor2("Effect Color 2", Color) = (1,1,1,0.5)
        _Color("Tint Color", Color) = (1,1,1,1)
        // Cannot update color without changing all tints
        _ScreenColor("Albedo Color", Color) = (1,1,1,0.5)
        _ScreenSize("Screen Pixels", Vector) = (640,360,0,0)
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
        _EmissionMap("Emission", 2D) = "clear" {}

    }
    SubShader
    {
        Tags {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        // Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
        // Blend One OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha

        LOD 200

        cull off
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        // Also enabling alpha in order to get transparency working
        #pragma surface surf Standard fullforwardshadows alpha:premul
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
        // Include the actual surface shader
        #include "Surface/MroTvScreenSurface.cginc"
        ENDCG
    }
    FallBack "OCBNET/MroStandardShader"
}

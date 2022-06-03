#include "Include/MroIncludeBase.cginc"
#include "Include/MroIncludeSurface.cginc"

int _Mode;
float4 _ScreenColor;
float4 _EffectColor1;
float4 _EffectColor2;

OCB_BASE_UNIFORMS
OCB_MRO_UNIFORMS

struct Input
{
    OCB_MRO_INPUTS
    OCB_BASE_INPUTS
};

#include "Effect/StaticNoise.cginc"
#include "Effect/MatrixNoise.cginc"

float4 GetColor(int mode, float2 uv)
{
    switch (mode)
    {
        // Render main texture but fully opaque
        case 1: return float4(tex2D(_MainTex, uv).rgb, 1);
        case 2: return StaticNoise(uv, _Time.w, _EffectColor1);
        case 3: return MatrixNoise(uv, _Time.w, _EffectColor2,
            128, 20, float2(128, 38));
        default: return tex2D(_MainTex, uv);
    }
}

void surf (Input IN, inout SurfaceOutputStandard o)
{
    // Apply MRO material
    OCB_MRO_SURF(IN, o);
    // Apply the normal map
    OCB_NORMALS_SURF(IN, o);
    // Get color by our tv screen mode function
    float4 screen = GetColor(_Mode, IN.uv_MainTex);
    // Take albedo from texture and tint it with color
    // Note: TvScreen is never tinted by main color
    o.Albedo = screen.rgb * _ScreenColor.rgb
        * screen.a * _ScreenColor.a;
    // Emission comes from effect tinted by screen color
    o.Emission = max(screen.rgb * _ScreenColor.a,
            tex2D(_EmissionMap, IN.uv_EmissionMap) * _EmissionColor.a)
        * _EmissionColor.rgb * _ScreenColor.rgb;
    // Multiply with global value
    o.Alpha = _ScreenColor.a * _Alpha;
}

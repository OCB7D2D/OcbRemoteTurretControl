#include "Include/MroIncludeBase.cginc"
#include "Include/MroIncludeSurface.cginc"

OCB_BASE_UNIFORMS
OCB_MRO_UNIFORMS

struct Input
{
    OCB_MRO_INPUTS
    OCB_BASE_INPUTS
};

void surf (Input IN, inout SurfaceOutputStandard o)
{
    float4 mro = tex2D(_MROMap, IN.uv_MainTex);
    // Take albedo from texture and tint it with color
    o.Albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
    // Metallic comes from blue channel tinted by slider variables
    o.Metallic = mro.r * _Metallic;
    // Smoothness comes from blue channel tinted by slider variables
    o.Smoothness = 1 - mro.g * _Glossiness;
    // Ambient Occlusion comes from green channel
    o.Occlusion = mro.b * _Occlusion;
    // Normal comes from a bump map
    o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
    // Emission comes from a texture tinted by color
    o.Emission = tex2D(_EmissionMap, IN.uv_EmissionMap) * _EmissionColor;
    // Multiply with global value
    o.Alpha *= _Alpha;
}

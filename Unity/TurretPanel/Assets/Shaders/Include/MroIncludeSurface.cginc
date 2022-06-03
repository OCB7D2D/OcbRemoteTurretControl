#define OCB_MRO_UNIFORMS \
    sampler2D _MROMap; \
    half _Glossiness; \
    half _Metallic; \
    half _Occlusion; \

#define OCB_MRO_INPUTS \
    float2 uv_MROMap; \

#define OCB_MRO_SURF(IN, o) \
{ \
    /* Use one texture containing all values */ \
    float4 mro = tex2D(_MROMap, IN.uv_MainTex); \
    /* Metallic comes from blue channel tinted by slider variables */ \
    o.Metallic = mro.r * _Metallic; \
    /* Smoothness comes from blue channel tinted by slider variables */ \
    o.Smoothness = 1 - mro.g * _Glossiness; \
    /* Ambient Occlusion comes from green channel */ \
    o.Occlusion = mro.b * _Occlusion; \
} \

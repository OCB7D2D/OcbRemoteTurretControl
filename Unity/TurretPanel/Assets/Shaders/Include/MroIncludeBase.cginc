#define OCB_BASE_UNIFORMS \
    float4 _Color; \
    sampler2D _MainTex; \
    float4 _EmissionColor; \
    sampler2D _EmissionMap; \
    sampler2D _BumpMap; \
    float _BumpScale; \
    float _Alpha; \

#define OCB_BASE_INPUTS \
    float2 uv_MainTex; \
    float2 uv_EmissionMap; \
    float2 uv_BumpMap; \

#define OCB_NORMALS_SURF(IN, o) \
{ \
    /* Normal comes from a bump map */ \
    o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale); \
} \

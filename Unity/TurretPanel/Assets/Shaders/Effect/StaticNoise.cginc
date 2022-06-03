float StaticRandom(fixed2 p, float ta, float tb) {
    return frac(sin(p.x * ta + p.y * tb) * 5678.);
}

float4 StaticNoise(float2 uv, float seed, float4 color)
{
    // if (tex2D(_MainTex, uv).r < 0.001) return 0;
    // Albedo comes from noise generator
    float t = seed / 1000 + 1576.42f;
    float ta = t * .654321;
    float2 res = float2(600, 200);
    uv = floor(uv * res) / res;
    float noise = StaticRandom(uv, ta,
        t * (ta * .123456));
    return color * noise;
}
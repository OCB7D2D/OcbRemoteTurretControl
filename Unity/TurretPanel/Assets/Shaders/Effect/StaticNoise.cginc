float StaticRandom(fixed2 p, float ta, float tb) {
    return frac(sin(p.x * ta + p.y * tb) * 5678.);
}

float4 StaticNoise(float2 uv, float time, float4 color)
{
    float t = time % 1024 + 12.756394;
    float ta = t * .654321;
    float tb = t * ta * .123456;
    float2 res = _ScreenSize;
    uv = floor(uv * res) / res;
    float noise = StaticRandom(uv, ta, tb);
    return color * noise;
}

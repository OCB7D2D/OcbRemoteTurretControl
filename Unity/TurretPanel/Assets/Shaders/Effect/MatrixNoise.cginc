float2 MatrixRandom(float2 uv) {
    return floor(abs(fmod(cos(
        uv * 652.6345 + uv.yx * 534.375 +
        _Time.w * 0.0000005 * dot(uv, float2(0.364, 0.934))),
        0.001)) * 16000.0);
}

float MatrixFallerSpeed(float col, float faller) {
    return fmod(cos(col * 363.435 + faller * 234.323), 0.1) * 1.0 + 0.3;
}

float4 MatrixNoise(float2 uv, float seed, float4 color,
    int _fallers, float _faller_height, float2 _faller_cells)
{
    float2 pix = fmod(uv, 1.0 / _faller_cells);
    float2 cell = (uv - pix) * _faller_cells;
    pix *= _faller_cells * float2(0.8, 1.0) + float2(0.1, 0.0);
    float c = tex2D(_MainTex, (MatrixRandom(cell) + pix) / 16.0).gb + 0.25;
    float b = 0.0;
    for (float i = 0.0; i < _fallers; ++i) {
        float s = (seed + i * 3534.34) * MatrixFallerSpeed(cell.x, i);
        float f = 3.0 - cell.y * 0.05 - fmod(s, _faller_height);
        if (f > 0.0 && f < 1.0) b += f;
    }
    return float4(color.rgb * b * c, 1);
}
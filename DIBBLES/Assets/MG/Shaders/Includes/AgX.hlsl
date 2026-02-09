// Raw, hue-preserving AgX-style tonemapper.
// Assumes input and output are in the same space (no sRGB/linear conversions).
// Applies a soft-knee curve to luminance and rescales RGB to preserve hue.

float TonemapAgXScalar(float x)
{
    // Simple filmic-style curve with toe and shoulder
    // x in [0, +inf) (HDR OK)
    const float toe = 0.08;      // lifts near-black
    const float shoulder = 5.0;  // highlight compression

    // Log shoulder for smooth compression
    float y = log(1.0 + shoulder * x) / log(1.0 + shoulder);

    // Toe lift
    y = (y + toe) / (1.0 + toe);

    return saturate(y);
}

float3 TonemapAgX(float3 color)
{
    // Preserve hue by scaling by luminance ratio
    float L = dot(color, float3(0.2126, 0.7152, 0.0722));
    float Lm = TonemapAgXScalar(L);

    float scale = (L > 1e-6) ? (Lm / L) : 0.0;
    float3 mapped = color * scale;

    return mapped;
}

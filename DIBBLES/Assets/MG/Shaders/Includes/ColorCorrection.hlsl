// Apply brightness: add bias to all channels (positive = brighter, negative = darker)
float4 ApplyBrightness(float4 color, float brightness)
{
    // brightness is typically in [-1, 1], where 0 = no change
    color.rgb += brightness;
    return saturate(color);
}

// Apply contrast: rescale relative to 0.5 (the midpoint)
// contrast > 0: more contrast; contrast < 0: less contrast; 0 = no change
float4 ApplyContrast(float4 color, float contrast)
{
    // contrast is typically in [-1, 1], where 0 = no change
    color.rgb = (color.rgb - 0.5) * (1.0 + contrast) + 0.5;
    return saturate(color);
}

// Apply saturation: lerp between grayscale and original color
// saturation = 0 → grayscale; 1 → original; >1 → oversaturate; <0 → invert saturation
float4 ApplySaturation(float4 color, float saturation)
{
    // Compute luminance (standard Rec.709 weights; adjust if you prefer)
    float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
    float3 gray = float3(luminance, luminance, luminance);
    color.rgb = lerp(gray, color.rgb, saturation);
    return saturate(color);
}

// Example combined usage (call in your PS):
float4 ApplyColor(float4 color, float brightness, float contrast, float saturation)
{
    color = ApplyBrightness(color, brightness);
    color = ApplyContrast(color, contrast);
    color = ApplySaturation(color, saturation);
    return color;
}

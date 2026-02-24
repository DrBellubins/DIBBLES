float GetLuminance(float3 color)
{
    return dot(color, float3(0.299f, 0.587f, 0.114f));
}

float GetLuminance(float4 color)
{
    return dot(color.rgb, float3(0.299f, 0.587f, 0.114f));
}

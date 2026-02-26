float3 SkyHorizonColor;
float3 SkyZenithColor;

// Self-contained height-based fog color computation to blend with skybox.
// This computes a per-direction fog color based on the view direction's y-component,
// simulating atmospheric blending towards horizon (low y) or zenith (high y).
float3 ComputeFog(float3 normalizedViewDir, float3 horizonColor, float3 zenithColor)
{
    // normalizedViewDir: direction from camera to pixel (must be normalized)
    float t = saturate(normalizedViewDir.y * 0.5f + 0.5f); // y = -1 (down) -> t=0 (horizon), y=0 -> t=0.5, y=1 (up) -> t=1 (zenith)
    return lerp(horizonColor, zenithColor, t);
}

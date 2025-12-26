float2 ScreenSize; // Screen dimensions in pixels

// Simple SSAO parameters (set from C#)
float AORadius;        // Radius in pixels for sampling
float AOBias;          // Tiny depth bias in normalized [0..1] depth to avoid self-occlusion
float AOIntensity;     // Scales depth occlusion strength
float NormalWeight;    // Subtle orientation influence factor
float AOEdgeStrength;  // Weights normal-contrast edge term for crease detection

texture ColorTex; // Input color texture
sampler ColorSampler = sampler_state
{
    Texture = <ColorTex>; // Bound texture
    MinFilter = POINT;    // Point filtering for minification
    MagFilter = POINT;    // Point filtering for magnification
    MipFilter = POINT;    // Point filtering for mipmaps
    AddressU = CLAMP;     // Clamp addressing for U coordinate
    AddressV = CLAMP;     // Clamp addressing for V coordinate
};

texture NormalTex; // Input normal texture
sampler NormalSampler = sampler_state
{
    Texture = <NormalTex>; // Bound texture
    MinFilter = POINT;     // Point filtering for minification
    MagFilter = POINT;     // Point filtering for magnification
    MipFilter = POINT;     // Point filtering for mipmaps
    AddressU = BORDER;     // Border addressing for U coordinate (changed for off-screen handling)
    AddressV = BORDER;     // Border addressing for V coordinate (changed for off-screen handling)
};

texture DepthTex; // Input depth texture
sampler DepthSampler = sampler_state
{
    Texture = <DepthTex>; // Bound texture
    MinFilter = POINT;    // Point filtering for minification
    MagFilter = POINT;    // Point filtering for magnification
    MipFilter = POINT;    // Point filtering for mipmaps
    AddressU = BORDER;    // Border addressing for U coordinate (changed for off-screen handling)
    AddressV = BORDER;    // Border addressing for V coordinate (changed for off-screen handling)
};

struct VSInput // Vertex shader input
{
    float3 Position : POSITION0; // Vertex position
    float2 TexCoord : TEXCOORD0; // Texture coordinates
};

struct VSOutput // Vertex shader output
{
    float4 Position : SV_Position; // Transformed position
    float2 TexCoord : TEXCOORD0;   // Passed texture coordinates
};

VSOutput VSMain(VSInput input) // Vertex shader main function
{
    VSOutput output;                    // Create output struct
    output.Position = float4(input.Position, 1.0f); // Set position with w=1.0
    output.TexCoord = input.TexCoord;   // Pass through texture coords
    return output;                      // Return output
}

// Fixed ring offsets (no random noise) for consistent sampling
static const int SampleCount = 12; // Number of samples
static const float2 SampleOffsets[SampleCount] =
{
    float2( 1,  0),  // Right
    float2(-1,  0),  // Left
    float2( 0,  1),  // Up
    float2( 0, -1),  // Down
    float2( 1,  1),  // Up-right
    float2( 1, -1),  // Down-right
    float2(-1,  1),  // Up-left
    float2(-1, -1),  // Down-left
    float2( 2,  0),  // Further right
    float2(-2,  0),  // Further left
    float2( 0,  2),  // Further up
    float2( 0, -2)   // Further down
};

// Simple SSAO: combine depth deltas and normal contrast around the pixel
float ComputeAO(float2 uv, float3 normal, float centerDepth) // Compute ambient occlusion factor
{
    // If the center is sky/far, no occlusion
    if (centerDepth >= 0.999f) // Check if center depth is near far plane
        return 1.0f;           // Return no occlusion

    float aoAccum = 0.0f; // Accumulator for occlusion
    float valid = 0.0f;   // Counter for valid samples

    float nInfluence = saturate(NormalWeight); // Saturate normal weight to [0..1]

    // Constant screen-space radius (stable for debug)
    float scaledRadius = AORadius; // Use parameter radius directly

    for (int i = 0; i < SampleCount; i++) // Loop over each sample
    {
        float2 sampleUV = uv + (SampleOffsets[i] * scaledRadius) / ScreenSize; // Compute sample UV offset

        // Sample neighbor depth/normal
        float neighborDepth = tex2D(DepthSampler, sampleUV).r; // Sample depth at offset

        // Skip invalid/sky samples
        if (neighborDepth >= 0.999f) // Check if neighbor is sky/far
            continue;                // Skip this sample

        float3 neighborNormalRGB = tex2D(NormalSampler, sampleUV).rgb; // Sample normal at offset
        float3 nSample = normalize(neighborNormalRGB * 2.0f - 1.0f);   // Decode normal to [-1..1]

        // Depth deltas are tiny in normalized [0..1] for your far=1000 setup.
        float dd = neighborDepth - centerDepth; // Compute depth difference

        // Skip samples with large depth discontinuities (e.g., edges)
        if (abs(dd) > 0.005f) // Check absolute depth diff against threshold (tune as needed)
            continue;         // Skip this sample

        // Only count closer neighbors as occluders (negative dd) with bias
        float closerTerm = saturate((-dd - AOBias) * AOIntensity); // Compute closer occlusion term (removed deeperTerm)

        // Edge term from normal contrast to reveal creases/corners
        float edgeTerm = AOEdgeStrength * saturate(1.0f - dot(normal, nSample)); // Compute edge based on normal dot product

        // Hemisphere bias: weight higher for samples in front relative to normal
        float2 dir2D = normalize(SampleOffsets[i]); // Normalize 2D direction
        float3 dir3D = normalize(float3(dir2D.xy, 0.0f)); // Extend to 3D (flat)
        float nWeight = saturate(dot(normal, dir3D) + 0.2f); // Compute weight with bias (changed for hemisphere enforcement)

        // Accumulate
        aoAccum += (closerTerm) * nWeight + edgeTerm; // Add weighted closer term and edge term
        valid += 1.0f; // Increment valid count
    }

    float occlusion = (valid > 0.0f) ? (aoAccum / valid) : 0.0f; // Average occlusion if valid samples exist

    // AO factor [0..1], where 1 = no occlusion
    return saturate(1.0f - occlusion); // Return saturated AO factor
}

float4 PSMain(VSOutput input) : SV_Target0 // Pixel shader main function
{
    float4 sceneCol = tex2D(ColorSampler,  input.TexCoord); // Sample scene color
    float3 normalRGB = tex2D(NormalSampler, input.TexCoord).rgb; // Sample normal
    float  depth     = tex2D(DepthSampler,  input.TexCoord).r; // Sample depth

    // Decode normal from [0..1] to [-1..1]
    float3 normal = normalize(normalRGB * 2.0f - 1.0f); // Decode and normalize normal

    // Compute AO factor
    float ao = ComputeAO(input.TexCoord, normal, depth); // Call AO computation

    // Debug: AO as grayscale
    return float4(ao, ao, ao, sceneCol.a); // Return AO in RGB, preserve alpha
}

technique TestPostProcess // Technique definition
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSMain();
    }
}

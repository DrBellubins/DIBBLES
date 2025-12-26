float2 ScreenSize;

// Simple SSAO parameters (set from C#)
float AORadius;      // nominal radius in pixels (will be scaled by perspective)
float AOBias;        // small depth bias to avoid self-occlusion
float AOIntensity;   // scales occlusion strength
float NormalWeight;  // optional normal influence (kept minimal)

texture ColorTex;
sampler ColorSampler = sampler_state
{
    Texture = <ColorTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

texture NormalTex;
sampler NormalSampler = sampler_state
{
    Texture = <NormalTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

texture DepthTex;
sampler DepthSampler = sampler_state
{
    Texture = <DepthTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

struct VSInput
{
    float3 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position, 1.0f);
    output.TexCoord = input.TexCoord;
    return output;
}

// Fixed ring offsets (no random noise)
static const int SampleCount = 12;
static const float2 SampleOffsets[SampleCount] =
{
    float2( 1,  0),
    float2(-1,  0),
    float2( 0,  1),
    float2( 0, -1),
    float2( 1,  1),
    float2( 1, -1),
    float2(-1,  1),
    float2(-1, -1),
    float2( 2,  0),
    float2(-2,  0),
    float2( 0,  2),
    float2( 0, -2)
};

// Very simple depth-only SSAO: neighbors deeper than current pixel contribute occlusion
float ComputeAO(float2 uv, float3 normal, float centerDepth)
{
    // If the center is sky/far, no occlusion
    if (centerDepth >= 0.999f)
        return 1.0f;

    float aoAccum = 0.0f;
    float valid = 0.0f;

    float nInfluence = saturate(NormalWeight);

    // Perspective-scale the sampling radius to approximate a constant world radius
    float perspScale = 1.0f / max(centerDepth, 0.2f);
    float scaledRadius = AORadius * perspScale;

    for (int i = 0; i < SampleCount; i++)
    {
        float2 sampleUV = uv + (SampleOffsets[i] * scaledRadius) / ScreenSize;

        // Sample neighbor depth
        float neighborDepth = tex2D(DepthSampler, sampleUV).r;

        // Skip invalid/sky samples
        if (neighborDepth >= 0.999f)
            continue;

        // Positive means neighbor is farther (deeper) than center; that should occlude
        float dd = neighborDepth - centerDepth;

        // Contribution when neighbor is deeper than bias
        float contrib = saturate((dd - AOBias) * AOIntensity);

        // Slight normal influence
        float2 dir2D = normalize(SampleOffsets[i]);
        float3 dir3D = normalize(float3(dir2D.xy, 0.0f));
        float nWeight = 1.0f - nInfluence * saturate(dot(normalize(normal), dir3D));

        aoAccum += contrib * nWeight;
        valid += 1.0f;
    }

    // Normalize and convert to AO factor [0..1], where 1 = no occlusion
    float occlusion = (valid > 0.0f) ? (aoAccum / valid) : 0.0f;
    return saturate(1.0f - occlusion);
}

float4 PSMain(VSOutput input) : SV_Target0
{
    float4 sceneCol = tex2D(ColorSampler,  input.TexCoord);
    float3 normalRGB = tex2D(NormalSampler, input.TexCoord).rgb;
    float  depth     = tex2D(DepthSampler,  input.TexCoord).r;

    // Decode normal from [0..1] to [-1..1]
    float3 normal = normalize(normalRGB * 2.0f - 1.0f);

    // Compute simple AO factor
    float ao = ComputeAO(input.TexCoord, normal, depth);

    // Debug: output AO as grayscale
    return float4(ao, ao, ao, sceneCol.a);
}

technique TestPostProcess
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSMain();
    }
}

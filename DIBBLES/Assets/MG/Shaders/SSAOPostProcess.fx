float2 ScreenSize;

// Simple SSAO parameters (set from C#)
float AORadius;        // radius in pixels
float AOBias;          // tiny depth bias in normalized [0..1] depth
float AOIntensity;     // scales depth occlusion
float NormalWeight;    // subtle orientation influence
float AOEdgeStrength;  // weights normal-contrast edge term

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

// Simple SSAO: combine tiny depth deltas and normal contrast around the pixel
float ComputeAO(float2 uv, float3 normal, float centerDepth)
{
    // If the center is sky/far, no occlusion
    if (centerDepth >= 0.999f)
        return 1.0f;

    float aoAccum = 0.0f;
    float valid = 0.0f;

    float nInfluence = saturate(NormalWeight);

    // Constant screen-space radius (stable for debug)
    float scaledRadius = AORadius;

    for (int i = 0; i < SampleCount; i++)
    {
        float2 sampleUV = uv + (SampleOffsets[i] * scaledRadius) / ScreenSize;

        // Sample neighbor depth/normal
        float neighborDepth = tex2D(DepthSampler, sampleUV).r;

        // Skip invalid/sky samples
        if (neighborDepth >= 0.999f)
            continue;

        float3 neighborNormalRGB = tex2D(NormalSampler, sampleUV).rgb;
        float3 nSample = normalize(neighborNormalRGB * 2.0f - 1.0f);

        // Depth deltas are tiny in normalized [0..1] for your far=1000 setup.
        // Count BOTH deeper and closer neighbors as occluders with a very small bias.
        float dd = neighborDepth - centerDepth;
        float deeperTerm = saturate((dd - AOBias) * AOIntensity);       // neighbor is farther
        float closerTerm = saturate((-dd - AOBias) * AOIntensity * 0.8f); // neighbor is closer

        // Edge term from normal contrast to reveal creases/corners
        float edgeTerm = AOEdgeStrength * saturate(1.0f - dot(normal, nSample));

        // Slightly reduce occlusion when normal faces away from sample direction
        float2 dir2D = normalize(SampleOffsets[i]);
        float3 dir3D = normalize(float3(dir2D.xy, 0.0f));
        float nWeight = 1.0f - nInfluence * saturate(dot(normal, dir3D));

        // Accumulate
        aoAccum += (deeperTerm + closerTerm) * nWeight + edgeTerm;
        valid += 1.0f;
    }

    float occlusion = (valid > 0.0f) ? (aoAccum / valid) : 0.0f;

    // AO factor [0..1], where 1 = no occlusion
    return saturate(1.0f - occlusion);
}

float4 PSMain(VSOutput input) : SV_Target0
{
    float4 sceneCol = tex2D(ColorSampler,  input.TexCoord);
    float3 normalRGB = tex2D(NormalSampler, input.TexCoord).rgb;
    float  depth     = tex2D(DepthSampler,  input.TexCoord).r;

    // Decode normal from [0..1] to [-1..1]
    float3 normal = normalize(normalRGB * 2.0f - 1.0f);

    // Compute AO factor
    float ao = ComputeAO(input.TexCoord, normal, depth);

    // Debug: AO as grayscale
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

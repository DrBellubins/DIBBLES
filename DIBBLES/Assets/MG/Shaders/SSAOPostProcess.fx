float2 ScreenSize;

// Camera params for view-space reconstruction
float CameraNear;
float CameraFar;
float CameraAspect;
float TanHalfFov;    // tan(FOV * 0.5) in radians

// SSAO params
float AORadiusPx;        // nominal kernel radius in pixels
float AOBiasZ;           // small view-space z bias (meters)
float DepthThresholdZ;   // bilateral gate threshold (meters)
float AOIntensity;       // overall strength
float NormalWeight;      // subtle weighting

// Bilateral blur params
float BlurSigmaPx;       // gaussian sigma in pixels (e.g. 1.0–2.0)
float DepthSigmaZ;       // depth similarity sigma in meters (e.g. 2.0)
float NormalPow;         // normal similarity power (e.g. 4.0)

// Textures
texture ColorTex;
texture NormalTex;
texture DepthTex;

// SSAO output texture for blur passes
texture AOTex;

// Samplers
sampler ColorSampler = sampler_state
{
    Texture = <ColorTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler NormalSampler = sampler_state
{
    Texture = <NormalTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = BORDER;
    AddressV = BORDER;
};

sampler DepthSampler = sampler_state
{
    Texture = <DepthTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = BORDER;
    AddressV = BORDER;
};

sampler AOSamplerLinear = sampler_state
{
    Texture = <AOTex>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
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
    VSOutput o;
    o.Position = float4(input.Position, 1.0f);
    o.TexCoord = input.TexCoord;
    return o;
}

// Deterministic kernel (no random rotation)
static const int SampleCount = 12;
static const float2 SampleOffsets[SampleCount] =
{
    float2( 1,  0), float2(-1,  0),
    float2( 0,  1), float2( 0, -1),
    float2( 1,  1), float2( 1, -1),
    float2(-1,  1), float2(-1, -1),
    float2( 2,  0), float2(-2,  0),
    float2( 0,  2), float2( 0, -2)
};

static const int RingCount = 2;
static const float RingScale[RingCount] = { 1.0f, 2.3f }; // non-integer outer ring to break grid aliasing

// Depth is linear [0..1]. Convert to view-space Z
float DepthLinToViewZ(float dlin)
{
    return lerp(CameraNear, CameraFar, dlin);
}

// Reconstruct view-space position from uv and linear depth
float3 ReconstructVSPos(float2 uv, float dlin)
{
    float z = DepthLinToViewZ(dlin);

    // NDC from uv (Y flipped because our fullscreen quad uses top-left origin UVs)
    float2 ndc;
    ndc.x = uv.x * 2.0f - 1.0f;
    ndc.y = (1.0f - uv.y) * 2.0f - 1.0f;

    // Symmetric perspective reconstruction
    float x = ndc.x * z * CameraAspect * TanHalfFov;
    float y = ndc.y * z * TanHalfFov;

    return float3(x, y, z);
}

// Perspective-correct pixel-to-uv scaling for a view-space offset (meters) at depth z
float2 VSOffsetToUV(float2 vsXY, float z)
{
    // ndc = vs / (z * (tanHalfFov * {aspect or 1}))
    float ndcX = vsXY.x / (z * CameraAspect * TanHalfFov);
    float ndcY = vsXY.y / (z * TanHalfFov);

    // uv = (ndc + 1) * 0.5, so d_uv = d_ndc * 0.5
    return 0.5f * float2(ndcX, ndcY);
}

float ComputeAO(float2 uv, float3 normalRGB)
{
    float dCenterLin = tex2D(DepthSampler, uv).r;
    if (dCenterLin >= 0.999f)
        return 1.0f;

    float3 n = normalize(normalRGB * 2.0f - 1.0f);
    float3 pVS = ReconstructVSPos(uv, dCenterLin);

    float occlusion = 0.0f;
    float samples = 0.0f;

    float nInfl = saturate(NormalWeight);

    // Base view-space radius from pixel radius (convert 1px in screen to meters at z, then scale)
    // One pixel in uv is 1/ScreenSize; convert that to view-space along x at z:
    // vsPerPixelX = z * Aspect * TanHalfFov * (2 / ScreenSize.x)
    float vsPerPixelX = pVS.z * CameraAspect * TanHalfFov * (2.0f / ScreenSize.x);
    float vsPerPixelY = pVS.z * TanHalfFov * (2.0f / ScreenSize.y);

    // Use average of axes to get a view-space radius roughly matching AORadiusPx at the current depth
    float baseVSRadius = 0.5f * (vsPerPixelX + vsPerPixelY) * AORadiusPx;

    [unroll]
    for (int r = 0; r < RingCount; r++)
    {
        float ringR = baseVSRadius * RingScale[r];

        [unroll]
        for (int i = 0; i < SampleCount; i++)
        {
            // Tangent-plane move in view-space XY
            float2 vsOffset = ringR * normalize(SampleOffsets[i]);

            // Convert view-space XY offset to uv delta at current z
            float2 uvOff = VSOffsetToUV(vsOffset, pVS.z);
            float2 uvSamp = uv + uvOff;

            float dLinN = tex2D(DepthSampler, uvSamp).r;
            if (dLinN >= 0.999f)
                continue;

            float3 pNVS = ReconstructVSPos(uvSamp, dLinN);

            // View-space z delta: positive when neighbor is closer (occluder)
            float ddZ = pVS.z - pNVS.z;

            // Bilateral gate to reject silhouettes and large discontinuities
            if (ddZ <= AOBiasZ || ddZ > DepthThresholdZ)
                continue;

            // Hemisphere check (normal-facing)
            float3 vdir = normalize(pNVS - pVS);
            float hemi = saturate(dot(n, vdir));
            if (hemi <= 0.0f)
                continue;

            // Distance falloff in view-space (xy distance)
            float distVS = length(pNVS.xy - pVS.xy);
            float falloff = saturate(1.0f - distVS / (ringR * 1.5f));

            // Slight directional weighting to avoid grazing directions
            float2 dir2D = normalize(SampleOffsets[i]);
            float3 dir3D = normalize(float3(dir2D.xy, 0.0f));
            float nWeight = 1.0f - nInfl * saturate(1.0f - dot(n, dir3D));

            float contrib = AOIntensity * (ddZ / DepthThresholdZ) * falloff * hemi * nWeight;

            occlusion += contrib;
            samples += 1.0f;
        }
    }

    float occ = (samples > 0.0f) ? occlusion / samples : 0.0f;
    return saturate(1.0f - occ);
}

// Pass 1: SSAO to texture
float4 PS_SSAO(VSOutput input) : SV_Target0
{
    float3 normalRGB = tex2D(NormalSampler, input.TexCoord).rgb;
    float ao = ComputeAO(input.TexCoord, normalRGB);
    return float4(ao, ao, ao, 1.0f);
}

// Gaussian weights for 5-tap blur
static const float w0 = 0.4026f; // center
static const float w1 = 0.2442f; // +/-1
static const float w2 = 0.0545f; // +/-2

// Similarity helpers
float DepthSimilarity(float zc, float zn)
{
    float dz = abs(zn - zc);
    return exp(-(dz * dz) / (2.0f * DepthSigmaZ * DepthSigmaZ));
}

float NormalSimilarity(float3 nc, float3 nn)
{
    float d = saturate(dot(nc, nn));
    return pow(d, NormalPow);
}

// Pass 2: Horizontal bilateral blur of AO
float4 PS_BlurH(VSOutput input) : SV_Target0
{
    float2 texel = float2(1.0f / ScreenSize.x, 0.0f);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float zC = DepthLinToViewZ(tex2D(DepthSampler, input.TexCoord).r);
    float3 nC = normalize(tex2D(NormalSampler, input.TexCoord).rgb * 2.0f - 1.0f);

    float sum = w0 * aoC;
    float wsum = w0;

    // +/-1
    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * (s * BlurSigmaPx);
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN = DepthLinToViewZ(tex2D(DepthSampler, uv).r);
        float3 nN = normalize(tex2D(NormalSampler, uv).rgb * 2.0f - 1.0f);

        float w = w1 * DepthSimilarity(zC, zN) * NormalSimilarity(nC, nN);
        sum += w * aoN;
        wsum += w;
    }

    // +/-2
    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * (s * BlurSigmaPx);
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN = DepthLinToViewZ(tex2D(DepthSampler, uv).r);
        float3 nN = normalize(tex2D(NormalSampler, uv).rgb * 2.0f - 1.0f);

        float w = w2 * DepthSimilarity(zC, zN) * NormalSimilarity(nC, nN);
        sum += w * aoN;
        wsum += w;
    }

    float ao = sum / max(wsum, 1e-4f);
    return float4(ao, ao, ao, 1.0f);
}

// Pass 3: Vertical bilateral blur of AO
float4 PS_BlurV(VSOutput input) : SV_Target0
{
    float2 texel = float2(0.0f, 1.0f / ScreenSize.y);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float zC = DepthLinToViewZ(tex2D(DepthSampler, input.TexCoord).r);
    float3 nC = normalize(tex2D(NormalSampler, input.TexCoord).rgb * 2.0f - 1.0f);

    float sum = w0 * aoC;
    float wsum = w0;

    // +/-1
    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * (s * BlurSigmaPx);
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN = DepthLinToViewZ(tex2D(DepthSampler, uv).r);
        float3 nN = normalize(tex2D(NormalSampler, uv).rgb * 2.0f - 1.0f);

        float w = w1 * DepthSimilarity(zC, zN) * NormalSimilarity(nC, nN);
        sum += w * aoN;
        wsum += w;
    }

    // +/-2
    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * (s * BlurSigmaPx);
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN = DepthLinToViewZ(tex2D(DepthSampler, uv).r);
        float3 nN = normalize(tex2D(NormalSampler, uv).rgb * 2.0f - 1.0f);

        float w = w2 * DepthSimilarity(zC, zN) * NormalSimilarity(nC, nN);
        sum += w * aoN;
        wsum += w;
    }

    float ao = sum / max(wsum, 1e-4f);
    return float4(ao, ao, ao, 1.0f);
}

technique SSAO
{
    pass AO
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PS_SSAO();
    }
}

technique BlurH
{
    pass H
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PS_BlurH();
    }
}

technique BlurV
{
    pass V
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PS_BlurV();
    }
}

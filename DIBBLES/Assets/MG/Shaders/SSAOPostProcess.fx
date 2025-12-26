float2 ScreenSize;

// Camera params for view-space reconstruction
float CameraNear;
float CameraFar;
float CameraAspect;
float TanHalfFov;    // tan(FOV * 0.5) in radians

// SSAO params (world-space)
float AORadiusPx;        // nominal radius in pixels
float AOBiasZ;           // small bias in view-space units (meters)
float DepthThresholdZ;   // bilateral gate threshold in view-space units
float AOIntensity;       // strength scaler
float NormalWeight;      // subtle influence

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
    AddressU = BORDER;
    AddressV = BORDER;
};

texture DepthTex;
sampler DepthSampler = sampler_state
{
    Texture = <DepthTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = BORDER;
    AddressV = BORDER;
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

// Fixed, deterministic kernel (no random rotation)
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

// Depth is linear [0..1]. Convert to view-space Z
float DepthLinToViewZ(float dlin)
{
    return lerp(CameraNear, CameraFar, dlin);
}

// Reconstruct view-space position from uv and linear depth
float3 ReconstructVSPos(float2 uv, float dlin)
{
    float z = DepthLinToViewZ(dlin);

    // NDC from uv (MonoGame default quad flips Y in our setup)
    float2 ndc;
    ndc.x = uv.x * 2.0f - 1.0f;
    ndc.y = (1.0f - uv.y) * 2.0f - 1.0f;

    // Symmetric perspective reconstruction
    float x = ndc.x * z * CameraAspect * TanHalfFov;
    float y = ndc.y * z * TanHalfFov;

    return float3(x, y, z);
}

// Pixel-to-view-space scale at current depth (approx)
float2 PixelToVS(float viewZ)
{
    float sx = (2.0f * viewZ * CameraAspect * TanHalfFov) / ScreenSize.x;
    float sy = (2.0f * viewZ * TanHalfFov)               / ScreenSize.y;
    return float2(sx, sy);
}

float ComputeAO(float2 uv, float3 normalEnc)
{
    float dCenterLin = tex2D(DepthSampler, uv).r;
    if (dCenterLin >= 0.999f)
        return 1.0f;

    float3 n = normalize(normalEnc * 2.0f - 1.0f);

    float3 pVS = ReconstructVSPos(uv, dCenterLin);
    float2 px2vs = PixelToVS(pVS.z);

    // Perspective scale radius into uv space (pixels -> uv delta)
    float scaledRadiusPx = AORadiusPx;
    float2 radiusUV = float2((scaledRadiusPx * px2vs.x) / (px2vs.x * ScreenSize.x),
                             (scaledRadiusPx * px2vs.y) / (px2vs.y * ScreenSize.y));

    float occlusionAccum = 0.0f;
    float valid = 0.0f;

    float nInfl = saturate(NormalWeight);

    [unroll]
    for (int i = 0; i < SampleCount; i++)
    {
        // UV offset in pixels converted to uv units
        float2 uvOff = (SampleOffsets[i] * scaledRadiusPx) / ScreenSize;
        float2 uvSamp = uv + uvOff;

        float dLinN = tex2D(DepthSampler, uvSamp).r;
        if (dLinN >= 0.999f)
            continue;

        float3 pNVS = ReconstructVSPos(uvSamp, dLinN);

        // View-space depth delta (positive when neighbor is closer to camera)
        float ddZ = (pVS.z - pNVS.z);

        // Bilateral gate to reject silhouettes and large discontinuities
        if (ddZ <= AOBiasZ || ddZ > DepthThresholdZ)
            continue;

        // Hemisphere check: only consider geometry in the direction of the surface normal
        float3 vdir = normalize(pNVS - pVS);
        float hemi = saturate(dot(n, vdir));   // >0 means within upper hemisphere

        if (hemi <= 0.0f)
            continue;

        // Distance falloff in view-space (using xy delta)
        float distVS = length((pNVS.xy - pVS.xy));
        float falloff = saturate(1.0f - distVS / (DepthThresholdZ * 1.5f));

        // Slight directional weighting to avoid grazing directions
        float2 dir2D = normalize(SampleOffsets[i]);
        float3 dir3D = normalize(float3(dir2D.xy, 0.0f));
        float nWeight = 1.0f - nInfl * saturate(1.0f - dot(n, dir3D));

        float contrib = AOIntensity * (ddZ / DepthThresholdZ) * falloff * hemi * nWeight;

        occlusionAccum += contrib;
        valid += 1.0f;
    }

    float occ = (valid > 0.0f) ? occlusionAccum / valid : 0.0f;

    // AO factor: 1 = no occlusion, 0 = full
    return saturate(1.0f - occ);
}

float4 PSMain(VSOutput input) : SV_Target0
{
    float4 sceneCol = tex2D(ColorSampler,  input.TexCoord);
    float3 normalRGB = tex2D(NormalSampler, input.TexCoord).rgb;

    float ao = ComputeAO(input.TexCoord, normalRGB);

    // Debug output: AO grayscale
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

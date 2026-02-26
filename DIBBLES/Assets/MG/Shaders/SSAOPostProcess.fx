// Chapman-style SSAO (view-space sampling with TBN orientation and reprojection)
//
// Inputs from C#:
//   ScreenSize      : float2(width, height)
//   Projection      : camera projection matrix
//   InvProjection   : inverse of camera projection
//   CameraNear/Far  : near/far planes (used to convert depth01 -> viewZ)
//   ColorTex        : scene color
//   DepthTex        : normalized linear depth in [0..1] (near=0, far=1)
//   NormalTex       : view-space normals encoded to [0..1], a=1 where valid
//   RandomTex       : small tileable blue-noise
//   AOTex           : intermediate AO texture for blur passes
//   NoiseScale      : float2(ScreenSize / noiseTextureSize)
//
// Tuning:
//   radius          : view-space radius (0.3–1.0 typical)
//   bias            : small view-space bias to prevent self-occlusion
//   total_strength  : AO strength multiplier
//   base_ao         : base AO floor (additive)
//
// Techniques:
//   SSAO   : writes AO to AOTex
//   BlurH  : horizontal bilateral blur (depth + normals)
//   BlurV  : vertical bilateral blur and composite over color

float2 ScreenSize;

float4x4 Projection;
float4x4 InvProjection;

float3 CameraPos;
float CameraNear;
float CameraFar;

float FogNear;
float FogFar;

float TanHalfFovY;  // tan(fov * 0.5)
float AspectRatio;  // width / height

float2 NoiseScale;

// Tuning
float total_strength;
float base_ao;
float radius;      // view-space units
float bias;        // view-space units (~0.02–0.08)

float BlurDepthSigma;
float BlurNormalPower;

// Kernel
static const int samples = 16;
static const float3 sample_sphere[samples] =
{
    float3( 0.5381, 0.1856,-0.4319), float3( 0.1379, 0.2486, 0.4430),
    float3( 0.3371, 0.5679,-0.0057), float3(-0.6999,-0.0451,-0.0019),
    float3( 0.0689,-0.1598,-0.8547), float3( 0.0560, 0.0069,-0.1843),
    float3(-0.0146, 0.1402, 0.0762), float3( 0.0100,-0.1924,-0.0344),
    float3(-0.3577,-0.5301,-0.4358), float3(-0.3169, 0.1063, 0.0158),
    float3( 0.0103,-0.5869, 0.0046), float3(-0.0897,-0.4940, 0.3287),
    float3( 0.7119,-0.0154,-0.0918), float3(-0.0533, 0.0596,-0.5411),
    float3( 0.0352,-0.0631, 0.5460), float3(-0.4776, 0.2847,-0.0271)
};

// Blur weights
static const float wC = 0.2270270f;
static const float w1 = 0.1945946f;
static const float w2 = 0.1216216f;
static const float w3 = 0.0540541f;
static const float w4 = 0.0162162f;

// Textures
texture ColorTex;
texture DepthTex;
texture NormalTex;
texture AOTex;
texture RandomTex;

// Samplers
sampler2D ColorSampler = sampler_state
{
    Texture = <ColorTex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D DepthSampler = sampler_state
{
    Texture = <DepthTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler2D NormalSampler = sampler_state
{
    Texture = <NormalTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler2D AOSamplerLinear = sampler_state
{
    Texture = <AOTex>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler2D RandomSampler = sampler_state
{
    Texture = <RandomTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = NONE;
    AddressU = WRAP;
    AddressV = WRAP;
};

struct VSInput
{
    float3 Position :  POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoord :  TEXCOORD0;
};

struct VSOutput
{
    float4 Position :  SV_Position;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput VSMain(VSInput input)
{
    VSOutput o;
    o.Position = float4(input.Position, 1.0f);
    o.Color = input.Color;
    o. TexCoord = input.TexCoord;
    return o;
}

// Utils
float3 DecodeNormal01(float4 nTex)
{
    float3 n = nTex.rgb * 2.0f - 1.0f;
    float len = max(length(n), 1e-5f);
    return n / len;
}

float viewZFrom01(float d01)
{
    return lerp(CameraNear, CameraFar, d01);
}

// Reconstruct view-space position from (uv, depth01)
float3 ReconstructViewPos(float2 uv, float depth01)
{
    // Ray direction in view space (not scaled by anything except aspect/fov)
    float rayX = (uv.x * 2.0f - 1.0f) * AspectRatio * TanHalfFovY;
    float rayY = (1.0f - uv.y * 2.0f) * TanHalfFovY;
    float rayZ = -1.0f;  // Camera looks down -Z, so ray. z = -1

    // Linear depth
    float linearZ = lerp(CameraNear, CameraFar, depth01);

    // Scale ray so that -ray.z * t = linearZ => t = linearZ
    return float3(rayX * linearZ, rayY * linearZ, rayZ * linearZ);
}

// Project view-space position back to UV (exact inverse of ReconstructViewPos)
bool ProjectToUV(float3 viewPos, out float2 uvOut)
{
    // viewPos = (rayX * linearZ, rayY * linearZ, -linearZ)
    // So: linearZ = -viewPos.z
    //     rayX = viewPos.x / linearZ
    //     rayY = viewPos. y / linearZ

    float linearZ = -viewPos.z;

    if (linearZ <= 0.0001f)
    {
        uvOut = float2(0.5f, 0.5f);
        return false;
    }

    float rayX = viewPos.x / linearZ;
    float rayY = viewPos.y / linearZ;

    // Reverse: rayX = (uv.x * 2 - 1) * AspectRatio * TanHalfFovY
    // uv.x = (rayX / (AspectRatio * TanHalfFovY) + 1) * 0.5
    uvOut.x = (rayX / (AspectRatio * TanHalfFovY) + 1.0f) * 0.5f;

    // Reverse:  rayY = (1 - uv. y * 2) * TanHalfFovY
    // uv.y = (1 - rayY / TanHalfFovY) * 0.5
    uvOut.y = (1.0f - rayY / TanHalfFovY) * 0.5f;

    if (uvOut.x < 0.0f || uvOut.x > 1.0f || uvOut. y < 0.0f || uvOut.y > 1.0f)
        return false;

    return true;
}

// Range check weight (Chapman)
float RangeWeight(float centerZ, float sampleZ, float r)
{
    float d = abs(centerZ - sampleZ);
    return saturate(d > 1e-4f ? smoothstep(0.0f, 1.0f, r / d) : 1.0f);
}

float FogFactor(float3 viewPos)
{
    float dist = length(viewPos - CameraPos);
    return saturate((dist - FogNear) / (FogFar - FogNear));
}

// Core AO
float ComputeAO(float2 uv)
{
    float depth01 = tex2D(DepthSampler, uv).r;

    if (depth01 >= 0.999f)
        return 1.0f;

    float3 P = ReconstructViewPos(uv, depth01);
    float centerZ = -P.z;

    // Get normal from G-buffer
    float3 N;
    float4 nTex = tex2D(NormalSampler, uv);

    if (nTex.a < 0.5f)
    {
        // No normal: do not darken
        return 1.0f;
    }
    else
    {
        N = DecodeNormal01(nTex);
    }

    // TBN construction (same as before)
    float3 R = tex2D(RandomSampler, uv * NoiseScale).rgb * 2.0f - 1.0f;
    R = normalize(R);

    float3 T = R - N * dot(R, N);
    float tLen = length(T);

    if (tLen < 0.001f)
    {
        float3 up = abs(N.y) < 0.99f ? float3(0, 1, 0) : float3(1, 0, 0);
        T = normalize(cross(N, up));
    }
    else
    {
        T = T / tLen;
    }

    float3 B = cross(N, T);
    float3x3 TBN = float3x3(T, B, N);

    float occlusion = 0.0f;
    int validSamples = 0;

    [unroll]
    for (int i = 0; i < samples; i++)
    {
        // Hemisphere-only sampling to avoid back-facing self-occlusion
        float3 s = sample_sphere[i];
        s.z = abs(s.z);

        float3 sampleOffset = mul(s, TBN);
        float3 samplePosVS = P + sampleOffset * radius;

        float2 uvSamp;

        if (!ProjectToUV(samplePosVS, uvSamp))
            continue;

        float sampDepth01 = tex2D(DepthSampler, uvSamp).r;
        float sceneZ = lerp(CameraNear, CameraFar, sampDepth01);
        float sampleZ = -samplePosVS.z;

        // Depth-proportional bias (reduces halos without crushing AO)
        float biasVS = max(bias, centerZ * 0.0005f);

        // Range check falloff
        float rangeCheck = 1.0f - smoothstep(0.0f, radius, abs(sceneZ - centerZ));

        // Occlusion if scene is in front of the sample (with bias)
        float occ = (sceneZ < sampleZ - biasVS) ? 1.0f : 0.0f;

        occlusion += occ * rangeCheck;
        validSamples++;
    }

    if (validSamples == 0)
        return 1.0f;

    float ao = 1.0f - (occlusion / (float)validSamples) * total_strength;

    // Fog falloff (fade AO as fog increases)
    float fogFactor = FogFactor(P);
    ao = lerp(ao, 1.0, fogFactor); // Fade AO with fog; at max fog, AO is 1 (no darken)

    return saturate(ao + base_ao);
}

// Pass 1: SSAO
float4 PS_SSAO(VSOutput input) : SV_Target0
{
    float ao = ComputeAO(input.TexCoord);
    return float4(ao, ao, ao, 1.0f);
}

/*float4 PS_SSAO(VSOutput input) : SV_Target0
{
    float4 nTex = tex2D(NormalSampler, input. TexCoord);

    // Visualize normals - should show smooth color gradients on surfaces
    // Red = X, Green = Y, Blue = Z

    if (nTex. a >= 0.5f)
    {
        float3 N = DecodeNormal01(nTex);
        // Remap from [-1,1] to [0,1] for visualization
        return float4(N * 0.5f + 0.5f, 1.0f);
    }
    else
    {
        return float4(1.0f, 0.0f, 1.0f, 1.0f); // Magenta for missing normals
    }
}*/

// Depth bilateral term
float DepthSimilarity(float zc, float zn, float sigma)
{
    float dz = abs(zn - zc);
    return exp(-(dz * dz) / (2.0f * sigma * sigma));
}

// Normal bilateral term
float NormalSimilarity(float3 nc, float3 nn, float normalPow)
{
    float d = saturate(dot(nc, nn));
    return pow(d, normalPow);
}

// PS_BlurH to a 9-tap bilateral blur
float4 PS_BlurH(VSOutput input) : SV_Target0
{
    float2 texel = float2(1.0f / ScreenSize.x, 0.0f);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float dC  = tex2D(DepthSampler,   input.TexCoord).r;

    float4 nCtex = tex2D(NormalSampler, input.TexCoord);
    float3 nC = (nCtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nCtex);

    float sum  = wC * aoC;
    float wsum = wC;

    // Offsets ±1
    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w1 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    // Offsets ±2
    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w2 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    // Offsets ±3
    [unroll]
    for (int s = -3; s <= 3; s += 6)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w3 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    // Offsets ±4
    [unroll]
    for (int s = -4; s <= 4; s += 8)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w4 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    float ao = sum / max(wsum, 1e-4f);
    return float4(ao, ao, ao, 1.0f);
}

// PS_BlurV to match the 9-tap kernel vertically
float4 PS_BlurV(VSOutput input) : SV_Target0
{
    float2 texel = float2(0.0f, 1.0f / ScreenSize.y);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float dC  = tex2D(DepthSampler,   input.TexCoord).r;

    float4 nCtex = tex2D(NormalSampler, input.TexCoord);
    float3 nC = (nCtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nCtex);

    float sum  = wC * aoC;
    float wsum = wC;

    // Offsets ±1
    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w1 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    // Offsets ±2
    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w2 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    // Offsets ±3
    [unroll]
    for (int s = -3; s <= 3; s += 6)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w3 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    // Offsets ±4
    [unroll]
    for (int s = -4; s <= 4; s += 8)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler,   uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0, 0, 1) : DecodeNormal01(nNtex);

        float w = w4 * DepthSimilarity(dC, dN, BlurDepthSigma) * NormalSimilarity(nC, nN, BlurNormalPower);
        sum  += w * aoN;
        wsum += w;
    }

    float ao = sum / max(wsum, 1e-4f);
    return float4(ao, ao, ao, 1.0f);
}

float4 PS_Composite(VSOutput input) : SV_Target0
{
    // Use blurred AO and mask where normals are invalid
    float ao = tex2D(AOSamplerLinear, input.TexCoord).r;

    // If normal is missing (sky, BasicEffect geometry, etc.), do not darken
    float4 nTex = tex2D(NormalSampler, input.TexCoord);

    if (nTex.a < 0.5f)
        ao = 1.0f;

    float4 color = tex2D(ColorSampler, input.TexCoord);

    return float4(color.rgb * ao, 1.0f); // ensure alpha = 1
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

technique Composite
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PS_Composite();
    }
}

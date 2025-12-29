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

float CameraNear;
float CameraFar;

float TanHalfFovY;  // tan(fov * 0.5)
float AspectRatio;  // width / height

float2 NoiseScale;

// Tuning
float total_strength;
float base_ao;
float radius;      // view-space units
float bias;        // view-space units (~0.02–0.08)

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

// VS/PS
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

// Reconstruct view-space position from (uv, depth01):
float3 ReconstructViewPos(float2 uv, float depth01)
{
    // Convert UV to normalized device coordinates centered at (0,0)
    float2 ndc;
    ndc.x = (uv.x * 2.0f - 1.0f) * AspectRatio * TanHalfFovY;
    ndc.y = (1.0f - uv.y * 2.0f) * TanHalfFovY;

    // Linear depth (positive distance from camera)
    float linearZ = lerp(CameraNear, CameraFar, depth01);

    // View-space position (camera looks down -Z)
    return float3(ndc.x * linearZ, ndc.y * linearZ, -linearZ);
}

// Project view-space position to screen uv
bool ProjectToUV(float3 viewPos, out float2 uvOut)
{
    float4 clip = mul(float4(viewPos, 1.0f), Projection);

    if (clip.w <= 0.0001f)
    {
        uvOut = float2(0.5f, 0.5f);
        return false;
    }

    float3 ndc = clip.xyz / clip. w;

    // Match ReconstructViewPos convention:
    // ReconstructViewPos: ndc.x = (uv.x * 2 - 1) * aspect * tanHalf
    //                     ndc.y = (1 - uv.y * 2) * tanHalf
    // So: uv.x = (ndc. x / (aspect * tanHalf) + 1) / 2 = ndc.x * 0.5 + 0.5  (after perspective divide)
    //     uv.y = (1 - ndc.y / tanHalf) / 2 = 0.5 - ndc.y * 0.5 = (1 - ndc.y) * 0.5

    uvOut. x = ndc.x * 0.5f + 0.5f;
    uvOut. y = 0.5f - ndc.y * 0.5f;  // This is the correct flip

    if (uvOut.x < 0.0f || uvOut.x > 1.0f || uvOut.y < 0.0f || uvOut.y > 1.0f)
        return false;

    return true;
}

// Range check weight (Chapman)
float RangeWeight(float centerZ, float sampleZ, float r)
{
    float d = abs(centerZ - sampleZ);
    return saturate(d > 1e-4f ? smoothstep(0.0f, 1.0f, r / d) : 1.0f);
}

// Core AO
float ComputeAO(float2 uv)
{
    float depth01 = tex2D(DepthSampler, uv).r;

    if (depth01 >= 0.999f)
        return 1.0f;

    float3 P = ReconstructViewPos(uv, depth01);
    float centerZ = -P.z;

    // Get normal from G-buffer or use view-space forward as fallback
    float4 nTex = tex2D(NormalSampler, uv);
    float3 N;

    if (nTex.a >= 0.5f)
    {
        N = DecodeNormal01(nTex);
    }
    else
    {
        // Reconstruct normal from depth (more reliable than flat fallback)
        float2 texel = 1.0f / ScreenSize;

        float depthL = tex2D(DepthSampler, uv + float2(-texel.x, 0)).r;
        float depthR = tex2D(DepthSampler, uv + float2( texel.x, 0)).r;
        float depthU = tex2D(DepthSampler, uv + float2(0, -texel. y)).r;
        float depthD = tex2D(DepthSampler, uv + float2(0,  texel.y)).r;

        float3 PL = ReconstructViewPos(uv + float2(-texel.x, 0), depthL);
        float3 PR = ReconstructViewPos(uv + float2( texel.x, 0), depthR);
        float3 PU = ReconstructViewPos(uv + float2(0, -texel. y), depthU);
        float3 PD = ReconstructViewPos(uv + float2(0,  texel. y), depthD);

        float3 dPdx = PR - PL;
        float3 dPdy = PD - PU;

        N = normalize(cross(dPdy, dPdx));
    }

    // Robust TBN construction
    float3 R = tex2D(RandomSampler, uv * NoiseScale).rgb * 2.0f - 1.0f;
    R = normalize(R);

    // Gram-Schmidt with fallback for near-parallel vectors
    float3 T = R - N * dot(R, N);
    float tLen = length(T);

    if (tLen < 0.001f)
    {
        // R was parallel to N, pick a different basis
        float3 up = abs(N. y) < 0.99f ? float3(0, 1, 0) : float3(1, 0, 0);
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
        float3 sampleOffset = mul(sample_sphere[i], TBN);
        float3 samplePosVS = P + sampleOffset * radius;

        float2 uvSamp;
        if (! ProjectToUV(samplePosVS, uvSamp))
            continue;

        float sampDepth01 = tex2D(DepthSampler, uvSamp).r;
        float sceneZ = lerp(CameraNear, CameraFar, sampDepth01);
        float sampleZ = -samplePosVS.z;

        float rangeCheck = smoothstep(0.0f, 1.0f, radius / max(abs(sceneZ - centerZ), 0.001f));
        float occ = (sceneZ < sampleZ - bias) ? 1.0f : 0.0f;

        occlusion += occ * rangeCheck;
        validSamples++;
    }

    if (validSamples == 0)
        return 1.0f;

    float ao = 1.0f - (occlusion / (float)validSamples) * total_strength;
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
    float depth01 = tex2D(DepthSampler, input. TexCoord).r;

    // Test 1: Is depth being read?  (should see gradient)
    //return float4(depth01, depth01, depth01, 1.0f); // works fine

    // Test 2: Is reconstruction working? (should see smooth gradient, not all black/white)
    float3 P = ReconstructViewPos(input.TexCoord, depth01);
    float vizZ = saturate(-P. z / CameraFar);
    //return float4(vizZ, vizZ, vizZ, 1.0f); // smooth gradient looks like depth01

    // Test 3: Does projection round-trip work?  (should see roughly white everywhere except edges)
    float2 uvTest;
    bool valid = ProjectToUV(P, uvTest);
    float err = length(uvTest - input.TexCoord);
    return float4(err * 10.0f, valid ? 1.0f : 0.0f, 0.0f, 1.0f);  // Green if valid, red = error magnitude: Still weird black dot in top left corner

    // Test 4: Are samples landing on screen? Count valid samples
    float4 nTex = tex2D(NormalSampler, input.TexCoord);
    float3 N = (nTex.a < 0.5f) ? float3(0, 0, -1) : DecodeNormal01(nTex);
    float3 R = normalize(tex2D(RandomSampler, input.TexCoord * NoiseScale).rgb * 2.0f - 1.0f);
    float3 T = normalize(R - N * dot(R, N));
    float3 B = cross(N, T);
    float3x3 TBN = float3x3(T, B, N);

    int validCount = 0;
    for (int i = 0; i < samples; i++)
    {
        float3 sampleOffset = mul(sample_sphere[i], TBN);
        float3 samplePosVS = P + sampleOffset * radius;
        float2 uvSamp;
        if (ProjectToUV(samplePosVS, uvSamp))
            validCount++;
    }

    float ratio = (float)validCount / (float)samples;
    //return float4(ratio, ratio, ratio, 1.0f);  // Should be mostly white (most samples valid)
}*/

// Blur weights
static const float w0 = 0.4026f;
static const float w1 = 0.2442f;
static const float w2 = 0.0545f;

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

// Blur H
float4 PS_BlurH(VSOutput input) : SV_Target0
{
    float2 texel = float2(1.0f / ScreenSize.x, 0.0f);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float dC  = tex2D(DepthSampler, input.TexCoord).r;

    float4 nCtex = tex2D(NormalSampler, input.TexCoord);
    float3 nC = (nCtex.a < 0.5f) ? float3(0,0,1) : DecodeNormal01(nCtex);

    float sum  = w0 * aoC;
    float wsum = w0;

    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0,0,1) : DecodeNormal01(nNtex);

        float w = w1 * DepthSimilarity(dC, dN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
        wsum += w;
    }

    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0,0,1) : DecodeNormal01(nNtex);

        float w = w2 * DepthSimilarity(dC, dN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
        wsum += w;
    }

    float ao = sum / max(wsum, 1e-4f);
    return float4(ao, ao, ao, 1.0f);
}

// Blur V + composite
float4 PS_BlurV(VSOutput input) : SV_Target0
{
    float2 texel = float2(0.0f, 1.0f / ScreenSize.y);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float dC  = tex2D(DepthSampler, input.TexCoord).r;

    float4 nCtex = tex2D(NormalSampler, input.TexCoord);
    float3 nC = (nCtex.a < 0.5f) ? float3(0,0,1) : DecodeNormal01(nCtex);

    float sum  = w0 * aoC;
    float wsum = w0;

    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0,0,1) : DecodeNormal01(nNtex);

        float w = w1 * DepthSimilarity(dC, dN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
        wsum += w;
    }

    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float dN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? float3(0,0,1) : DecodeNormal01(nNtex);

        float w = w2 * DepthSimilarity(dC, dN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
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

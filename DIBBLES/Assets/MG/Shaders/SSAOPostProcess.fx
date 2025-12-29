// Pure depth-based SSAO inspired by normal_from_depth.glsl
// - Reconstructs normals from depth alone using finite differences
// - Hemispherical sampling oriented by a screen-space random vector
// - Optional bilateral blur using depth + normals reconstructed from depth
//
// Required effect params to set from C#:
//   ScreenSize         : float2(width, height)
//   DepthTex           : Texture containing depth (0..1) sampled as .r
//   RandomTex          : Noise texture (RGB), any small-tile blue-noise works
//   AOTex              : Intermediate AO texture for blur passes (set by the effect code)
//
// Notes:
// - This implementation expects a readable depth texture. If you only have the default depth buffer,
//   you must render scene depth to a texture first (e.g., via a depth pre-pass or MRT).
// - RandomTex can be a small blue/noise texture; it is sampled at 4x UV scale like the reference.
//
// Techniques:
//   SSAO   : Produces AO into AOTex
//   BlurH  : Horizontal bilateral blur (depth + reconstructed normals)
//   BlurV  : Vertical bilateral blur

float2 ScreenSize;

// Tunable parameters (match the reference behavior)
float total_strength;
float base_ao;
float area;
float falloff;
float radius;

// Sample kernel (reference 16-sample sphere)
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

// Inputs
texture ColorTex;
texture DepthTex;
texture NormalTex;

texture AOTex;
texture RandomTex;

// Samplers (textures must be bound from C#)
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

// VS/PS structs
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

// Utility functions (GLSL equivalents)
float3 DecodeNormal01(float4 nTex)
{
    float3 n = nTex.rgb * 2.0f - 1.0f;
    float len = max(length(n), 1e-5f);
    return n / len;
}

float step(float edge, float x)
{
    return x >= edge ? 1.0f : 0.0f;
}

float smoothstep(float minv, float maxv, float x)
{
    float t = saturate((x - minv) / (maxv - minv));
    return t * t * (3.0f - 2.0f * t);
}

float randomNumber(in float2 uv)
{
    float2 noise = (frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * 43758.5453));
    return abs(noise.x + noise.y) * 0.5;
}

// Reconstruct screen-space normal from depth using finite differences.
// Uses 1-pixel offsets scaled by ScreenSize instead of fixed 0.001.
float3 NormalFromDepth(float depth, float2 uv)
{
    float2 texel = 1.0f / ScreenSize;

    float depth1 = tex2D(DepthSampler, uv + float2(0.0f, texel.y)).r;
    float depth2 = tex2D(DepthSampler, uv + float2(texel.x, 0.0f)).r;

    float3 p1 = float3(0.0f, texel.y, depth1 - depth);
    float3 p2 = float3(texel.x, 0.0f, depth2 - depth);

    float3 n = cross(p1, p2);
    n.z = -n.z;

    return normalize(n);
}

// Use MRT normals (with alpha guard) in ComputeAO
float ComputeAO(float2 uv)
{
    float depth = tex2D(DepthSampler, uv).r;

    if (depth >= 0.999f)
    {
        return 1.0f;
    }

    float3 position = float3(uv, depth);

    float4 nTex = tex2D(NormalSampler, uv);
    float3 normal = (nTex.a < 0.5f) ? NormalFromDepth(depth, uv) : DecodeNormal01(nTex);

    float3 random = normalize(tex2D(RandomSampler, uv * 40.0f).rgb * 2.0f - 1.0f);

    float radius_depth = radius / max(depth, 1e-5f);
    float occlusion = 0.0f;

    [unroll]
    for (int i = 0; i < samples; i++)
    {
        float3 ray = radius_depth * reflect(sample_sphere[i], random);
        float3 hemi_ray = position + sign(dot(ray, normal)) * ray;

        float2 uvSamp = saturate(hemi_ray.xy);
        float occ_depth = tex2D(DepthSampler, uvSamp).r;

        float difference = depth - occ_depth;

        occlusion += step(falloff, difference) * (1.0f - smoothstep(falloff, area, difference));
    }

    float ao = 1.0f - total_strength * occlusion * (1.0f / samples);
    return saturate(ao + base_ao);
}

// Pass 1: SSAO to texture
float4 PS_SSAO(VSOutput input) : SV_Target0
{
    float ao = ComputeAO(input.TexCoord);
    return float4(ao, ao, ao, 1.0f);
}

// Gaussian weights for 5-tap blur (same as previous implementation)
static const float w0 = 0.4026f;
static const float w1 = 0.2442f;
static const float w2 = 0.0545f;

// Depth similarity (use raw depth, not linearized; simple bilateral gate)
float DepthSimilarity(float zc, float zn, float sigma)
{
    float dz = abs(zn - zc);
    return exp(-(dz * dz) / (2.0f * sigma * sigma));
}

// Optional normal similarity using normals reconstructed from depth
float NormalSimilarity(float3 nc, float3 nn, float normalPow)
{
    float d = saturate(dot(nc, nn));
    return pow(d, normalPow);
}

// Horizontal blur: Use MRT normals in BlurH
float4 PS_BlurH(VSOutput input) : SV_Target0
{
    float2 texel = float2(1.0f / ScreenSize.x, 0.0f);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float zC  = tex2D(DepthSampler, input.TexCoord).r;

    float4 nCtex = tex2D(NormalSampler, input.TexCoord);
    float3 nC = (nCtex.a < 0.5f) ? NormalFromDepth(zC, input.TexCoord) : DecodeNormal01(nCtex);

    float sum  = w0 * aoC;
    float wsum = w0;

    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? NormalFromDepth(zN, uv) : DecodeNormal01(nNtex);

        float w = w1 * DepthSimilarity(zC, zN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
        wsum += w;
    }

    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? NormalFromDepth(zN, uv) : DecodeNormal01(nNtex);

        float w = w2 * DepthSimilarity(zC, zN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
        wsum += w;
    }

    float ao = sum / max(wsum, 1e-4f);
    return float4(ao, ao, ao, 1.0f);
}

// Vertical blur: Use MRT normals in BlurV
float4 PS_BlurV(VSOutput input) : SV_Target0
{
    float2 texel = float2(0.0f, 1.0f / ScreenSize.y);

    float aoC = tex2D(AOSamplerLinear, input.TexCoord).r;
    float zC  = tex2D(DepthSampler, input.TexCoord).r;

    float4 nCtex = tex2D(NormalSampler, input.TexCoord);
    float3 nC = (nCtex.a < 0.5f) ? NormalFromDepth(zC, input.TexCoord) : DecodeNormal01(nCtex);

    float sum  = w0 * aoC;
    float wsum = w0;

    [unroll]
    for (int s = -1; s <= 1; s += 2)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? NormalFromDepth(zN, uv) : DecodeNormal01(nNtex);

        float w = w1 * DepthSimilarity(zC, zN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
        wsum += w;
    }

    [unroll]
    for (int s = -2; s <= 2; s += 4)
    {
        float2 uv = input.TexCoord + texel * s;
        float aoN = tex2D(AOSamplerLinear, uv).r;
        float zN  = tex2D(DepthSampler, uv).r;

        float4 nNtex = tex2D(NormalSampler, uv);
        float3 nN = (nNtex.a < 0.5f) ? NormalFromDepth(zN, uv) : DecodeNormal01(nNtex);

        float w = w2 * DepthSimilarity(zC, zN, 1.5f) * NormalSimilarity(nC, nN, 4.0f);

        sum  += w * aoN;
        wsum += w;
    }

    float ao = sum / max(wsum, 1e-4f);

    float4 color = tex2D(ColorSampler, input.TexCoord);
    return float4(color.r * ao, color.g * ao, color.b * ao, 1.0f);
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

// Bloom.fx
// Three techniques: BloomDownsample, BloomUpsample, BloomCombine
// - Downsample: 13-tap kernel similar to Stride version
// - Upsample: 3x3 tent filter scaled by Radius and multiplied by Intensity
// - Combine: screen blend of SceneTex and BloomTex
//
// Fullscreen quad is provided in clip space [-1,1], vertex shader passes it through.
// Ignore sampler binding issues per instructions.

#define EPSILON 1.0e-4

float PreBrightness;

float Intensity;
float Strength;
float Radius;

float Threshold;
float3 ThresholdCurve;

float2 TexelSize;

float LayerDecay;
int LayerIndex;

Texture2D SourceTex : register(t0);
sampler SourceSampler : register(s0) = sampler_state
{
    Texture = <SourceTex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

Texture2D StageTex : register(t1);
sampler StageSampler : register(s1) = sampler_state
{
    Texture = <StageTex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

Texture2D BloomTex : register(t2);
sampler BloomSampler : register(s2) = sampler_state
{
    Texture = <BloomTex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

Texture2D SceneTex : register(t3);
sampler SceneSampler : register(s3) = sampler_state
{
    Texture = <SceneTex>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertIn
{
    float3 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertOut
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

VertOut FullscreenVS(VertIn i)
{
    VertOut o;
    o.Position = float4(i.Position.xy, 0, 1);
    o.TexCoord = i.TexCoord;
    return o;
}

float4 Box4(float4 a, float4 b, float4 c, float4 d)
{
    return (a + b + c + d) * 0.25;
}

float4 DownsamplePS(float2 uv : TEXCOORD0) : SV_Target0
{
    float2 o = TexelSize * max(Radius, 0.0001);

    float4 c0  = SourceTex.Sample(SourceSampler, uv + float2(-2, -2) * o);
    float4 c1  = SourceTex.Sample(SourceSampler, uv + float2( 0, -2) * o);
    float4 c2  = SourceTex.Sample(SourceSampler, uv + float2( 2, -2) * o);
    float4 c3  = SourceTex.Sample(SourceSampler, uv + float2(-1, -1) * o);
    float4 c4  = SourceTex.Sample(SourceSampler, uv + float2( 1, -1) * o);
    float4 c5  = SourceTex.Sample(SourceSampler, uv + float2(-2,  0) * o);
    float4 c6  = SourceTex.Sample(SourceSampler, uv + float2( 0,  0) * o);
    float4 c7  = SourceTex.Sample(SourceSampler, uv + float2( 2,  0) * o);
    float4 c8  = SourceTex.Sample(SourceSampler, uv + float2(-1,  1) * o);
    float4 c9  = SourceTex.Sample(SourceSampler, uv + float2( 1,  1) * o);
    float4 c10 = SourceTex.Sample(SourceSampler, uv + float2(-2,  2) * o);
    float4 c11 = SourceTex.Sample(SourceSampler, uv + float2( 0,  2) * o);
    float4 c12 = SourceTex.Sample(SourceSampler, uv + float2( 2,  2) * o);

    float4 r =
    Box4(c0, c1, c5, c6)   * 0.125 +
    Box4(c1, c2, c6, c7)   * 0.125 +
    Box4(c5, c6, c10, c11) * 0.125 +
    Box4(c6, c7, c11, c12) * 0.125 +
    Box4(c3, c4, c8, c9)   * 0.5;

    // Force visible alpha so debug draw with AlphaBlend shows content
    return float4(r.rgb, 1.0);
}

float4 UpsamplePS(float2 uv : TEXCOORD0) : SV_Target0
{
    float2 o = TexelSize * max(Radius, 0.0001);

    float2 u0 = uv + float2(-1, -1) * o;
    float2 u1 = uv + float2( 0, -1) * o;
    float2 u2 = uv + float2( 1, -1) * o;
    float2 u3 = uv + float2(-1,  0) * o;
    float2 u4 = uv + float2( 0,  0) * o;
    float2 u5 = uv + float2( 1,  0) * o;
    float2 u6 = uv + float2(-1,  1) * o;
    float2 u7 = uv + float2( 0,  1) * o;
    float2 u8 = uv + float2( 1,  1) * o;

    float4 c0 = SourceTex.Sample(SourceSampler, u0);
    float4 c1 = SourceTex.Sample(SourceSampler, u1);
    float4 c2 = SourceTex.Sample(SourceSampler, u2);
    float4 c3 = SourceTex.Sample(SourceSampler, u3);
    float4 c4 = SourceTex.Sample(SourceSampler, u4);
    float4 c5 = SourceTex.Sample(SourceSampler, u5);
    float4 c6 = SourceTex.Sample(SourceSampler, u6);
    float4 c7 = SourceTex.Sample(SourceSampler, u7);
    float4 c8 = SourceTex.Sample(SourceSampler, u8);

    float4 tent = 0.0625 * (c0 + 2 * c1 + c2 + 2 * c3 + 4 * c4 + 2 * c5 + c6 + 2 * c7 + c8);
    float3 rgb = tent.rgb * Strength;

    // Force visible alpha
    return float4(rgb, 1.0);
}

// Accumulation PS: add a weighted contribution per layer.
// Use additive blending on the render target for safe accumulation.
// Weight falls off by pow(LayerDecay, LayerIndex); multiply by Strength for per-layer control.
float4 AccumulatePS(float2 uv : TEXCOORD0) : SV_Target0
{
    float4 src = SourceTex.Sample(SourceSampler, uv);

    // Decay weight per layer, keep within [0..1]
    float w = saturate(Strength * pow(saturate(LayerDecay), (float)LayerIndex));

    // Return weighted contribution; additive blending composes layers
    return float4(src.rgb * w, 1.0);
}

float4 CombinePS(float2 uv : TEXCOORD0) : SV_Target0
{
    float4 scene = SceneTex.Sample(SceneSampler, uv);
    float4 bloom = BloomTex.Sample(BloomSampler, uv);

    bloom.rgb *= Intensity;

    //float3 screenRgb = scene.rgb + bloom.rgb - scene.rgb * bloom.rgb;

    return float4(scene.rgb + bloom.rgb, scene.a);
}

technique BloomDownsample
{
    pass P0
    {
        VertexShader = compile vs_6_0 FullscreenVS();
        PixelShader  = compile ps_6_0 DownsamplePS();
    }
}

technique BloomAccumulate
{
    pass P0
    {
        VertexShader = compile vs_6_0 FullscreenVS();
        PixelShader  = compile ps_6_0 AccumulatePS();
    }
}

technique BloomUpsample
{
    pass P0
    {
        VertexShader = compile vs_6_0 FullscreenVS();
        PixelShader  = compile ps_6_0 UpsamplePS();
    }
}

technique BloomCombine
{
    pass P0
    {
        VertexShader = compile vs_6_0 FullscreenVS();
        PixelShader  = compile ps_6_0 CombinePS();
    }
}

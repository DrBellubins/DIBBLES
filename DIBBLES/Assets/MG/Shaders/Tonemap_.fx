// Tonemap.fx (AgX-like)
// Scene-referred linear HDR input -> display-referred output.
//
// Steps:
// 1) Optional exposure (EV).
// 2) Transform to AgX working primaries.
// 3) Log shaper into [0..1] domain (approximately -10..+10 EV range).
// 4) Apply look S-curve (toe/shoulder via smoothstep-like shaping).
// 5) Inverse shaper back to linear domain.
// 6) Luminance-preserving chroma scaling and optional saturation.
// 7) Transform back to display RGB (Rec.709/sRGB primaries).
//
// Note: This is an analytic approximation intended to closely reproduce Blender's AgX.
// Exact match may require LUT-based ODT and gamut mapping tables.
// Per instructions: ignore sampler binding issues.

#define EPSILON 1.0e-6

texture SourceTex;

float ExposureEV = 0.0f;  // + stops
int   AgxLook    = 1;     // 0=Low, 1=Medium (default), 2=High, 3=VeryHigh
float Saturation = 1.0f;  // 1.0 = neutral, <1 desaturates

sampler2D SourceSampler = sampler_state
{
    Texture = <SourceTex>;
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

// AgX working primaries fit (from published AgX approximations)
static const float3x3 RGB2AgX =
{
    0.842479, 0.042770, 0.163138,
    0.078433, 0.878468, 0.043099,
    0.000000, 0.054184, 0.993662
};

static const float3x3 AgX2RGB =
{
     1.186680, -0.052460, -0.134210,
    -0.105210,  1.142220, -0.036990,
     0.000000, -0.054184,  1.006608
};

// Luma weights (Rec.709) — used as an approximation for AgX luma
static const float3 LumaW = float3(0.2126, 0.7152, 0.0722);

// Map EV to linear scale
float EV2Scale(float ev)
{
    return exp2(ev);
}

// Log shaper to [0..1] domain (covers ~-10..+10 EV)
float ShaperForward(float x)
{
    float lx = log2(max(x, EPSILON));
    const float minEV = -10.0f;
    const float maxEV = +10.0f;
    return saturate((lx - minEV) / (maxEV - minEV));
}

float ShaperInverse(float t)
{
    const float minEV = -10.0f;
    const float maxEV = +10.0f;
    float lx = lerp(minEV, maxEV, saturate(t));
    return exp2(lx);
}

// Contrast mapping roughly aligned to Blender's looks
float ContrastForLook(int look)
{
    // Medium ~ 1.00; Low/High/VeryHigh scale around it
    if (look == 0) return 0.85f; // Low
    if (look == 1) return 1.00f; // Medium
    if (look == 2) return 1.15f; // High
    if (look == 3) return 1.30f; // VeryHigh
    return 1.00f;
}

// S-curve in [0..1] with soft shoulders and toe
float LookCurve01(float x, float contrast)
{
    // Center on mid gray (0.5), apply contrast gain
    float y = (x - 0.5f) * contrast + 0.5f;

    // Soft clip near bounds using smoothstep shaping
    y = saturate(y);
    y = y * y * (3.0f - 2.0f * y);

    return y;
}

// Highlight desaturation blend factor — mild hue/gamut preservation
float HighlightDesatFactor(float luminance)
{
    // Start desaturating near very bright values; keep subtle
    // This mimics Blender AgX highlight chroma control qualitatively.
    float t = saturate((luminance - 1.0f) * 0.5f); // >1 gets into highlight range
    return 1.0f - (t * 0.15f); // up to ~15% desat at extreme highlights
}

float4 TonemapPS(float2 uv : TEXCOORD0) : COLOR0
{
    // 1) Sample scene-referred HDR color
    float4 scene = tex2D(SourceSampler, uv);
    float3 rgb   = scene.rgb;

    // 2) Exposure
    rgb *= EV2Scale(ExposureEV);

    // 3) To AgX primaries
    float3 agx = mul(RGB2AgX, rgb);

    // 4) Luminance
    float Y = dot(agx, LumaW);

    // 5) Log shaper
    float tY = ShaperForward(Y);

    // 6) Contrast/look curve
    float contrast = ContrastForLook(AgxLook);
    float tY2 = LookCurve01(tY, contrast);

    // 7) Inverse shaper
    float Yout = ShaperInverse(tY2);

    // 8) Luminance-preserving chroma scaling
    float scale = (Y > EPSILON) ? (Yout / Y) : 0.0f;
    float3 agx_out = agx * scale;

    // Optional saturation adjustment (global)
    agx_out = lerp(Yout.xxx, agx_out, Saturation);

    // Mild highlight desaturation to mimic AgX hue behavior
    float hDesat = HighlightDesatFactor(Yout);
    agx_out = lerp(Yout.xxx, agx_out, hDesat);

    // 9) Back to display primaries
    float3 outRgb = mul(AgX2RGB, agx_out);

    // 10) Clamp to display range
    outRgb = saturate(outRgb);

    return float4(outRgb, scene.a);
}

technique TonemapAgX
{
    pass P0
    {
        VertexShader = compile vs_3_0 FullscreenVS();
        PixelShader  = compile ps_3_0 TonemapPS();
    }
}

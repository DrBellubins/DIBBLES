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

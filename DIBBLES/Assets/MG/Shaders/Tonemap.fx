// Tonemap.fx - ACES tonemapping (sRGB -> ACES RRT/ODT -> sRGB)
// Converted from GLSL to HLSL and wired for fullscreen blit usage.
// Ignore sampler binding issues as requested.

float PreBrightness;
float PostBrightness;

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

// sRGB => XYZ => D65_2_D60 => AP1 => RRT_SAT
static const float3x3 ACESInputMat =
{
    {0.59719, 0.35458, 0.04823},
    {0.07600, 0.90834, 0.01566},
    {0.02840, 0.13383, 0.83777}
};

// ODT_SAT => XYZ => D60_2_D65 => sRGB
static const float3x3 ACESOutputMat =
{
    { 1.60475, -0.53108, -0.07367},
    {-0.10208,  1.10813, -0.00605},
    {-0.00327, -0.07276,  1.07602}
};

float3 RRTAndODTFit(float3 v)
{
    float3 a = v * (v + 0.0245786f) - 0.000090537f;
    float3 b = v * (0.983729f * v + 0.4329510f) + 0.238081f;
    return a / b;
}

float3 ACESFitted(float3 color)
{
    color = mul(ACESInputMat, color);

    // Apply RRT and ODT
    color = RRTAndODTFit(color);

    color = mul(ACESOutputMat, color);

    // Clamp to [0, 1]
    color = saturate(color);

    return color;
}

float4 TonemapACESPS(float2 uv : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(SourceSampler, uv);

    src.rgb = src.rgb * PreBrightness;

    // Assume src.rgb is in linear sRGB (HDR possible); apply ACES curve
    float3 mapped = ACESFitted(src.rgb);

    mapped = saturate(mapped * PostBrightness);

    // Preserve source alpha
    return float4(mapped, src.a);
}

technique TonemapACES
{
    pass P0
    {
        VertexShader = compile vs_3_0 FullscreenVS();
        PixelShader  = compile ps_3_0 TonemapACESPS();
    }
}

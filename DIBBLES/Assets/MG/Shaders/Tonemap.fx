// Tonemap.fx - ACES tonemapping (sRGB -> ACES RRT/ODT -> sRGB)
// Converted from GLSL to HLSL and wired for fullscreen blit usage.
// Ignore sampler binding issues as requested.

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

// ACES tonemap (HLSL)
float3 tonemap_aces(float3 rgb)
{
    // sRGB => XYZ => D65_2_D60 => AP1 => RRT_SAT
    const float3x3 IN = float3x3(
        0.59719, 0.07600, 0.02840,
        0.35458, 0.90834, 0.13383,
        0.04823, 0.01566, 0.83777
    );

    // ODT_SAT => XYZ => D60_2_D65 => sRGB
    const float3x3 OUT = float3x3(
        1.60475, -0.10208, -0.00327,
        -0.53108,  1.10813, -0.07276,
        -0.07367, -0.00605,  1.07602
    );

    float3 col = mul(IN, rgb);

    // Filmic curve
    float3 a = col * (col + 0.0245786) - 0.000090537;
    float3 b = col * (0.983729 * col + 0.4329510) + 0.238081;
    col = a / b;

    return saturate(mul(OUT, col));
}

float4 TonemapACESPS(float2 uv : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(SourceSampler, uv);

    // Assume src.rgb is in linear sRGB (HDR possible); apply ACES curve
    float3 mapped = tonemap_aces(src.rgb);

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

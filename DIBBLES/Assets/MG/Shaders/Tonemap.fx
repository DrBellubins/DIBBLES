#include "ACES.hlsl"

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

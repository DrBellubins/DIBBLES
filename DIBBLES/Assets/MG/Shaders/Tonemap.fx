#include "Includes/ACES.hlsl"
#include "Includes/AgX.hlsl"

int Algorithm; // 0 = ACES, 1 = AgX

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

float4 TonemapPS(float2 uv : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(SourceSampler, uv);
    src.rgb = src.rgb * PreBrightness;

    if (Algorithm == 0) // ACES
    {
        src.rgb = TonemapACES(src.rgb);
        src.rgb = saturate(src.rgb * PostBrightness);
    }
    else // AgX
    {
        src.rgb = TonemapAgX(src.rgb);
        src.rgb = saturate(src.rgb * PostBrightness);
    }

    // Preserve source alpha
    return src;
}

technique Tonemap
{
    pass P0
    {
        VertexShader = compile vs_3_0 FullscreenVS();
        PixelShader  = compile ps_3_0 TonemapPS();
    }
}

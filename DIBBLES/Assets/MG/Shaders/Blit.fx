// Blit.fx
// Simple texture copy to destination with linear sampling.

struct VSIn
{
    float3 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOut
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

VSOut FullscreenVS(VSIn i)
{
    VSOut o;
    o.Position = float4(i.Position.xy, 0, 1);
    o.TexCoord = i.TexCoord;
    return o;
}

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

float4 CopyPS(float2 uv : TEXCOORD0) : COLOR0
{
    return tex2D(SourceSampler, uv);
}

technique Blit
{
    pass P0
    {
        VertexShader = compile vs_3_0 FullscreenVS();
        PixelShader  = compile ps_3_0 CopyPS();
    }
}

// Parameters
float2 texelSize;
float radius;

Texture2D Texture0      : register(t0);
Texture2D MaskTexture   : register(t1);

// Samplers
sampler Sampler = sampler_state
{
    Texture = <Texture0>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler MaskSampler = sampler_state
{
    Texture = <MaskTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Vertex shader structs
struct VSInput
{
    float4 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

// Vertex shader: passthrough
VSOutput VSMain(VSInput input)
{
    VSOutput output;
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    return output;
}

float4 Box4(float4 p0, float4 p1, float4 p2, float4 p3)
{
    return (p0 + p1 + p2 + p3) * 0.25f;
}

// Downsample pass (weighted 5-group mix from original)
float4 DownsamplePS(float2 texCoord)
{
    float2 offset = texelSize;

    float4 c0  = tex2D(Sampler, texCoord + float2(-2, -2) * offset);
    float4 c1  = tex2D(Sampler, texCoord + float2( 0, -2) * offset);
    float4 c2  = tex2D(Sampler, texCoord + float2( 2, -2) * offset);
    float4 c3  = tex2D(Sampler, texCoord + float2(-1, -1) * offset);
    float4 c4  = tex2D(Sampler, texCoord + float2( 1, -1) * offset);
    float4 c5  = tex2D(Sampler, texCoord + float2(-2,  0) * offset);
    float4 c6  = tex2D(Sampler, texCoord + float2( 0,  0) * offset);
    float4 c7  = tex2D(Sampler, texCoord + float2( 2,  0) * offset);
    float4 c8  = tex2D(Sampler, texCoord + float2(-1,  1) * offset);
    float4 c9  = tex2D(Sampler, texCoord + float2( 1,  1) * offset);
    float4 c10 = tex2D(Sampler, texCoord + float2(-2,  2) * offset);
    float4 c11 = tex2D(Sampler, texCoord + float2( 0,  2) * offset);
    float4 c12 = tex2D(Sampler, texCoord + float2( 2,  2) * offset);

    float4 result =
          Box4(c0,  c1,  c5,  c6)  * 0.125
        + Box4(c1,  c2,  c6,  c7)  * 0.125
        + Box4(c5,  c6,  c10, c11) * 0.125
        + Box4(c6,  c7,  c11, c12) * 0.125
        + Box4(c3,  c4,  c8,  c9)  * 0.5;

    return result;
}

// Upsample pass (3x3 tent)
float4 UpsamplePS(float2 texCoord)
{
    float2 offset = texelSize * radius * 0.5;

    float4 c0 = tex2D(Sampler, texCoord + float2(-1, -1) * offset);
    float4 c1 = tex2D(Sampler, texCoord + float2( 0, -1) * offset);
    float4 c2 = tex2D(Sampler, texCoord + float2( 1, -1) * offset);
    float4 c3 = tex2D(Sampler, texCoord + float2(-1,  0) * offset);
    float4 c4 = tex2D(Sampler, texCoord + float2( 0,  0) * offset);
    float4 c5 = tex2D(Sampler, texCoord + float2( 1,  0) * offset);
    float4 c6 = tex2D(Sampler, texCoord + float2(-1,  1) * offset);
    float4 c7 = tex2D(Sampler, texCoord + float2( 0,  1) * offset);
    float4 c8 = tex2D(Sampler, texCoord + float2( 1,  1) * offset);

    float4 result = 0.0625f * (c0 + 2.0 * c1 + c2 + 2.0 * c3 + 4.0 * c4 + 2.0 * c5 + c6 + 2.0 * c7 + c8);
    return result;
}

// Pixel shaders (entrypoints per technique)
float4 PSDownsample(VSOutput input) : SV_Target
{
    float4 blur = DownsamplePS(input.TexCoord);
    return float4(blur.rgb, 1.0);
}

float4 PSUpsampleMasked(VSOutput input) : SV_Target
{
    float4 blurUpscaled = UpsamplePS(input.TexCoord);
    float maskA = tex2D(MaskSampler, input.TexCoord).a;

    // Only output blurred color where mask alpha > 0.5
    if (maskA > 0.5)
        return blurUpscaled;
    else
        return float4(0, 0, 0, 0);
}

// Techniques
technique Downsample
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSDownsample();
    }
}

technique UpsampleMasked
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSUpsampleMasked();
    }
}

// Parameters
float2 texelSize;
float radius;

Texture2D Texture0      : register(t0);
Texture2D MaskTexture   : register(t1);

// Samplers
SamplerState Sampler : register(s0)
{
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Clamp;
    AddressV = Clamp;
};

SamplerState MaskSampler : register(s1)
{
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Downsample pass (weighted 5-group mix from original)
float4 DownsamplePS(float2 texCoord)
{
    float2 offset = texelSize * 0.5;

    float4 c0  = tex2d(Sampler, texCoord + float2(-2, -2) * offset);
    float4 c1  = tex2d(Sampler, texCoord + float2( 0, -2) * offset);
    float4 c2  = tex2d(Sampler, texCoord + float2( 2, -2) * offset);
    float4 c3  = tex2d(Sampler, texCoord + float2(-1, -1) * offset);
    float4 c4  = tex2d(Sampler, texCoord + float2( 1, -1) * offset);
    float4 c5  = tex2d(Sampler, texCoord + float2(-2,  0) * offset);
    float4 c6  = tex2d(Sampler, texCoord + float2( 0,  0) * offset);
    float4 c7  = tex2d(Sampler, texCoord + float2( 2,  0) * offset);
    float4 c8  = tex2d(Sampler, texCoord + float2(-1,  1) * offset);
    float4 c9  = tex2d(Sampler, texCoord + float2( 1,  1) * offset);
    float4 c10 = tex2d(Sampler, texCoord + float2(-2,  2) * offset);
    float4 c11 = tex2d(Sampler, texCoord + float2( 0,  2) * offset);
    float4 c12 = tex2d(Sampler, texCoord + float2( 2,  2) * offset);

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

    float4 c0 = tex2d(Sampler, texCoord + float2(-1, -1) * offset);
    float4 c1 = tex2d(Sampler, texCoord + float2( 0, -1) * offset);
    float4 c2 = tex2d(Sampler, texCoord + float2( 1, -1) * offset);
    float4 c3 = tex2d(Sampler, texCoord + float2(-1,  0) * offset);
    float4 c4 = tex2d(Sampler, texCoord + float2( 0,  0) * offset);
    float4 c5 = tex2d(Sampler, texCoord + float2( 1,  0) * offset);
    float4 c6 = tex2d(Sampler, texCoord + float2(-1,  1) * offset);
    float4 c7 = tex2d(Sampler, texCoord + float2( 0,  1) * offset);
    float4 c8 = tex2d(Sampler, texCoord + float2( 1,  1) * offset);

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
    float maskA = tex2d(MaskSampler, input.TexCoord).a;

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
        VertexShader = compile VS_SHADERMODEL VSMain();
        PixelShader  = compile PS_SHADERMODEL PSDownsample();
    }
}

technique UpsampleMasked
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL VSMain();
        PixelShader  = compile PS_SHADERMODEL PSUpsampleMasked();
    }
}

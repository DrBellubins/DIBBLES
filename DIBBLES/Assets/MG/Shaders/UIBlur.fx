// Multi-pass Gaussian Blur with Mask Support (for MonoGame Effects)
// Author: Copilot Space

// === Parameters ===
float2 texelSize;          // 1 / texture resolution (set per pass)
float radius;              // Blur radius in pixels (used to scale offsets)

// --- Fixed kernel size for HLSL compatibility (must match C# code) ---
#define KERNEL_SIZE 9
float kernel[KERNEL_SIZE]; // Gaussian weights (set from C#)

// === Textures ===
Texture2D Texture0      : register(t0); // Input (for blur stage)
Texture2D MaskTexture   : register(t1); // Mask (for masking stage)

// === Samplers ===
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

// === Vertex structs ===
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

// === Vertex Shader ===
VSOutput VSMain(VSInput input)
{
    VSOutput output;
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;

    return output;
}

// === Gaussian Blur (Horizontal) ===
float4 GaussianBlurHPS(float2 texCoord)
{
    float4 color = float4(0,0,0,0);

    // Kernel is always KERNEL_SIZE (must be odd, e.g. 9)
    int halfKernel = KERNEL_SIZE / 2;

    // Sample horizontally
    [unroll]
    for (int i = 0; i < KERNEL_SIZE; i++)
    {
        int offset = i - halfKernel;
        float2 sampleOffset = float2(offset, 0) * texelSize * radius;
        color += tex2D(Sampler, texCoord + sampleOffset) * kernel[i];
    }

    return color;
}

// === Gaussian Blur (Vertical) ===
float4 GaussianBlurVPS(float2 texCoord)
{
    float4 color = float4(0,0,0,0);

    int halfKernel = KERNEL_SIZE / 2;

    // Sample vertically
    [unroll]
    for (int i = 0; i < KERNEL_SIZE; i++)
    {
        int offset = i - halfKernel;
        float2 sampleOffset = float2(0, offset) * texelSize * radius;
        color += tex2D(Sampler, texCoord + sampleOffset) * kernel[i];
    }

    return color;
}

// === Masked Upsample Pass ===
float4 MaskedPS(float2 texCoord)
{
    float4 blurColor = tex2D(Sampler, texCoord); // Already blurred
    float maskA = tex2D(MaskSampler, texCoord).a;

    // Only output blurred color where mask alpha > 0.5
    if (maskA > 0.5)
        return blurColor;
    else
        return float4(0,0,0,0);
}

// === Shader Entrypoints ===
float4 PSBlurH(VSOutput input) : SV_Target
{
    return GaussianBlurHPS(input.TexCoord);
}

float4 PSBlurV(VSOutput input) : SV_Target
{
    return GaussianBlurVPS(input.TexCoord);
}

float4 PSMask(VSOutput input) : SV_Target
{
    return MaskedPS(input.TexCoord);
}

// === Techniques ===
technique GaussianBlurH
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSBlurH();
    }
}

technique GaussianBlurV
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSBlurV();
    }
}

technique MaskedComposite
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSMask();
    }
}

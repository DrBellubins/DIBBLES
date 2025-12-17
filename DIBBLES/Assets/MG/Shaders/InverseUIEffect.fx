// InverseUIEffect.fx
// Renders an inverted grayscale of the input scene, multiplied by the alpha
// from a mask texture (your inverseUITarget). Intended for fullscreen quad/SpriteBatch use.

// SpriteBatch-style transform (or set to identity for a fullscreen triangle/quad)
float4x4 MatrixTransform;

// Scene texture (backbuffer or a RenderTarget you pass when drawing)
texture Texture;

// Mask texture (the highlight render target, inverseUITarget). Only alpha is used.
texture MaskTexture;

// Samplers
sampler TextureSampler
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler MaskSampler
{
    Texture = <MaskTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Optional tweakables
float3 LumaWeights = float3(0.299f, 0.587f, 0.114f); // NTSC luma
float InvertStrength = 1.0f; // 0..1

struct VS_INPUT
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VS_OUTPUT VSMain(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

float4 PSMain(VS_OUTPUT input) : COLOR0
{
    // Sample scene
    float4 scene = tex2D(TextureSampler, input.TexCoord);

    // Grayscale luminance
    float gray = dot(scene.rgb, LumaWeights);

    // Invert to white-on-dark look
    float invGray = lerp(gray, 1.0f - gray, saturate(InvertStrength));

    // Sample mask alpha from inverseUITarget
    float maskA = tex2D(MaskSampler, input.TexCoord).a;

    // Premultiply color by alpha (works with AlphaBlend)
    float3 outRGB = invGray.xxx * maskA;

    return float4(outRGB, maskA);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSMain();
    }
}

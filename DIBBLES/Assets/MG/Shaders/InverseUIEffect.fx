// InverseUIEffect.fx
// Samples BackBuffer, inverts to grayscale, and multiplies by inverseUITarget alpha.

float4x4 MatrixTransform;

Texture2D Texture;
Texture2D MaskTexture;

sampler2D TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D MaskSampler = sampler_state
{
    Texture = <MaskTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

float3 LumaWeights = float3(0.299f, 0.587f, 0.114f);
float InvertStrength = 1.0f;

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
    float4 scene = tex2D(TextureSampler, input.TexCoord);

    float gray = dot(scene.rgb, LumaWeights);
    float invGray = lerp(gray, 1.0f - gray, saturate(InvertStrength));

    float maskA = tex2D(MaskSampler, input.TexCoord).a;

    // Premultiply for blending
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

// Terrain.fx (MonoGame MGFX for OpenGL/Linux)
// Use SM2.0/3.0 features and tex2D sampling!

float4x4 World;
float4x4 View;
float4x4 Projection;

float3 CameraPos;
float FogNear;
float FogFar;
float4 FogColor;

texture Texture0;

sampler2D TextureSampler = sampler_state
{
    Texture = <Texture0>;
};

struct VertexInput
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
};

struct PixelInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
    float3 WorldPos : TEXCOORD1;
};

PixelInput VS(VertexInput input)
{
    PixelInput output;

    float4 worldPos = mul(float4(input.Position, 1), World);

    float4 viewPos = mul(worldPos, View);
    output.Position = mul(viewPos, Projection);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.WorldPos = worldPos.xyz;

    return output;
}

float4 PS(PixelInput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);
    float4 blockColor = texColor * input.Color;

    float dist = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 finalColor = lerp(blockColor, FogColor, fogFactor);
    finalColor.a = blockColor.a;

    return finalColor;
}

technique Terrain
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}

texture AtlasTex;

float4x4 World;
float4x4 View;
float4x4 Projection;

float3 CameraPos;
float CameraNear;
float CameraFar;

float FogNear;
float FogFar;
float4 FogColor;

sampler2D AtlasSampler = sampler_state
{
    Texture = <AtlasTex>;
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
    float4 viewPos  = mul(worldPos, View);

    output.Position = mul(viewPos, Projection);
    output.TexCoord = input.TexCoord;
    output.Color    = input.Color;
    output.WorldPos = worldPos.xyz;

    return output;
}

float4 PS_Color(PixelInput input) : COLOR0
{
    float4 texColor = tex2D(AtlasSampler, input.TexCoord);
    float4 blockColor = texColor * input.Color;

    // Fog
    float dist = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));

    float4 finalColor = lerp(blockColor, FogColor, fogFactor);
    finalColor.a = blockColor.a;

    return finalColor;
}

technique Terrain
{
    pass Color
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS_Color();
    }
}

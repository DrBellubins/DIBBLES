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
    float3 NormalVS : TEXCOORD2; // view-space normal
};

PixelInput VS(VertexInput input)
{
    PixelInput output;

    float4 worldPos = mul(float4(input.Position, 1), World);
    float4 viewPos  = mul(worldPos, View);

    // Transform normal to world, then to view space
    float3 nWorld = normalize(mul(input.Normal, (float3x3)World));
    float3 nView  = normalize(mul(nWorld, (float3x3)View));

    output.Position = mul(viewPos, Projection);
    output.TexCoord = input.TexCoord;
    output.Color    = input.Color;
    output.WorldPos = worldPos.xyz;
    output.NormalVS = nView;

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

float4 PS_Normal(PixelInput input) : COLOR0
{
    // Draw view-space normal raw with range -1 to 1
}

float4 PS_Depth(PixelInput input) : COLOR0
{
    // Draw linearized depth 0..1
}

technique TerrainOpaque
{
    pass Color
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS_Color();
    }
    pass Normal
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS_Normal();
    }
    pass Depth
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS_Depth();
    }
}

technique TerrainTransparent
{
    pass Color
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS_Color();
    }
}

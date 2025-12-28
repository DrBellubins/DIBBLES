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

struct PSOutput
{
    float4 Color0 : COLOR0; // Albedo with fog
    float4 Color1 : COLOR1; // View-space normal encoded to [0..1]
    float4 Color2 : COLOR2; // Linear depth [0..1]
};

PSOutput PS(PixelInput input)
{
    PSOutput o;

    float4 texColor  = tex2D(TextureSampler, input.TexCoord);
    float4 blockColor = texColor * input.Color;

    // Fog (same as your current shader)
    float dist = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 finalColor = lerp(blockColor, FogColor, fogFactor);
    finalColor.a = blockColor.a;

    // Encode view-space normal into [0..1]
    float3 n = normalize(input.NormalVS);
    float3 nEnc = n * 0.5f + 0.5f;

    // Linear depth in [0..1], using view-space z (assumes forward is -Z)
    float4 viewPos = mul(float4(input.WorldPos, 1.0f), View);
    float zVS = -viewPos.z;
    float dLin = saturate((zVS - FogNear) / (FogFar - FogNear));

    o.Color0 = finalColor;
    o.Color1 = float4(nEnc, 1.0f);
    o.Color2 = float4(dLin, dLin, dLin, 1.0f);

    return o;
}

technique TerrainOpaque
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}

technique TerrainTransparent
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}

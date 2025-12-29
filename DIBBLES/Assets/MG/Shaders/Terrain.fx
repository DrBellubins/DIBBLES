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

// Add CameraNear/Far are already declared; reuse them to write normalized linear depth to RT1.
// Extend VS->PS payload and return two render targets (COLOR0=color, COLOR1=depth).

struct PixelInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
    float3 WorldPos : TEXCOORD1;
    float  ViewZ    : TEXCOORD2;   // +Z forward distance in view space
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

    // View-space forward is -Z; use -viewPos.z for positive distance
    output.ViewZ = -viewPos.z;

    return output;
}

struct PixelOutput
{
    float4 Color0 : COLOR0; // scene color
    float4 Color1 : COLOR1; // linear depth in [0..1]
};

PixelOutput PS_Color(PixelInput input)
{
    float4 texColor  = tex2D(AtlasSampler, input.TexCoord);
    float4 blockColor = texColor * input.Color;

    // Fog
    float dist = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 finalColor = lerp(blockColor, FogColor, fogFactor);
    finalColor.a = blockColor.a;

    // Normalized linear depth (near=0, far=1)
    float depth01 = saturate((input.ViewZ - CameraNear) / (CameraFar - CameraNear));

    PixelOutput output;

    output.Color0 = finalColor;
    output.Color1 = float4(depth01, depth01, depth01, 1.0f); // SSAO samples .r

    return output;
}

technique Terrain
{
    pass Color
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS_Color();
    }
}

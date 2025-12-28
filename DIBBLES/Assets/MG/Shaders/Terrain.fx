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
    float  ViewZ    : TEXCOORD3; // positive view-space Z (distance along camera forward)
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

    // Make view-space Z positive in front of the camera
    // XNA/MonoGame view space typically looks down -Z, so negate
    output.ViewZ = -viewPos.z;

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
    // Encode view-space normal from [-1..1] to [0..1] for storage
    float3 nVS = normalize(input.NormalVS);
    float3 enc = nVS * 0.5f + 0.5f;
    return float4(enc, 1.0f);
}

float4 PS_Depth(PixelInput input) : COLOR0
{
    // Linear depth in [0..1], near=0, far=1
    float z = input.ViewZ;

    // Guard against negative or zero z (behind camera), clamp to near
    z = max(z, CameraNear);

    float dlin = saturate((z - CameraNear) / (CameraFar - CameraNear));
    return float4(dlin, dlin, dlin, 1.0f);
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

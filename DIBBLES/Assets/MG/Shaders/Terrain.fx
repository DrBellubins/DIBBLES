texture Texture0;

float4x4 World;
float4x4 View;
float4x4 Projection;
float3 CameraPos;

float CameraNear;
float CameraFar;
float AlphaCutoff;

float FogNear;
float FogFar;
float4 FogColor;

sampler2D TextureSampler = sampler_state
{
    Texture = <Texture0>;  // bind the atlas
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

// Normal and view-depth to PixelInput, and return multiple render targets
struct PixelInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
    float3 WorldPos : TEXCOORD1;

    // World-space normal and view-space depth
    float3 WorldNormal : TEXCOORD2;
    float  ViewDepth   : TEXCOORD3;
};

struct VertexInput
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
};


// MRT output struct
struct PSOutput
{
    float4 Color0   : COLOR0; // scene color
    float4 NormalRT : COLOR1; // encoded normals
    float4 DepthRT  : COLOR2; // linear depth
};

PixelInput VS(VertexInput input)
{
    PixelInput output;

    float4 worldPos = mul(float4(input.Position, 1), World);
    float4 viewPos  = mul(worldPos, View);

    output.Position    = mul(viewPos, Projection);
    output.TexCoord    = input.TexCoord;
    output.Color       = input.Color;
    output.WorldPos    = worldPos.xyz;

    // Transform normal to world space (assumes uniform scale; use inverse-transpose for non-uniform)
    float3 worldNormal = normalize(mul(float4(input.Normal, 0), World).xyz);
    output.WorldNormal = worldNormal;

    // View-space depth (positive forward; negate if your View uses right-handed)
    output.ViewDepth = -viewPos.z;

    return output;
}

PSOutput PS(PixelInput input)
{
    float4 texColor   = tex2D(TextureSampler, input.TexCoord);
    float4 blockColor = texColor * input.Color;

    // Hard cutouts: use texture alpha (not multiplied color)
    clip(texColor.a - AlphaCutoff);

    float dist = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 finalColor = lerp(blockColor, FogColor, fogFactor);
    finalColor.a = blockColor.a;

    // Encode VIEW-SPACE normal to [0..1]
    // This aligns the hemisphere check in SSAO with the reconstructed view-space positions.
    float3 viewNormal = normalize(mul(float4(input.WorldNormal, 0.0f), View).xyz);
    float3 normalEnc = viewNormal * 0.5f + 0.5f;

    float depthLin = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));

    PSOutput output;
    output.Color0   = finalColor;
    output.NormalRT = float4(normalEnc, 1.0f);
    output.DepthRT  = float4(depthLin, depthLin, depthLin, 1.0f);
    return output;
}

technique Terrain
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}

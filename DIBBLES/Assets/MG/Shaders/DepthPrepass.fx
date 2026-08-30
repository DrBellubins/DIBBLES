float4x4 World;
float4x4 View;
float4x4 Projection;

float CameraNear;
float CameraFar;

// Depth-only pass: output normalized linear view-space depth in color
struct VSInput
{
    float3 Position : POSITION0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float  LinearDepth01 : TEXCOORD0;
};

VSOutput VS_Depth(VSInput input)
{
    VSOutput o;

    float4 worldPos = mul(float4(input.Position, 1.0f), World);
    float4 viewPos  = mul(worldPos, View);
    float4 clipPos  = mul(viewPos, Projection);

    // View-space forward is -Z; use -viewPos.z for positive distance
    float viewZ = -viewPos.z;

    // Normalize to [0..1] using near/far
    float d01 = saturate((viewZ - CameraNear) / (CameraFar - CameraNear));

    o.Position = clipPos;
    o.LinearDepth01 = d01;

    return o;
}

float4 PS_Depth(VSOutput input) : SV_Target0
{
    float d = input.LinearDepth01;
    return float4(d, d, d, 1.0f);
}

technique DepthPrepass
{
    pass P0
    {
        VertexShader = compile vs_6_0 VS_Depth();
        PixelShader  = compile ps_6_0 PS_Depth();
    }
}

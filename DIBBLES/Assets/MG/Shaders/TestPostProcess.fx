float2 ScreenSize;

texture ColorTex;
sampler ColorSampler = sampler_state
{
    Texture = <ColorTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

texture NormalTex;
sampler NormalSampler = sampler_state
{
    Texture = <NormalTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

texture DepthTex;
sampler DepthSampler = sampler_state
{
    Texture = <DepthTex>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

struct VSInput
{
    float3 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position, 1.0f);
    output.TexCoord = input.TexCoord;
    return output;
}

float4 PSMain(VSOutput input) : SV_Target0
{
    float4 sceneCol = tex2D(ColorSampler,  input.TexCoord);
    float3 normalRGB = tex2D(NormalSampler, input.TexCoord).rgb;
    float  depth     = tex2D(DepthSampler,  input.TexCoord).r;

    // Remap normals [-1..1] -> [0..1] if they are stored as RGB
    float3 normalVis = saturate(normalRGB * 2.0f - 1.0f) * 0.5f + 0.5f;

    // Simple test composite:
    // - Blend scene color with visualized normals
    // - Darken slightly by depth as a cheap fog preview
    //float fog = saturate(depth);
    //float3 combined = lerp(sceneCol.rgb, normalVis, 0.5f);
    //combined *= (1.0f - 0.25f * fog);

    float3 depthRGB = float3(depth, depth, depth);

    return float4(depthRGB, sceneCol.a);
}

technique TestPostProcess
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader  = compile ps_3_0 PSMain();
    }
}

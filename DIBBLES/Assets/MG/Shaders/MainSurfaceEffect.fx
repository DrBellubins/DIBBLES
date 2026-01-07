// MainSurfaceEffect.fx
// General-purpose MRT shader similar to BasicEffect but writing to:
// - COLOR0: final color
// - COLOR1: normalized linear depth [0..1]
// - COLOR2: view-space normals encoded to [0..1]
//
// Supports combinations of Texture and VertexColor like BasicEffect,
// plus optional fog. Lighting is not included.
//
// Notes:
// - AlphaCutoff discards near-transparent pixels to avoid occluding geometry behind cutouts.
// - Assumes uniform scaling for normals; use inverse-transpose if needed.
// - Ignore sampler binding issues as requested.

texture DiffuseTex;

float4x4 World;
float4x4 View;
float4x4 Projection;

float3 CameraPos;
float CameraNear;
float CameraFar;

float FogNear;
float FogFar;
float4 FogColor;

float4 DiffuseColor;     // Base material color/tint (RGBA)

static const float AlphaCutoff = 0.35f;

sampler2D DiffuseSampler = sampler_state
{
    Texture = <DiffuseTex>;
};

struct VSInput
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
};

struct VSInputTx
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

struct VSInputVc
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float4 Color    : COLOR0;
};

struct VSInputTxVc
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
};

struct PSInput
{
    float4 Position   : POSITION0;
    float2 TexCoord   : TEXCOORD0;
    float4 Color      : COLOR0;
    float3 WorldPos   : TEXCOORD1;
    float  ViewDepth  : TEXCOORD2;   // -viewPos.z (positive forward)
    float3 ViewNormal : TEXCOORD3;
};

PSInput VSBasic(VSInput input)
{
    PSInput o;

    float4 worldPos = mul(float4(input.Position, 1), World);
    float4 viewPos  = mul(worldPos, View);

    // Transform normal to view space (assumes uniform scale)
    float3 worldNormal = mul(float4(input.Normal, 0), World).xyz;
    float3 viewNormal  = mul(float4(worldNormal, 0), View).xyz;

    o.Position   = mul(viewPos, Projection);
    o.WorldPos   = worldPos.xyz;
    o.ViewDepth  = -viewPos.z;
    o.ViewNormal = normalize(viewNormal);
    o.TexCoord   = float2(0, 0);
    o.Color      = DiffuseColor;

    return o;
}

PSInput VSBasicTx(VSInputTx input)
{
    PSInput o;

    float4 worldPos = mul(float4(input.Position, 1), World);
    float4 viewPos  = mul(worldPos, View);

    float3 worldNormal = mul(float4(input.Normal, 0), World).xyz;
    float3 viewNormal  = mul(float4(worldNormal, 0), View).xyz;

    o.Position   = mul(viewPos, Projection);
    o.WorldPos   = worldPos.xyz;
    o.ViewDepth  = -viewPos.z;
    o.ViewNormal = normalize(viewNormal);
    o.TexCoord   = input.TexCoord;
    o.Color      = DiffuseColor;

    return o;
}

PSInput VSBasicVc(VSInputVc input)
{
    PSInput o;

    float4 worldPos = mul(float4(input.Position, 1), World);
    float4 viewPos  = mul(worldPos, View);

    float3 worldNormal = mul(float4(input.Normal, 0), World).xyz;
    float3 viewNormal  = mul(float4(worldNormal, 0), View).xyz;

    o.Position   = mul(viewPos, Projection);
    o.WorldPos   = worldPos.xyz;
    o.ViewDepth  = -viewPos.z;
    o.ViewNormal = normalize(viewNormal);
    o.TexCoord   = float2(0, 0);
    o.Color      = DiffuseColor * input.Color;

    return o;
}

PSInput VSBasicTxVc(VSInputTxVc input)
{
    PSInput o;

    float4 worldPos = mul(float4(input.Position, 1), World);
    float4 viewPos  = mul(worldPos, View);

    float3 worldNormal = mul(float4(input.Normal, 0), World).xyz;
    float3 viewNormal  = mul(float4(worldNormal, 0), View).xyz;

    o.Position   = mul(viewPos, Projection);
    o.WorldPos   = worldPos.xyz;
    o.ViewDepth  = -viewPos.z;
    o.ViewNormal = normalize(viewNormal);
    o.TexCoord   = input.TexCoord;
    o.Color      = DiffuseColor * input.Color;

    return o;
}

struct PSOutput
{
    float4 Color0 : COLOR0; // scene color
    float4 Color1 : COLOR1; // linear depth (replicated to RGB)
    float4 Color2 : COLOR2; // view-space normals encoded to [0..1]
};

float4 applyFog(float4 color, float3 worldPos)
{
    float dist = distance(worldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 fogged = lerp(color, FogColor, fogFactor);
    fogged.a = color.a;
    return fogged;
}

PSOutput PS_NoTex_Fog(PSInput input)
{
    float4 baseColor = input.Color;

    clip(baseColor.a - AlphaCutoff);

    float4 finalColor = applyFog(baseColor, input.WorldPos);

    float depth01 = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));
    float3 n01    = normalize(input.ViewNormal) * 0.5f + 0.5f;

    PSOutput o;
    o.Color0 = finalColor;
    o.Color1 = float4(depth01, depth01, depth01, 1.0f);
    o.Color2 = float4(n01, 1.0f);
    return o;
}

PSOutput PS_NoTex_NoFog(PSInput input)
{
    float4 baseColor = input.Color;
    clip(baseColor.a - AlphaCutoff);

    float depth01 = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));
    float3 n01    = normalize(input.ViewNormal) * 0.5f + 0.5f;

    PSOutput o;
    o.Color0 = baseColor;
    o.Color1 = float4(depth01, depth01, depth01, 1.0f);
    o.Color2 = float4(n01, 1.0f);
    return o;
}

PSOutput PS_Tex_Fog(PSInput input)
{
    float4 texColor  = tex2D(DiffuseSampler, input.TexCoord);
    float4 baseColor = texColor * input.Color;

    clip(baseColor.a - AlphaCutoff);

    float4 finalColor = applyFog(baseColor, input.WorldPos);

    float depth01 = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));
    float3 n01    = normalize(input.ViewNormal) * 0.5f + 0.5f;

    PSOutput o;
    o.Color0 = finalColor;
    o.Color1 = float4(depth01, depth01, depth01, 1.0f);
    o.Color2 = float4(n01, 1.0f);
    return o;
}

PSOutput PS_Tex_NoFog(PSInput input)
{
    float4 texColor  = tex2D(DiffuseSampler, input.TexCoord);
    float4 baseColor = texColor * input.Color;

    clip(baseColor.a - AlphaCutoff);

    float depth01 = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));
    float3 n01    = normalize(input.ViewNormal) * 0.5f + 0.5f;

    PSOutput o;
    o.Color0 = baseColor;
    o.Color1 = float4(depth01, depth01, depth01, 1.0f);
    o.Color2 = float4(n01, 1.0f);
    return o;
}

// Techniques mirroring BasicEffect combinations (but MRT outputs)
technique MainSurfaceEffect
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasic();
        PixelShader  = compile ps_3_0 PS_NoTex_Fog();
    }
}

technique MainSurfaceEffect_NoFog
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasic();
        PixelShader  = compile ps_3_0 PS_NoTex_NoFog();
    }
}

technique MainSurfaceEffect_VertexColor
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasicVc();
        PixelShader  = compile ps_3_0 PS_NoTex_Fog();
    }
}

technique MainSurfaceEffect_VertexColor_NoFog
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasicVc();
        PixelShader  = compile ps_3_0 PS_NoTex_NoFog();
    }
}

technique MainSurfaceEffect_Texture
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasicTx();
        PixelShader  = compile ps_3_0 PS_Tex_Fog();
    }
}

technique MainSurfaceEffect_Texture_NoFog
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasicTx();
        PixelShader  = compile ps_3_0 PS_Tex_NoFog();
    }
}

technique MainSurfaceEffect_Texture_VertexColor
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasicTxVc();
        PixelShader  = compile ps_3_0 PS_Tex_Fog();
    }
}

technique MainSurfaceEffect_Texture_VertexColor_NoFog
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSBasicTxVc();
        PixelShader  = compile ps_3_0 PS_Tex_NoFog();
    }
}

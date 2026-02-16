texture AtlasTex;
texture EmissiveAtlasTex;

int UseGreedyMeshing; // Toggle (0 or 1). Set from TerrainMesh.UseGreedyMeshing

float4x4 World;
float4x4 View;
float4x4 Projection;

float3 CameraPos;
float CameraNear;
float CameraFar;

float EmissiveStrength;
float FogNear;
float FogFar;
float4 FogColor;

bool IsTranslucentPass;

// Alpha cutoff for foliage cutout; pixels below this alpha are discarded
static const float AlphaCutoff = 0.35f;

sampler2D AtlasSampler = sampler_state
{
    Texture = <AtlasTex>;
};

sampler2D EmissiveAtlasSampler = sampler_state
{
    Texture = <EmissiveAtlasTex>;
};

struct VertexInput
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0; // local tile-space for greedy OR absolute atlas for non-greedy
    float4 Color    : COLOR0;

    float4 UVRect   : TEXCOORD1; // (x,y,w,h) atlas sub-rect; zero for non-greedy
    float4 UVBasis  : TEXCOORD2; // (uX,uY,vX,vY) atlas-space basis from BR-BL and TL-BL

    float4 EmisUVRect  : TEXCOORD3;
    float4 EmisUVBasis : TEXCOORD4;
};

// Add CameraNear/Far are already declared; reuse them to write normalized linear depth to RT1.
// Extend VS->PS payload and return two render targets (COLOR0=color, COLOR1=depth).

struct PixelInput
{
    float4 Position     : POSITION0;
    float2 TexCoord     : TEXCOORD0;
    float4 Color        : COLOR0;
    float3 WorldPos     : TEXCOORD1;
    float  ViewDepth    : TEXCOORD2;   // +Z forward distance in view space
    float3 ViewNormal   : TEXCOORD3;

    float4 UVRect       : TEXCOORD4;
    float4 UVBasis      : TEXCOORD5;

    float4 EmisUVRect   : TEXCOORD6;
    float4 EmisUVBasis  : TEXCOORD7;
};

struct PixelOutput
{
    float4 Color0 : COLOR0; // scene color
    float4 Color1 : COLOR1; // linear depth in [0..1]
    float4 Color2 : COLOR2; // view-space normals encoded to [0..1]
    float4 Color3 : COLOR3; // emissive color (RGB) + mask in A
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

    // View-space forward is -Z; use -viewPos.z for positive distance
    output.ViewDepth = -viewPos.z;

    // Compute view-space normal (assumes uniform scaling; use inverse-transpose for non-uniform)
    float3 worldNormal = mul(float4(input.Normal, 0), World).xyz;
    float3 viewNormal = mul(float4(worldNormal, 0), View).xyz;

    output.ViewNormal = normalize(viewNormal);

    output.UVRect = input.UVRect;
    output.UVBasis = input.UVBasis;

    output.EmisUVRect = input.EmisUVRect;
    output.EmisUVBasis = input.EmisUVBasis;

    return output;
}

float2 projectToLocal(float2 uv, float2 rectOrigin, float2 basisU, float2 basisV)
{
    float2 d = uv - rectOrigin;

    float a = basisU.x;
    float b = basisV.x;
    float c = basisU.y;
    float d2 = basisV.y;

    float det = a * d2 - b * c;

    if (abs(det) < 1e-6)
    {
        return float2(0, 0);
    }

    float invDet = 1.0 / det;

    float u = ( d2 * (d.x) - b * (d.y)) * invDet;
    float v = (-c  * (d.x) + a * (d.y)) * invDet;

    return float2(u, v);
}

PixelOutput PS_Color(PixelInput input)
{
    // Base atlas UV
    float2 atlasUV;

    if (UseGreedyMeshing > 0.5 && (input.UVRect.z > 0.0 || input.UVRect.w > 0.0))
    {
        float2 basisU = float2(input.UVBasis.x, input.UVBasis.y);
        float2 basisV = float2(input.UVBasis.z, input.UVBasis.w);

        atlasUV = float2(input.UVRect.x, input.UVRect.y)
                + frac(input.TexCoord.x) * basisU
                + frac(input.TexCoord.y) * basisV;
    }
    else
    {
        atlasUV = input.TexCoord;
    }

    float4 texColor = tex2D(AtlasSampler, atlasUV);
    float4 blockColor = texColor * input.Color;

    // Hand cutoff vs transparency
    float alpha = blockColor.a;

    if (!IsTranslucentPass && alpha < 1.0)
        clip(alpha - AlphaCutoff);

    float dist = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 finalColor = lerp(blockColor, FogColor, fogFactor);

    finalColor.a = alpha;

    // Emissive atlas UV (separate layout)
    float2 emisUV;

    if (UseGreedyMeshing > 0.5 && (input.EmisUVRect.z > 0.0 || input.EmisUVRect.w > 0.0))
    {
        float2 eU = float2(input.EmisUVBasis.x, input.EmisUVBasis.y);
        float2 eV = float2(input.EmisUVBasis.z, input.EmisUVBasis.w);

        emisUV = float2(input.EmisUVRect.x, input.EmisUVRect.y)
               + frac(input.TexCoord.x) * eU
               + frac(input.TexCoord.y) * eV;
    }
    else
    {
        // Non-greedy: project base UV into local (u,v) of base rect, then remap to emissive rect/basis
        float2 bU = float2(input.UVBasis.x, input.UVBasis.y);
        float2 bV = float2(input.UVBasis.z, input.UVBasis.w);
        float2 local = projectToLocal(atlasUV, float2(input.UVRect.x, input.UVRect.y), bU, bV);

        float2 eU = float2(input.EmisUVBasis.x, input.EmisUVBasis.y);
        float2 eV = float2(input.EmisUVBasis.z, input.EmisUVBasis.w);

        emisUV = float2(input.EmisUVRect.x, input.EmisUVRect.y) + local.x * eU + local.y * eV;
    }

    float4 emissiveColor = tex2D(EmissiveAtlasSampler, emisUV);
    float3 emissiveRGB = emissiveColor.rgb * emissiveColor.a * EmissiveStrength;

    float depth01 = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));
    float3 normal = normalize(input.ViewNormal);
    float3 normal01 = normal * 0.5f + 0.5f;

    PixelOutput output;

    output.Color0 = finalColor;

    // Only draw depth/normal on opaque
    if (finalColor.a >= 1.0)
    {
        output.Color1 = float4(depth01, depth01, depth01, 1.0f);
        output.Color2 = float4(normal01, 1.0f);
    }

    output.Color3 = float4(emissiveRGB, 1.0f);

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

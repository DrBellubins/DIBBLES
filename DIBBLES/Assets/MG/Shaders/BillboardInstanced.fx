texture AtlasTex;

float4x4 View;
float4x4 Projection;

float3 CameraPos;
float CameraNear;
float CameraFar;

float FogNear;
float FogFar;
float4 FogColor;

// Atlas tile rect for GrassBlades: (x,y,w,h) in [0..1] atlas UV space
float4 UVRect;

// Alpha cutoff for foliage cutout; pixels below this alpha are discarded
static const float AlphaCutoff = 0.35f;

sampler2D AtlasSampler = sampler_state
{
    Texture = <AtlasTex>;
};

struct VertexInput
{
    // Stream 0: base crossed-quad local mesh
    float3 Position   : POSITION0;
    float2 Tex        : TEXCOORD0;

    // Stream 1: per-instance data
    float3 Center     : POSITION1;     // world-space center
    float  Angle      : TEXCOORD1;     // rotation around Y
    float2 Size       : TEXCOORD2;     // halfWidth, height
    float4 InstanceCol: COLOR1;        // vertex color (lighting)
};

struct PixelInput
{
    float4 Position : POSITION0;
    float2 Tex      : TEXCOORD0;
    float4 Color    : COLOR0;
    float3 WorldPos : TEXCOORD1;
    float  ViewDepth: TEXCOORD2;
    float3 ViewNorm : TEXCOORD3;
};

PixelInput VS(VertexInput input)
{
    PixelInput o;

    // Rotate local XZ by Angle around Y, then scale by Size
    float s = sin(input.Angle);
    float c = cos(input.Angle);

    float3 local = input.Position;

    float3 rotated;
    rotated.x = local.x * c + local.z * s;
    rotated.y = local.y;
    rotated.z = -local.x * s + local.z * c;

    // Scale: XZ by halfWidth, Y by height
    rotated.x *= (input.Size.x / 0.5f);
    rotated.y *= (input.Size.y / 1.0f);
    rotated.z *= (input.Size.x / 0.5f);

    float3 worldPos = input.Center + rotated;

    float4 viewPos = mul(float4(worldPos, 1), View);

    o.Position  = mul(viewPos, Projection);
    o.WorldPos  = worldPos;
    o.ViewDepth = -viewPos.z;                // +Z forward distance in view space
    o.ViewNorm  = float3(0, 1, 0);           // billboard: Up normal

    // Map quad UV (0..1) to atlas rect
    float2 uv;
    uv.x = UVRect.x + input.Tex.x * UVRect.z;
    uv.y = UVRect.y + input.Tex.y * UVRect.w;

    o.Tex   = uv;
    o.Color = input.InstanceCol;

    return o;
}

struct PixelOutput
{
    float4 Color0 : COLOR0; // scene color
    float4 Color1 : COLOR1; // linear depth
    float4 Color2 : COLOR2; // view-space normals
};

PixelOutput PS_Color(PixelInput input)
{
    float4 texColor  = tex2D(AtlasSampler, input.Tex);
    float4 blockColor = texColor * input.Color;

    // Hard alpha cutout to prevent transparent texels from writing depth
    // Discards pixels with alpha below threshold so they don't occlude behind billboards.
    float alpha = blockColor.a;
    clip(alpha - AlphaCutoff);

    // Fog
    float dist     = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 finalColor = lerp(blockColor, FogColor, fogFactor);
    finalColor.a = blockColor.a;

    // Normalized linear depth
    float depth01 = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));

    // Encode view-space normal to [0,1]
    float3 nrm = normalize(input.ViewNorm);
    float3 n01 = nrm * 0.5f + 0.5f;

    PixelOutput o;
    o.Color0 = finalColor;
    o.Color1 = float4(depth01, depth01, depth01, 1.0f);
    o.Color2 = float4(n01, 1.0f);
    return o;
}

technique BillboardInstanced
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS_Color();
    }
}

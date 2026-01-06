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

// Wind parameters (set from C# as needed; keep simple and fast)
// Time drives advection of a coherent world-space field.
// WindDir is XZ direction; WindFrequency controls spatial scale.
// WindAmplitude controls lateral bend; BendExponent increases bend toward the tip.
// SideCurlAmount scales inward curl; SideCurlExponent controls height falloff.
float Time;
static const float2 WindDir = float2(1.0f, 0.0f);
static const float  WindSpeed = 0.6f;
static const float  WindFrequency = 0.08f;
static const float  WindAmplitude = 0.35f;
static const float  BendExponent = 2.0f;
static const float  SideCurlAmount = 0.45f;
static const float  SideCurlExponent = 1.2f;

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

// ------------------------------
// Fast hash and 3D value noise
// ------------------------------
float hash3(float3 p)
{
    // Cheap hash: sine-based; good enough for vegetation sway
    return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float valueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);

    // Quintic smoothstep for better continuity
    float3 u = f * f * (3.0 - 2.0 * f);

    // 8 corners
    float n000 = hash3(i + float3(0,0,0));
    float n100 = hash3(i + float3(1,0,0));
    float n010 = hash3(i + float3(0,1,0));
    float n110 = hash3(i + float3(1,1,0));
    float n001 = hash3(i + float3(0,0,1));
    float n101 = hash3(i + float3(1,0,1));
    float n011 = hash3(i + float3(0,1,1));
    float n111 = hash3(i + float3(1,1,1));

    // Trilinear interpolation
    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);

    float nxy0 = lerp(nx00, nx10, u.y);
    float nxy1 = lerp(nx01, nx11, u.y);

    float nxyz = lerp(nxy0, nxy1, u.z);
    return nxyz; // [0..1]
}

float fbm3(float3 p)
{
    // 2 octaves for cost reasons: cheap but lively
    float v = 0.0;
    float a = 0.5;

    v += a * valueNoise3D(p);
    p *= 2.0;
    a *= 0.5;

    v += a * valueNoise3D(p);
    return v; // [0..1]
}

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

    // ------------------------------
    // Wind bend & curl (single coherent field)
    // ------------------------------
    float3 worldPosNoWind = input.Center + rotated;

    // Wind dirs
    float2 wdir2 = normalize(WindDir);
    float3 wdir3 = float3(wdir2.x, 0.0f, wdir2.y);
    float3 perp3 = float3(-wdir2.y, 0.0f, wdir2.x);

    // Advected sample position for coherent flow
    float3 flow = wdir3 * (Time * WindSpeed);

    // Single FBM field drives both sway and curl
    float low = fbm3(worldPosNoWind * WindFrequency + flow); // [0..1]
    float low01 = saturate(low);
    float low11 = (low01 * 2.0f - 1.0f);                     // [-1..1]
    float gust = abs(low11);                                  // [0..1] stronger fold on gusts

    // Height-normalized bend mask so base stays anchored
    float t = saturate(rotated.y / max(input.Size.y, 1e-4));
    float bendMask = pow(t, BendExponent);

    // Lateral displacement along wind direction
    float lateral = low11 * WindAmplitude * bendMask;

    // Side curl: fold inward across the blade width, modulated by gusts and height
    // u = local horizontal across quad in [-1..1] so left/right sides move toward center
    float u = input.Tex.x * 2.0f - 1.0f;
    float sideCurl = u * SideCurlAmount * pow(t, SideCurlExponent) * gust;

    // Apply displacements
    rotated.xyz += wdir3 * lateral;   // main bend with wind
    rotated.xyz += perp3 * sideCurl;  // inward curl that strengthens on gusts

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

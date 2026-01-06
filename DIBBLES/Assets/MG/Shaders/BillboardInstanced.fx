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
// WindDir is XZ direction, is set at runtime periodically in a random direction.
// WindFrequency controls spatial scale.
// WindAmplitude controls lateral bend; BendExponent increases bend toward the tip.
// SideCurlAmount scales inward curl; SideCurlExponent controls height falloff.
float Time;
float2 WindDir = float2(0.0f, 0.0f);

static const float  WindSpeed = 0.6f;
static const float  WindFrequency = 0.01f;
static const float  WindAmplitude = 0.15f;
static const float  BendExponent = 2.0f;
static const float  SideCurlAmount = 0.45f;
static const float  SideCurlExponent = 1.2f;

// Band-limited sinusoid field settings (2–3 harmonics)
// Spatial wavevectors (in 2D XZ); magnitudes are later scaled by WindFrequency
static const float2 K1 = float2(0.8f, 0.3f);
static const float2 K2 = float2(-0.4f, 1.1f);
static const float2 K3 = float2(0.2f, -0.7f);

// Temporal angular frequencies (radians/sec), modestly separated bands
static const float W1 = 0.9f;
static const float W2 = 1.6f;
static const float W3 = 2.3f;

// Harmonic weights (must sum <= 1 to keep [-1,1] range)
static const float A1 = 0.6f;
static const float A2 = 0.3f;
static const float A3 = 0.1f;

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

// Small per-instance phase jitter derived from Angle
float hash1(float x)
{
    return frac(sin(x * 12.9898f) * 43758.5453f);
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
    // Wind bend & curl (band-limited sinusoids)
    // ------------------------------
    float3 worldPosNoWind = input.Center + rotated;

    // Wind dirs
    float2 wdir2 = normalize(WindDir);
    float3 wdir3 = float3(wdir2.x, 0.0f, wdir2.y);
    float3 perp3 = float3(-wdir2.y, 0.0f, wdir2.x);

    // Advected sample position for coherent flow
    float3 flow = wdir3 * (Time * WindSpeed);

    // 2D field input (XZ) scaled by WindFrequency and advected along wind
    float2 p = (worldPosNoWind.xz * WindFrequency) + flow.xz;

    // Per-instance phase offsets (stable across time, varies per instance)
    /*float phi1 = hash1(input.Angle * 17.0f) * 6.2831853f;
    float phi2 = hash1(input.Angle * -31.0f) * 6.2831853f;
    float phi3 = hash1(input.Angle * 59.0f) * 6.2831853f;*/

    // Disable per-instance phase offset for smoother, natrual look.
    float phi1 = hash1(input.Angle) * 6.2831853f;
    float phi2 = hash1(input.Angle) * 6.2831853f;
    float phi3 = hash1(input.Angle) * 6.2831853f;

    // Band-limited sum of sinusoids (smooth C∞ field), roughly in [-1,1]
    float sinusoids =
        A1 * sin(dot(p, K1) + W1 * Time + phi1) +
        A2 * sin(dot(p, K2) + W2 * Time + phi2) +
        A3 * sin(dot(p, K3) + W3 * Time + phi3);

    // Main sway signal in [-1,1]
    float low11 = saturate((sinusoids + 1.0f) * 0.5f) * 2.0f - 1.0f;

    // Smooth “gust” measure: squared amplitude avoids abs() cusp
    float gust = saturate(sinusoids * sinusoids);

    // Height-normalized bend mask so base stays anchored
    float t = saturate(rotated.y / max(input.Size.y, 1e-4f));
    float bendMask = pow(t, BendExponent);

    // Lateral displacement along wind direction
    float lateral = low11 * WindAmplitude * bendMask;

    // Side curl: fold inward across the blade width, modulated by gusts and height
    float u = input.Tex.x * 2.0f - 1.0f; // -1..1 across quad
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

    // Wind debug
    /*float3 colRGB = input.InstanceCol.rgb * sinusoids;
    float4 outputCol = float4(colRGB, input.InstanceCol.a);

    o.Color = outputCol;*/

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

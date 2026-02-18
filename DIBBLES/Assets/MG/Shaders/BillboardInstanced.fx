// TODO: Wind only seems to blow billboards in one direction
// TODO: Weird stretching with mesh on flowers (and sometimes grass)

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

// Time drives advection of a coherent world-space field.
float Time;

// Debug toggle
static const bool WindDebug = false;

// Wind parameters (kept simple and fast)
static const float2 WindDir = float2(1.0f, 0.0f); // XZ direction
static const float  WindDirRotateSpeed = 0.06f;   // radians/sec (slow)
static const float  WindSpeed = 0.6f;             // advection speed
static const float  WindFrequency = 0.4f;         // spatial scale
static const float  WindAmplitude = 0.15f;        // lateral bend scale
static const float  BendExponent = 2.0f;          // bend grows toward tip
static const float  SideCurlAmount = 0.45f;       // inward curl magnitude
static const float  SideCurlExponent = 1.2f;      // curl grows toward tip

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

    // Wind debug payload
    float3 WindBend : TEXCOORD4; // x=lateralBend, y=sideCurlOffset, z=gustStrength
};

// Hash helpers
float hash1(float x)
{
    return frac(sin(x * 12.9898f) * 43758.5453f);
}

// Quasi-random unit direction on the circle from a scalar “seed”
float2 unitDir(float h)
{
    float a = 6.2831853f * frac(sin(h * 19.19f) * 43758.5453f);
    return float2(cos(a), sin(a));
}

float2 rotate2(float2 v, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

// Parameters for multi-wave sums
#define MAIN_WAVES 7   // try 5 if you need cheaper
#define GUST_WAVES 4   // try 3 if you need cheaper

// Band-limited isotropic “random waves” field (sum of plane waves)
float randomWaves(float2 p, float t, float baseSeed, float kMin, float kMax, float wMin, float wMax)
{
    float s = 0.0f;

    [unroll]
    for (int i = 0; i < MAIN_WAVES; ++i)
    {
        float id = baseSeed + (float)i * 17.0f;

        float2 dir = unitDir(id);
        float k = lerp(kMin, kMax, hash1(id + 3.1f));     // spatial frequency band
        float w = lerp(wMin, wMax, hash1(id + 7.7f));     // temporal band
        float ph = 6.2831853f * hash1(id + 11.9f);        // independent phase

        s += sin(dot(p * WindFrequency * k, dir) + w * t + ph);
    }

    return s / MAIN_WAVES; // keep in ~[-1,1]
}

// Lower-frequency set for gust strength (separate seed so it decorrelates)
float randomWavesGust(float2 p, float t, float baseSeed)
{
    float s = 0.0f;

    [unroll]
    for (int i = 0; i < GUST_WAVES; ++i)
    {
        float id = baseSeed + (float)i * 29.0f;

        float2 dir = unitDir(id);
        float k = lerp(0.25f, 0.7f,  hash1(id + 2.3f));   // broad, low frequency
        float w = lerp(0.35f, 0.9f,  hash1(id + 5.5f));   // slower time
        float ph = 6.2831853f * hash1(id + 9.9f);

        s += sin(dot(p * WindFrequency * k, dir) + w * t + ph);
    }

    return s / GUST_WAVES; // ~[-1,1]
}

PixelInput VS(VertexInput input)
{
    PixelInput pixelOut;

    // Rotate local crossed-quad around Y by the instance Angle (billboard yaw)
    float sinAngle = sin(input.Angle);
    float cosAngle = cos(input.Angle);

    float3 localPosition = input.Position;

    float3 rotatedPosition;
    rotatedPosition.x = localPosition.x * cosAngle + localPosition.z * sinAngle;
    rotatedPosition.y = localPosition.y;
    rotatedPosition.z = -localPosition.x * sinAngle + localPosition.z * cosAngle;

    // Scale: XZ by halfWidth, Y by height (base quad is halfWidth=0.5, height=1.0)
    /*float widthScale  = input.Size.x / 0.5f;
    float heightScale = input.Size.y / 1.0f;

    rotatedPosition.x *= widthScale;
    rotatedPosition.y *= heightScale;
    rotatedPosition.z *= widthScale;*/

    static const float BillboardHalfWidth = 0.5f;
    static const float BillboardHeight    = 1.0f;

    // Base mesh is already built with halfW = 0.5 and height = 1.0,
    // so scale = 1 keeps current visual size.
    // If we want bigger/smaller globally, change these multipliers.
    static const float WidthScale  = 1.0f;
    static const float HeightScale = 1.0f;

    rotatedPosition.x *= WidthScale;
    rotatedPosition.y *= HeightScale;
    rotatedPosition.z *= WidthScale;

    // World position without wind deformation (used to sample the wind field)
    float3 worldPositionUnbent = input.Center + rotatedPosition;

    // Wind direction basis in XZ: forward direction and a perpendicular axis
    //float2 windDirection2D = normalize(WindDir);
    float2 windDirection2D = normalize(rotate2(WindDir, Time * WindDirRotateSpeed));
    float3 windDirection3D = float3(windDirection2D.x, 0.0f, windDirection2D.y);
    float3 windPerpendicular3D = float3(-windDirection2D.y, 0.0f, windDirection2D.x);

    // Coherent advection over time (moves the wind field, not the geometry directly)
    float3 windFlowOffset = windDirection3D * (Time * WindSpeed);

    // Sample coordinate for the wind field (2D in XZ), scaled and advected
    float2 windFieldCoord = (worldPositionUnbent.xz) + windFlowOffset.xz;

    // Per-instance base seeds to keep motion stable per blade and decorrelate fields
    float baseSeed = 37.0f + 97.0f * hash1(input.Angle * 0.773f);   // main field
    float gustSeed = 113.0f + 59.0f * hash1(input.Angle * 1.337f);  // gust field

    // Isotropic multi-wave sway in [-1,1]
    float windSignal = randomWaves(windFieldCoord, Time, baseSeed, 0.6f, 1.6f, 0.7f, 2.0f);

    // Slow-varying gust strength in [0,1], decorrelated from windSignal
    float gustStrength = saturate(0.5f + 0.5f * randomWavesGust(windFieldCoord, Time, gustSeed));

    // Height factor along the blade so the base stays anchored (0 at base, 1 at tip)
    float heightFactor = saturate(rotatedPosition.y / max(BillboardHeight * HeightScale, 1e-4f));

    // Bend increases toward the tip
    float bendMask = pow(heightFactor, BendExponent);

    // Lateral bend along wind direction
    float lateralBend = windSignal * WindAmplitude * bendMask;

    // Width coordinate across the quad: -1 (left) .. +1 (right)
    float widthCoordinate = input.Tex.x * 2.0f - 1.0f;

    // Inward curl across blade width, stronger on gusts and higher up
    float sideCurlOffset = widthCoordinate
                         * SideCurlAmount
                         * pow(heightFactor, SideCurlExponent)
                         * gustStrength;

    // Apply wind deformation
    rotatedPosition.xyz += windDirection3D      * lateralBend;      // main sway
    rotatedPosition.xyz += windPerpendicular3D * sideCurlOffset;    // inward curl

    // Final world and view positions
    float3 worldPosition = input.Center + rotatedPosition;
    float4 viewPosition  = mul(float4(worldPosition, 1.0f), View);

    // Compute per-vertex world-space plane normal for the crossed quads:
    // Quad A (base z==0) has local normal +Z; Quad B (base x==0) has local normal +X.
    // Rotate by instance yaw, add a small tilt from curl, flip to face camera, then convert to view space.
    float3 baseNormalLocal = (abs(localPosition.z) < 1e-6f) ? float3(0.0f, 0.0f, 1.0f)  // Quad A
                                                        : float3(1.0f, 0.0f, 0.0f); // Quad B

    // Rotate normal by yaw (same rotation as position)
    float3 worldPlaneNormal;
    worldPlaneNormal.x = baseNormalLocal.x * cosAngle + baseNormalLocal.z * sinAngle;
    worldPlaneNormal.y = 0.0f;
    worldPlaneNormal.z = -baseNormalLocal.x * sinAngle + baseNormalLocal.z * cosAngle;

    // Optional: slight tilt with the curl so normals aren’t perfectly planar
    float curlTilt = SideCurlAmount * pow(heightFactor, SideCurlExponent) * gustStrength * 0.2f;
    worldPlaneNormal += windPerpendicular3D * (widthCoordinate * curlTilt);
    worldPlaneNormal = normalize(worldPlaneNormal);

    // Flip normal to face the camera for double-sided billboards
    float3 toCamera = normalize(CameraPos - worldPosition);

    if (dot(worldPlaneNormal, toCamera) < 0.0f)
        worldPlaneNormal = -worldPlaneNormal;

    // View-space normal (ignore translation by using w=0)
    float3 viewNormal = mul(float4(worldPlaneNormal, 0.0f), View).xyz;
    viewNormal = normalize(viewNormal);

    // Clip-space position for rasterizer
    pixelOut.Position  = mul(viewPosition, Projection);

    // World-space for fog computations
    pixelOut.WorldPos  = worldPosition;

    // Positive forward distance in view space (camera looks down -Z)
    pixelOut.ViewDepth = -viewPosition.z;

    // Use the computed view-space normal for the normal buffer
    pixelOut.ViewNorm  = viewNormal;

    // Map quad UV (0..1) into atlas rectangle
    float2 atlasUV;
    atlasUV.x = UVRect.x + input.Tex.x * UVRect.z;
    atlasUV.y = UVRect.y + input.Tex.y * UVRect.w;
    pixelOut.Tex = atlasUV;

    // Keep your per-instance lighting tint
    pixelOut.Color = input.InstanceCol;

    pixelOut.WindBend = float3(lateralBend, sideCurlOffset, gustStrength);

    return pixelOut;
}

struct PixelOutput
{
    float4 Color0 : COLOR0; // scene color
    float4 Color1 : COLOR1; // linear depth
    float4 Color2 : COLOR2; // view-space normals
};

PixelOutput PS_Color(PixelInput input)
{
    float4 texColor = tex2D(AtlasSampler, input.Tex);
    float4 blockColor = texColor * input.Color;

    // Hard alpha cutout so near-transparent texels don’t occlude
    clip(blockColor.a - AlphaCutoff);

    // Fog
    float dist      = distance(input.WorldPos, CameraPos);
    float fogFactor = saturate((dist - FogNear) / (FogFar - FogNear));
    float4 finalColor = lerp(blockColor, FogColor, fogFactor);
    finalColor.a = blockColor.a;

    // Normalized linear depth
    float depth01 = saturate((input.ViewDepth - CameraNear) / (CameraFar - CameraNear));

    // Encode view-space normal to [0,1]
    float3 nrm = normalize(input.ViewNorm);
    float3 n01 = nrm * 0.5f + 0.5f;

    PixelOutput o;

    if (WindDebug)
    {
        // Visualize: R=lateral bend, G=gust, B=curl
        float r = saturate(0.5f + input.WindBend.x);
        float g = saturate(input.WindBend.z);
        float b = saturate(0.5f + input.WindBend.y);

        o.Color0 = float4(r, g, b, 1.0f);
        o.Color1 = float4(depth01, depth01, depth01, 1.0f);
        o.Color2 = float4(n01, 1.0f);
        return o;
    }

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

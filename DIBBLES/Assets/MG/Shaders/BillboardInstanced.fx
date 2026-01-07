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

// Value-noise hash
float rand11(float2 p)
{
    return frac(sin(dot(p, float2(12.9898f, 78.233f))) * 43758.5453f);
}

// Smooth bilinear blend helper (value noise)
float bilerp(float a00, float a10, float a01, float a11, float2 w)
{
    float ax0 = lerp(a00, a10, w.x);
    float ax1 = lerp(a01, a11, w.x);
    return lerp(ax0, ax1, w.y);
}

PixelInput VS(VertexInput input)
{
    PixelInput pixelOut;

    // Rotate local crossed-quad around Y by the instance Angle (billboard yaw)
    float sinAngle = sin(input.Angle);
    float cosAngle = cos(input.Angle);

    float3 localPosition = input.Position;

    float3 rotatedPosition;
    rotatedPosition.x =  localPosition.x * cosAngle + localPosition.z * sinAngle;
    rotatedPosition.y =  localPosition.y;
    rotatedPosition.z = -localPosition.x * sinAngle + localPosition.z * cosAngle;

    // Scale: XZ by halfWidth, Y by height (base quad is halfWidth=0.5, height=1.0)
    float widthScale  = input.Size.x / 0.5f;
    float heightScale = input.Size.y / 1.0f;

    rotatedPosition.x *= widthScale;
    rotatedPosition.y *= heightScale;
    rotatedPosition.z *= widthScale;

    // World position without wind deformation (used to sample the wind field)
    float3 worldPositionUnbent = input.Center + rotatedPosition;

    // Wind direction basis in XZ: forward direction and a perpendicular axis
    float2 windDirection2D   = normalize(WindDir);
    float3 windDirection3D   = float3(windDirection2D.x, 0.0f, windDirection2D.y);
    float3 windPerpendicular3D = float3(-windDirection2D.y, 0.0f, windDirection2D.x);

    // Coherent advection over time (moves the wind field, not the geometry directly)
    float3 windFlowOffset = windDirection3D * (Time * WindSpeed);

    // Sample coordinate for the band-limited wind field (2D in XZ), scaled and advected
    float2 windFieldCoord = (worldPositionUnbent.xz * WindFrequency) + windFlowOffset.xz;

    // Stable per-instance phases (derived from Angle) to keep motion consistent
    //float phase1 = hash1(input.Angle) * 6.2831853f; // 2*pi

    float phase1 = hash1(worldPositionUnbent.x * WindFrequency);
    float phase2 = phase1;
    float phase3 = phase2;

    // Smooth wind signal in approximately [-1,1] using 3 harmonics
    float windSignal =
        A1 * sin(dot(windFieldCoord, K1) + W1 * Time + phase1) +
        A2 * sin(dot(windFieldCoord, K2) + W2 * Time + phase2) +
        A3 * sin(dot(windFieldCoord, K3) + W3 * Time + phase3);

    // Map to signed sway [-1,1] and compute a smooth gust strength [0,1]
    float swaySigned    = saturate((windSignal + 1.0f) * 0.5f) * 2.0f - 1.0f;
    float gustStrength  = saturate(windSignal * windSignal);

    // Height factor along the blade so the base stays anchored (0 at base, 1 at tip)
    float heightFactor = saturate(rotatedPosition.y / max(input.Size.y, 1e-4f));

    // Bend increases toward the tip
    float bendMask = pow(heightFactor, BendExponent);

    // Lateral bend along wind direction
    float lateralBend = swaySigned * WindAmplitude * bendMask;

    // Width coordinate across the quad: -1 (left) .. +1 (right)
    float widthCoordinate = input.Tex.x * 2.0f - 1.0f;

    // Inward curl across blade width, stronger on gusts and higher up
    float sideCurlOffset = widthCoordinate
                         * SideCurlAmount
                         * pow(heightFactor, SideCurlExponent)
                         * gustStrength;

    // Apply wind deformation
    rotatedPosition.xyz += windDirection3D    * lateralBend;   // main sway
    rotatedPosition.xyz += windPerpendicular3D * sideCurlOffset; // inward curl

    // Final world and view positions
    float3 worldPosition = input.Center + rotatedPosition;
    float4 viewPosition  = mul(float4(worldPosition, 1.0f), View);

    // Clip-space position for rasterizer
    pixelOut.Position  = mul(viewPosition, Projection);

    // World-space for fog computations
    pixelOut.WorldPos  = worldPosition;

    // Positive forward distance in view space (MonoGame convention: camera looks down -Z)
    pixelOut.ViewDepth = -viewPosition.z;

    // Simple upward normal for billboards (used by normal buffer)
    pixelOut.ViewNorm  = float3(0.0f, 1.0f, 0.0f);

    // Map quad UV (0..1) into atlas rectangle
    float2 atlasUV;
    atlasUV.x = UVRect.x + input.Tex.x * UVRect.z;
    atlasUV.y = UVRect.y + input.Tex.y * UVRect.w;

    pixelOut.Tex   = atlasUV;

    float3 colorRGB = input.InstanceCol.rgb * lateralBend;
    float4 colorOutput = float4(colorRGB, input.InstanceCol.a);
    //float4 colorOutput = input.InstanceCol;

    pixelOut.Color = colorOutput; // per-instance lighting tint

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

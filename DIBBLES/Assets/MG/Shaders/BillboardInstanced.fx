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

float rand11(float2 p)
{
    return frac(sin(dot(p, float2(12.9898f, 78.233f))) * 43758.5453f);
}

PixelInput VS(VertexInput input)
{
    PixelInput pixelOut;

    // Rotate local crossed-quad around Y by the instance Angle
    float sinAngle = sin(input.Angle);
    float cosAngle = cos(input.Angle);

    float3 localPosition = input.Position;

    float3 rotatedPosition;
    rotatedPosition.x =  localPosition.x * cosAngle + localPosition.z * sinAngle;
    rotatedPosition.y =  localPosition.y;
    rotatedPosition.z = -localPosition.x * sinAngle + localPosition.z * cosAngle;

    // Scale: XZ by halfWidth, Y by height
    float widthScale  = input.Size.x / 0.5f;
    float heightScale = input.Size.y / 1.0f;

    rotatedPosition.x *= widthScale;
    rotatedPosition.y *= heightScale;
    rotatedPosition.z *= widthScale;

    // Use instance center for sampling so the whole blade stays coherent
    float3 worldPositionUnbent = input.Center;

    // Wind direction base (global)
    float2 windDirBase2D = normalize(WindDir);
    float3 windDirBase3D = float3(windDirBase2D.x, 0.0f, windDirBase2D.y);
    float3 windPerpBase3D = float3(-windDirBase2D.y, 0.0f, windDirBase2D.x);

    // Field advection
    float3 windFlowOffset = windDirBase3D * (Time * WindSpeed);

    // Low-frequency grid coord (cluster size ≈ 1/WindFrequency)
    float2 gustCoord = (worldPositionUnbent.xz * WindFrequency) + windFlowOffset.xz;

    // Cluster cell and a stable random per-cell
    float2 cell = floor(gustCoord);

    // One coherent phase per cell
    float clusterPhase = rand11(cell) * 6.2831853f; // [0..2π]

    // Optional: coherent direction jitter per cell, blended with global wind dir
    float clusterAngle = rand11(cell + float2(17, 13)) * 6.2831853f;
    float2 clusterDir2D = float2(cos(clusterAngle), sin(clusterAngle));
    float2 windDirection2D = normalize(lerp(windDirBase2D, clusterDir2D, 0.4f));
    float3 windDirection3D = float3(windDirection2D.x, 0.0f, windDirection2D.y);
    float3 windPerpendicular3D = float3(-windDirection2D.y, 0.0f, windDirection2D.x);

    // Tiny per-instance jitter so blades within a cluster aren’t identical
    float smallJitter = (hash1(input.Angle) - 0.5f) * 0.3f;

    // Harmonically related phases from the same cluster seed
    float phase1 = clusterPhase + smallJitter;
    float phase2 = clusterPhase * 1.33f + smallJitter;
    float phase3 = clusterPhase * 1.77f + smallJitter;

    // Sample the band-limited wind field at the instance center
    float2 fieldCoord = gustCoord;

    float windSignal =
        A1 * sin(dot(fieldCoord, K1) + W1 * Time + phase1) +
        A2 * sin(dot(fieldCoord, K2) + W2 * Time + phase2) +
        A3 * sin(dot(fieldCoord, K3) + W3 * Time + phase3);

    // Signed sway and gust strength
    float swaySigned   = saturate((windSignal + 1.0f) * 0.5f) * 2.0f - 1.0f;
    float gustStrength = saturate(windSignal * windSignal);

    // Height factor along the blade so the base stays anchored
    float heightFactor = saturate(rotatedPosition.y / max(input.Size.y, 1e-4f));
    float bendMask     = pow(heightFactor, BendExponent);

    // Lateral bend along wind direction
    float lateralBend = swaySigned * WindAmplitude * bendMask;

    // Inward curl across blade width, stronger on gusts and higher up
    float widthCoordinate = input.Tex.x * 2.0f - 1.0f;
    float sideCurlOffset  = widthCoordinate
                          * SideCurlAmount
                          * pow(heightFactor, SideCurlExponent)
                          * gustStrength;

    // Apply wind deformation to the rotated local vertex
    float3 deformed = rotatedPosition;
    deformed.xyz += windDirection3D     * lateralBend;
    deformed.xyz += windPerpendicular3D * sideCurlOffset;

    // Final world/view transforms
    float3 worldPosition = input.Center + deformed;
    float4 viewPosition  = mul(float4(worldPosition, 1.0f), View);

    pixelOut.Position  = mul(viewPosition, Projection);
    pixelOut.WorldPos  = worldPosition;
    pixelOut.ViewDepth = -viewPosition.z;
    pixelOut.ViewNorm  = float3(0.0f, 1.0f, 0.0f);

    // Atlas UV mapping
    float2 atlasUV;
    atlasUV.x = UVRect.x + input.Tex.x * UVRect.z;
    atlasUV.y = UVRect.y + input.Tex.y * UVRect.w;
    pixelOut.Tex = atlasUV;

    // Keep your original lighting tint (avoid multiplying by windSignal here to prevent visual “sync” cues)
    pixelOut.Color = input.InstanceCol;

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

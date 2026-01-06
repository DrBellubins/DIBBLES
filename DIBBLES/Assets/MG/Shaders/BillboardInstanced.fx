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
// WindDir is XZ direction, is set at runtime, rotates slowly at WindDirRotationSpeed
// WindFrequency controls spatial scale.
// WindAmplitude controls lateral bend; BendExponent increases bend toward the tip.
// SideCurlAmount scales inward curl; SideCurlExponent controls height falloff.
float Time;
float2 WindDir = float2(0.0f, 0.0f);

static const float WindSpeed = 0.6f;
static const float WindFrequency = 0.01f;
static const float WindAmplitude = 0.15f;
static const float WindDirRotationSpeed = 0.4f;
static const float BendExponent = 2.0f;
static const float SideCurlAmount = 0.45f;
static const float SideCurlExponent = 1.2f;

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
    PixelInput outData;

    // 1) Rotate the local crossed-quad around Y by per-instance Angle
    float sinAngle = sin(input.Angle);
    float cosAngle = cos(input.Angle);

    float3 localPos = input.Position;

    float3 rotatedPos;
    rotatedPos.x =  localPos.x * cosAngle + localPos.z * sinAngle;
    rotatedPos.y =  localPos.y;
    rotatedPos.z = -localPos.x * sinAngle + localPos.z * cosAngle;

    // 2) Scale: XZ by halfWidth, Y by height (base quad is halfWidth=0.5, height=1.0)
    float halfWidthScale = input.Size.x / 0.5f;
    float heightScale    = input.Size.y / 1.0f;

    rotatedPos.x *= halfWidthScale;
    rotatedPos.y *= heightScale;
    rotatedPos.z *= halfWidthScale;

    // 3) Base world position without wind deformation
    float3 worldPosUnbent = input.Center + rotatedPos;

    // 4) Compute a smoothly rotating wind direction in XZ from WindDir
    //    WindDir is the initial direction; we rotate it by Time * WindDirRotationSpeed.
    float windRotationAngle = Time * WindDirRotationSpeed;
    float cosRot = cos(windRotationAngle);
    float sinRot = sin(windRotationAngle);

    float2 baseWindDir = (dot(WindDir, WindDir) > 1e-6f) ? normalize(WindDir) : float2(1.0f, 0.0f);

    float2 windDir2D = float2(
        baseWindDir.x * cosRot - baseWindDir.y * sinRot,
        baseWindDir.x * sinRot + baseWindDir.y * cosRot
    );

    // Guard against degenerate direction
    windDir2D = (dot(windDir2D, windDir2D) > 1e-6f) ? normalize(windDir2D) : float2(1.0f, 0.0f);

    float3 windDir3D       = float3(windDir2D.x, 0.0f, windDir2D.y);
    float3 perpendicular3D = float3(-windDir2D.y, 0.0f, windDir2D.x);

    // 5) Advect the sampling position in the wind direction (coherent motion over the world)
    float3 flowOffset = windDir3D * (Time * WindSpeed);

    // 6) Band-limited sinusoid field (very cheap and perfectly smooth)
    //    Use XZ world coordinates scaled by WindFrequency and advected by flowOffset.
    float2 fieldCoord = (worldPosUnbent.xz * WindFrequency) + flowOffset.xz;

    // Per-instance phase offsets for diversity (stable across frames)
    float phi1 = hash1(input.Angle * 17.0f) * 6.2831853f;
    float phi2 = hash1(input.Angle * -31.0f) * 6.2831853f;
    float phi3 = hash1(input.Angle * 59.0f) * 6.2831853f;

    float sinField =
        A1 * sin(dot(fieldCoord, K1) + W1 * Time + phi1) +
        A2 * sin(dot(fieldCoord, K2) + W2 * Time + phi2) +
        A3 * sin(dot(fieldCoord, K3) + W3 * Time + phi3);

    // Map to [-1,1] sway; squared amplitude as a smooth gust strength in [0,1]
    float sway01  = saturate((sinField + 1.0f) * 0.5f);
    float sway11  = sway01 * 2.0f - 1.0f;
    float gust    = saturate(sinField * sinField);

    // 7) Height factor along the blade so the base stays anchored
    float bladeHeight = max(input.Size.y, 1e-4f);
    float heightFactor = saturate(rotatedPos.y / bladeHeight);
    float bendProfile  = pow(heightFactor, BendExponent);

    // 8) Lateral bend along wind direction and inward side curl across the quad
    float lateralOffset = sway11 * WindAmplitude * bendProfile;

    // Width coordinate across the quad: -1 at left, +1 at right
    float widthCoord = input.Tex.x * 2.0f - 1.0f;

    float sideCurl = widthCoord
                   * SideCurlAmount
                   * pow(heightFactor, SideCurlExponent)
                   * gust;

    // 9) Apply wind deformation
    rotatedPos.xyz += windDir3D       * lateralOffset;
    rotatedPos.xyz += perpendicular3D * sideCurl;

    // 10) Final world and view transforms
    float3 worldPos = input.Center + rotatedPos;
    float4 viewPos  = mul(float4(worldPos, 1.0f), View);

    outData.Position  = mul(viewPos, Projection);
    outData.WorldPos  = worldPos;
    outData.ViewDepth = -viewPos.z;              // +Z forward in view space
    outData.ViewNorm  = float3(0.0f, 1.0f, 0.0f);// Up-normal for billboards

    // 11) Map quad UVs (0..1) into atlas tile rectangle
    float2 atlasUV;
    atlasUV.x = UVRect.x + input.Tex.x * UVRect.z;
    atlasUV.y = UVRect.y + input.Tex.y * UVRect.w;

    outData.Tex   = atlasUV;
    outData.Color = input.InstanceCol;           // Per-instance lighting tint

    return outData;
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

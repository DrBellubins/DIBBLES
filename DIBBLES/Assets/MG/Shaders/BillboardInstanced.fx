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

// Wind parameters (set from C#)
// Time: seconds since start
// WindDir: normalized XZ direction of wind
// WindSpeed: how fast the noise field advects along WindDir
// WindFrequency: spatial frequency of noise (larger = tighter features)
// WindAmplitude: max bend distance in world units
// BendExponent: controls how strongly bend increases near the tip (>=1)
float Time;
static const float2 WindDir = float2(1.0f, 0.0f);
static const float WindSpeed = 0.6f;
static const float WindFrequency = 0.1f;
static const float WindAmplitude = 0.5f;
static const float BendExponent = 2.5f;

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
    // Wind bend in vertex shader
    // ------------------------------
    // World-space pos before wind
    float3 worldPosNoWind = input.Center + rotated;

    // Advected sample position: flow = WindDir * Time * WindSpeed
    float3 flow = float3(WindDir.x, 0.0f, WindDir.y) * Time * WindSpeed;

    // Spatially coherent 3D FBM in world space
    float n = fbm3(worldPosNoWind * WindFrequency + flow); // [0..1]
    float sway = (n * 2.0f - 1.0f) * WindAmplitude;        // [-amp..amp]

    // Height-weighted bend (anchor base, bend tip)
    // rotated.y is in [0..height]; normalize by Size.y
    float bendMask = saturate(rotated.y / max(input.Size.y, 1e-4));
    bendMask = pow(bendMask, BendExponent);

    // Apply lateral displacement along wind direction
    rotated.x += WindDir.x * (sway * bendMask);
    rotated.z += WindDir.y * (sway * bendMask);

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

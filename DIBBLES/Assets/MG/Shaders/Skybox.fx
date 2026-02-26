// Simple atmospheric scattering + sun and moon sprite
float4x4 World;
float4x4 View;
float4x4 Projection;

float3 SkyZenithColor;
float3 SkyHorizonColor;

texture SunTexture : register(t0);
texture MoonTexture : register(t1);

float3 SunDirection;
float3 MoonDirection;

float TimeOfDay; // 0..24

sampler2D SunSampler : register(s0) = sampler_state
{
    Filter = POINT;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D MoonSampler : register(s1) = sampler_state
{
    Filter = POINT;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VSInput
{
    float3 Position : POSITION0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float3 World : TEXCOORD0;
};

struct PixelOutput
{
    float4 Color : COLOR0; // scene color
    float4 Emissive : COLOR1; // emissive color (RGB) + mask in A
};

PixelInput VS(VSInput input)
{
    PixelInput output;

    float4 world = mul(float4(input.Position, 1.0), World);

    output.Position = mul(mul(world, View), Projection);
    output.World = world.xyz;

    return output;
}

// Sky color calculation
float3 computeSky(float3 dir)
{
    float t = saturate(dir.y * 0.5 + 0.5); // t = 0: horizon, t = 1: zenith (up)
    float3 skyColor = lerp(SkyHorizonColor, SkyZenithColor, t);

    return skyColor;
}

float2 computeLocalUV(float3 viewDir, float3 center, float size)
{
    float3 arbitrary = (abs(center.y) > 0.9) ? float3(1,0,0) : float3(0,1,0);
    float3 tan1 = normalize(cross(arbitrary, center));
    float3 tan2 = cross(center, tan1);
    float2 offset = float2(dot(viewDir, tan1), dot(viewDir, tan2));

    return 0.5 + offset / (2.0 * size);  // Exact fit: UV edges align with angular radius
}

PixelOutput PS(PixelInput input) : SV_Target
{
    float3 viewDir = normalize(input.World);

    float3 baseColor = computeSky(viewDir);

    // ────────────────────────────────────────────────
    // Sun / Moon parameters
    // ────────────────────────────────────────────────
    const float sunMoonAngularRadius = 0.04;  // in radians – adjust 0.025–0.06 as needed

    float3 sunCenter = -SunDirection;
    float3 moonCenter = -MoonDirection;

    // ────────────────────────────────────────────────
    // UVs – exact fit to angular disk
    // ────────────────────────────────────────────────
    float2 sunUV = computeLocalUV(viewDir, sunCenter, sunMoonAngularRadius);
    float2 moonUV = computeLocalUV(viewDir, moonCenter, sunMoonAngularRadius);

    float4 sunTex = tex2D(SunSampler, sunUV);
    float4 moonTex = tex2D(MoonSampler, moonUV);

    // ────────────────────────────────────────────────
    // Intensity control
    // ────────────────────────────────────────────────
    float sunBaseMultiplier = 1.0;    // base appearance in color buffer
    float moonBaseMultiplier = 1.0;

    float sunGlowMultiplier = 1.5;    // brighter version → emissive / bloom
    float moonGlowMultiplier = 1.0;   // usually lower than sun

    // ────────────────────────────────────────────────
    // Base (non-multiplied) versions – go to color buffer
    // ────────────────────────────────────────────────
    float3 sunBaseRgb = sunTex.rgb * sunBaseMultiplier * sunTex.a;
    float3 moonBaseRgb = moonTex.rgb * moonBaseMultiplier * moonTex.a;

    // Composite base sun/moon over sky (standard over operator)
    float3 sceneColor = baseColor;

    // No mask → texture alpha alone controls blending shape/edges
    sceneColor = sceneColor * (1.0 - sunTex.a) + sunBaseRgb * sunTex.a;
    sceneColor = sceneColor * (1.0 - moonTex.a) + moonBaseRgb * moonTex.a;

    // ────────────────────────────────────────────────
    // Glow (multiplied) versions – go to emissive only
    // ────────────────────────────────────────────────
    float3 sunGlowRgb = sunTex.rgb * sunGlowMultiplier * sunTex.a;
    float3 moonGlowRgb = moonTex.rgb * moonGlowMultiplier * moonTex.a;

    float3 emissive = sunGlowRgb + moonGlowRgb;

    // ────────────────────────────────────────────────
    // Output
    // ────────────────────────────────────────────────
    PixelOutput output;
    output.Color = float4(sceneColor, 1.0);
    output.Emissive = float4(emissive, 1.0);

    return output;
}

technique Skybox
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}

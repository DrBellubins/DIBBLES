// Simple atmospheric scattering + sun and moon sprite
float4x4 World;
float4x4 View;
float4x4 Projection;

float3 SkyZenithColor;
float3 SkyHorizonColor;

texture SunTexture;
texture MoonTexture;

float3 SunDirection;
float3 MoonDirection;

float TimeOfDay; // 0..24

sampler2D SunSampler = sampler_state
{
    Texture = <SunTexture>;
    Filter = POINT;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D MoonSampler = sampler_state
{
    Texture = <MoonTexture>;
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
    // Parameterize daytime factor [0=night, 1=day]
    /*float t = saturate(sin((TimeOfDay - 6) * 3.14159/12)); // peaks at noon

    // Altitude for horizon fade
    float horizon = saturate(dir.y * 0.5 + 0.5);

    // Dawn/Dusk: crossfade around 6-7, 17-19
    float dawnT = smoothstep(5, 7, TimeOfDay);
    float duskT = 1-smoothstep(17, 19, TimeOfDay);
    float dawnDusk = saturate(max(dawnT, duskT));*/

    // v is normalized view direction (from fragment position)
    float t = saturate(dir.y * 0.5 + 0.5); // t = 0: horizon, t = 1: zenith (up)
    float3 skyColor = lerp(SkyHorizonColor, SkyZenithColor, t);

    return skyColor;
}

float gaussian(float2 p, float2 center, float sigma)
{
    float2 d = p - center;
    return exp(-dot(d,d) / (2*sigma*sigma));
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

    const float sunMoonAngularRadius = 0.04;

    float3 sunCenter  = -SunDirection;
    float3 moonCenter = -MoonDirection;

    float2 sunUV  = computeLocalUV(viewDir, sunCenter,  sunMoonAngularRadius);
    float2 moonUV = computeLocalUV(viewDir, moonCenter, sunMoonAngularRadius);

    // Keep this order — sun first — to match typical working pattern
    float4 sunTex  = tex2D(SunSampler,  sunUV);
    float4 moonTex = tex2D(MoonSampler, moonUV);

    float sunBaseMultiplier  = 1.0;
    float moonBaseMultiplier = 1.0;
    float sunGlowMultiplier  = 1.5;
    float moonGlowMultiplier = 1.0;

    float3 sunBaseRgb  = sunTex.rgb  * sunBaseMultiplier  * sunTex.a;
    float3 moonBaseRgb = moonTex.rgb * moonBaseMultiplier * moonTex.a;

    float3 sceneColor = baseColor;
    sceneColor = sceneColor * (1.0 - sunTex.a)  + sunBaseRgb  * sunTex.a;
    sceneColor = sceneColor * (1.0 - moonTex.a) + moonBaseRgb * moonTex.a;

    float3 sunGlowRgb  = sunTex.rgb  * sunGlowMultiplier  * sunTex.a;
    float3 moonGlowRgb = moonTex.rgb * moonGlowMultiplier * moonTex.a;

    float3 emissive = sunGlowRgb + moonGlowRgb;

    PixelOutput output;
    //output.Color = moonTex;
    output.Color    = float4(sceneColor, 1.0);
    output.Emissive = float4(0.0, 0.0, 0.0, 1.0);
    //output.Emissive = float4(emissive,   1.0);

    return output;
}

technique Skybox
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}

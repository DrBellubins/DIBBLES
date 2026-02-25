// Simple atmospheric scattering + sun and moon sprite
float4x4 View;
float4x4 Projection;

float3 SkyZenithColor;
float3 SkyHorizonColor;

texture SunTexture;
texture MoonTexture;

float3 SunDirection;
float3 MoonDirection;
float TimeOfDay; // 0..24

sampler2D SunSampler = sampler_state { Texture = <SunTexture>; };
sampler2D MoonSampler = sampler_state { Texture = <MoonTexture>; };

struct VSInput
{
    float3 Position : POSITION0;
};
struct PSInput
{
    float4 Position : SV_POSITION;
    float3 World : TEXCOORD0;
};

PSInput VS(VSInput input)
{
    PSInput output;
    float4 world = float4(input.Position, 1.0);
    output.Position = mul(mul(world, View), Projection);
    output.World = input.Position;
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

float4 PS(PSInput input) : SV_Target
{
    float3 viewDir = normalize(input.World);

    float3 baseColor = computeSky(viewDir);

    // Sun
    float sunDot = saturate(dot(viewDir, -SunDirection));
    float2 sunUV = 0.5 + viewDir.xz * 0.5;

    float sunSize = 0.04;
    float sunBrightness = pow(sunDot, 250);
    float4 sunTex = tex2D(SunSampler, sunUV);
    float sunMask = gaussian(viewDir, -SunDirection, sunSize);
    float3 sunColor = sunTex.rgb * sunTex.a * sunBrightness * sunMask * 1.4;

    // Moon (opposite sun)
    float moonDot = saturate(dot(viewDir, -MoonDirection));
    float2 moonUV = 0.5 + viewDir.xz * 0.5;
    float moonSize = 0.03;
    float moonBrightness = pow(moonDot, 90);
    float4 moonTex = tex2D(MoonSampler, moonUV);
    float moonMask = gaussian(viewDir, -MoonDirection, moonSize);
    float3 moonColor = moonTex.rgb * moonTex.a * moonBrightness * moonMask * 1.0;

    float3 color = baseColor + sunColor + moonColor;

    return float4(color, 1.0);
}

technique Skybox
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}

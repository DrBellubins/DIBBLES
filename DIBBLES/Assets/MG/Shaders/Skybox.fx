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

float4 PS(PSInput input) : SV_Target
{
    float3 viewDir = normalize(input.World);

    float3 baseColor = computeSky(viewDir);

    float horizonFade = smoothstep(-0.07, 0.07, viewDir.y); // Fade between slightly below and above horizon

    float sunMoonSize = 0.04;
    float2 domeUV = 0.5 + viewDir.xz * 0.5;

    // Sun
    float4 sunTex = tex2D(SunSampler, domeUV);

    float sunDot = dot(viewDir, -SunDirection);
    float sunMask = gaussian(viewDir, -SunDirection, sunMoonSize);
    float sunBrightness = pow(max(sunDot, 0), 250); // Only brighten when above horizon:
    float sunAlpha = sunMask * horizonFade;

    // Final color uses sunAlpha for fade-out at horizon and below
    float3 sunColor = sunTex.rgb * sunTex.a * sunAlpha * 1.4;

    // Moon: same logic, maybe softer (smaller pow)
    float4 moonTex = tex2D(MoonSampler, domeUV);

    float moonDot = dot(viewDir, -MoonDirection);
    float moonMask = gaussian(viewDir, -MoonDirection, sunMoonSize);
    float moonBrightness = pow(max(moonDot, 0), 90);
    float moonAlpha = moonMask * horizonFade;

    float3 moonColor = moonTex.rgb * moonTex.a * moonAlpha * 1.0;

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

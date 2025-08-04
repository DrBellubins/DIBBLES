#version 330

in vec2 fragTexCoord;
out vec4 finalColor;

uniform sampler2D texture0; // Input texture (render target from scene)
uniform float Strength;
uniform float Fade;
uniform float Contrast;
uniform float Linearization;
uniform float Bleach;
uniform float Saturation;
uniform float RedCurve;
uniform float GreenCurve;
uniform float BlueCurve;
uniform float BaseCurve;
uniform float BaseGamma;
uniform float EffectGamma;
uniform float EffectGammaR;
uniform float EffectGammaG;
uniform float EffectGammaB;

const vec3 LumCoeff = vec3(0.212656, 0.715158, 0.072186);

void main()
{
    // Sample input texture
    vec3 B = texture(texture0, fragTexCoord).rgb;
    vec3 G = B;
    vec3 H = vec3(0.01);

    // Saturate and apply linearization
    B = clamp(B, 0.0, 1.0);
    B = pow(B, vec3(Linearization));

    // Apply contrast
    B = mix(H, B, Contrast);

    // Compute luminance
    float A = dot(B.rgb, LumCoeff);
    vec3 D = vec3(A);

    // Apply base gamma
    B = pow(abs(B), vec3(1.0 / BaseGamma));

    // Color curve parameters
    float a = RedCurve;
    float b = GreenCurve;
    float c = BlueCurve;
    float d = BaseCurve;

    // Sigmoid curve adjustments
    float y = 1.0 / (1.0 + exp(a / 2.0));
    float z = 1.0 / (1.0 + exp(b / 2.0));
    float w = 1.0 / (1.0 + exp(c / 2.0));
    float v = 1.0 / (1.0 + exp(d / 2.0));

    vec3 C = B;
    D.r = (1.0 / (1.0 + exp(-a * (D.r - 0.5))) - y) / (1.0 - 2.0 * y);
    D.g = (1.0 / (1.0 + exp(-b * (D.g - 0.5))) - z) / (1.0 - 2.0 * z);
    D.b = (1.0 / (1.0 + exp(-c * (D.b - 0.5))) - w) / (1.0 - 2.0 * w);

    // Apply effect gamma
    D = pow(abs(D), vec3(1.0 / EffectGamma));

    // Bleach effect
    vec3 Di = 1.0 - D;
    D = mix(D, Di, Bleach);

    // Individual channel gamma adjustments
    D.r = pow(abs(D.r), 1.0 / EffectGammaR);
    D.g = pow(abs(D.g), 1.0 / EffectGammaG);
    D.b = pow(abs(D.b), 1.0 / EffectGammaB);

    // Apply tone mapping
    if (D.r < 0.5)
        C.r = (2.0 * D.r - 1.0) * (B.r - B.r * B.r) + B.r;
    else
        C.r = (2.0 * D.r - 1.0) * (sqrt(B.r) - B.r) + B.r;

    if (D.g < 0.5)
        C.g = (2.0 * D.g - 1.0) * (B.g - B.g * B.g) + B.g;
    else
        C.g = (2.0 * D.g - 1.0) * (sqrt(B.g) - B.g) + B.g;

    if (D.b < 0.5)
        C.b = (2.0 * D.b - 1.0) * (B.b - B.b * B.b) + B.b;
    else
        C.b = (2.0 * D.b - 1.0) * (sqrt(B.b) - B.b) + B.b;

    // Apply strength
    vec3 F = mix(B, C, Strength);

    // Apply base curve
    F = (1.0 / (1.0 + exp(-d * (F - 0.5))) - v) / (1.0 - 2.0 * v);

    // Saturation and fade adjustments
    float r2R = 1.0 - Saturation;
    float g2R = 0.0 + Saturation;
    float b2R = 0.0 + Saturation;
    float r2G = 0.0 + Saturation;
    float g2G = (1.0 - Fade) - Saturation;
    float b2G = (0.0 + Fade) + Saturation;
    float r2B = 0.0 + Saturation;
    float g2B = (0.0 + Fade) + Saturation;
    float b2B = (1.0 - Fade) - Saturation;

    vec3 iF = F;
    F.r = (iF.r * r2R + iF.g * g2R + iF.b * b2R);
    F.g = (iF.r * r2G + iF.g * g2G + iF.b * b2G);
    F.b = (iF.r * r2B + iF.g * g2B + iF.b * b2B);

    // Final tone mapping based on luminance
    float N = dot(F.rgb, LumCoeff);
    vec3 Cn = F;
    if (N < 0.5)
        Cn = (2.0 * N - 1.0) * (F - F * F) + F;
    else
        Cn = (2.0 * N - 1.0) * (sqrt(F) - F) + F;

    // Final linearization and strength application
    Cn = pow(max(Cn, 0.0), vec3(1.0 / Linearization));
    vec3 Fn = mix(B, Cn, Strength);

    finalColor = vec4(Fn, 1.0);
}

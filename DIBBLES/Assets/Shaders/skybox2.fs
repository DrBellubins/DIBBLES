#version 330

#define WIND vec2(0.4, -0.8)
#define MIN_HEIGHT 2.0
#define MAX_HEIGHT 8.0

in vec3 fragPosition;
uniform sampler2D environmentMap;
uniform float Time; // Time uniform for animation
uniform vec3 cameraPosition;
out vec4 finalColor;

vec3 sundir = normalize(vec3(1.0, 0.75, 1.0));

// Bayer dither matrix (4x4) for dither effect
const float bayer4x4[16] = float[16](
    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
);

// Bayer dither matrix (2x2) for dither effect
const float bayer2x2[4] = float[4](
    0.0/4.0, 2.0/4.0,
    3.0/4.0, 1.0/4.0
);


float getDither(vec3 pos, float density)
{
    // Use 3D position for dithering
    int x = int(mod(pos.x * 32.0, 4.0));
    int y = int(mod(pos.y * 32.0, 4.0));
    int z = int(mod(pos.z * 32.0, 4.0));
    int index = (x + y * 4 + z) % 16; // Combine 3D coordinates into index
    float ditherValue = bayer4x4[index];
    return density > ditherValue ? density : 0.0;
}

float noise(in vec3 x)
{
    vec3 f = fract(x);
    vec3 p = floor(x);
    f = f * f * (3.0 - 2.0 * f);

    p.xz += WIND * Time;
    vec2 uv = (p.xz + vec2(37.0, 17.0) * p.y) + f.xz;

    uv *= 0.25;

    vec2 rg = texture(environmentMap, (uv + 0.5) / 256.0).yx;
    return mix(rg.x, rg.y, f.y);
}

float fractal_noise(vec3 p)
{
    float f = 0.0;
    p = p * 3.0;

    f += 0.50000 * noise(p);
    p *= 2.0;

    f += 0.25000 * noise(p);
    p *= 2.0;

    f += 0.12500 * noise(p);
    p *= 2.0;

    f += 0.06250 * noise(p);
    p *= 2.0;

    f += 0.03125 * noise(p);

    return f;
}

float density(vec3 pos)
{
    float den = 3.0 * fractal_noise(pos * vec3(0.3, 0.2, 0.3)) - 2.0 + (pos.y - MIN_HEIGHT);
    float edge = 1.0 - smoothstep(MIN_HEIGHT, MAX_HEIGHT, pos.y);
    edge *= edge;
    den *= edge;
    den = clamp(den, 0.0, 1.0);
    return den;
}

float compute_light(vec3 pos, vec3 sundir)
{
    float opticalDepth = 0.0;
    vec3 lightPos = pos;
    float lightStep = 0.1;
    for (int i = 0; i < 6; i++) {
        lightPos += sundir * lightStep;
        opticalDepth += density(lightPos) * lightStep;
    }
    return exp(-opticalDepth * 0.2); // Attenuate light based on optical depth
}

vec3 raymarching(vec3 ro, vec3 rd, float t, vec3 backCol)
{
    vec4 sum = vec4(0.0);
    vec3 pos = ro + rd * t;

    int maxSteps = 24; // Increased from 10 to 24 for better sampling
    float baseStep = 0.07; // Smaller base step size

    for (int i = 0; i < maxSteps; i++)
    {
        if (sum.a > 0.99 ||
            pos.y < (MIN_HEIGHT - 1.0) ||
            pos.y > (MAX_HEIGHT + 1.0)) break;

        float den = density(pos);

        if (den > 0.01)
        {
            // Apply dither effect to density
            //den = getDither(pos, den);
            //den = mix(den, getDither(pos, den), 0.8);

            float light = compute_light(pos, sundir);
            vec3 lin = vec3(0.65, 0.7, 0.75) * 1.5 + vec3(1.0, 0.6, 0.3) * light;
            vec4 col = vec4(mix(vec3(1.0, 0.95, 0.8) * 1.1, vec3(0.35, 0.4, 0.45), den), den);
            col.rgb *= lin;
            col.a *= 0.5;
            col.rgb *= col.a;
            sum = sum + col * (1.0 - sum.a);
        }

        // Adjust step size based on vertical component to reduce artifacts
        float stepSize = baseStep / max(0.1, abs(rd.y)); // Smaller steps for vertical rays
        t += max(stepSize, 0.02 * t);
        pos = ro + rd * t;
    }

    sum = clamp(sum, 0.0, 1.0);

    float h = rd.y;
    sum.rgb = mix(sum.rgb, backCol, exp(-20.0 * h * h));

    return mix(backCol, sum.xyz, sum.a);
}

float planeIntersect(vec3 ro, vec3 rd, float plane)
{
    float h = plane - ro.y;
    return h / rd.y;
}

void main()
{
    // Normalize fragPosition to use as direction vector
    vec3 dir = normalize(fragPosition);

    // Map to 2D UV coordinates for a panoramic sky
    vec2 uv = vec2(
        0.5 + (atan(dir.x, dir.z) / (2.0 * 3.14159)) * 2.0, // Horizontal wrap, match 2048/1024 aspect
        0.5 - (asin(dir.y) / 3.14159)   // Vertical angle, clamped
    );

    // Ensure seamless wrapping
    uv = fract(uv);

    // Compute ray origin and direction
    vec3 ro = vec3(cameraPosition.x * 0.1, cameraPosition.y * 0.1, cameraPosition.z * 0.1);
    vec3 rd = dir; // Ray direction from normalized fragment position

    // Background color (sky gradient)
    float sun = clamp(dot(sundir, rd), 0.0, 1.0);
    vec3 col = mix(vec3(0.78, 0.78, 0.7), vec3(0.3, 0.4, 0.5), uv.y * 0.5 + 0.5);
    col += 0.5 * vec3(1.0, 0.5, 0.1) * pow(sun, 4.0);

    // Raymarch clouds if ray intersects the cloud plane
    float dist = planeIntersect(ro, rd, MIN_HEIGHT);

    if (dist > 0.0)
    {
        col = raymarching(ro, rd, dist, col);
    }

    finalColor = vec4(col, 1.0);
}

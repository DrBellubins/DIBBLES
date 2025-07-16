// CRT Scanline Shader (Raylib version, cleaned up)
// Adapted from ShaderToy by Timothy Lottes, public domain

// Raylib provides:
uniform sampler2D texture0;      // The source texture
uniform vec2 resolution;         // Output resolution
varying vec2 fragTexCoord;       // Texcoord for current pixel

// ---------- Parameters ----------
const float hardScan  = -8.0;   // Scanline hardness
const float hardPix   = -3.0;   // Pixel hardness
const vec2  warp      = vec2(1.0/32.0, 1.0/24.0); // Display warp
const float maskDark  = 0.5;    // Mask minimum
const float maskLight = 1.5;    // Mask maximum

// Emulated input resolution (change to match your virtual screen)
const vec2 emuRes = vec2(320.0, 240.0);

// ---------- Color Space Helpers ----------
// (For sRGB textures, these can be omitted in Raylib)
float toLinear1(float c)
{
    return (c <= 0.04045) ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
}

vec3 toLinear(vec3 c)
{
    return vec3(toLinear1(c.r), toLinear1(c.g), toLinear1(c.b));
}

float toSrgb1(float c)
{
    return (c < 0.0031308) ? c * 12.92 : 1.055*pow(c,0.41666) - 0.055;
}

vec3 toSrgb(vec3 c)
{
    return vec3(toSrgb1(c.r), toSrgb1(c.g), toSrgb1(c.b));
}

// ---------- CRT Effect Core ----------

// Fetch texel in emulated input space, with offset
vec3 fetch(vec2 pos, vec2 offset)
{
    pos = floor(pos * emuRes + offset) / emuRes;

    // Clamp to texture edge
    if (any(lessThan(pos, vec2(0.0))) || any(greaterThan(pos, vec2(1.0))))
        return vec3(0.0);

    return texture2D(texture0, pos).rgb;
}

// Distance in emulated pixels to nearest texel
vec2 dist(vec2 pos)
{
    pos = pos * emuRes;
    return -((pos - floor(pos)) - vec2(0.5));
}

// Gaussian kernel
float gauss(float pos, float scale)
{
    return exp2(scale * pos * pos);
}

// 3-tap horizontal filter
vec3 horz3(vec2 pos, float offset)
{
    vec3 b = fetch(pos, vec2(-1.0, offset));
    vec3 c = fetch(pos, vec2( 0.0, offset));
    vec3 d = fetch(pos, vec2( 1.0, offset));

    float dst = dist(pos).x;
    float scale = hardPix;
    float wb = gauss(dst - 1.0, scale);
    float wc = gauss(dst,       scale);
    float wd = gauss(dst + 1.0, scale);

    return (b * wb + c * wc + d * wd) / (wb + wc + wd);
}

// 5-tap horizontal filter
vec3 horz5(vec2 pos, float offset)
{
    vec3 a = fetch(pos, vec2(-2.0, offset));
    vec3 b = fetch(pos, vec2(-1.0, offset));
    vec3 c = fetch(pos, vec2( 0.0, offset));
    vec3 d = fetch(pos, vec2( 1.0, offset));
    vec3 e = fetch(pos, vec2( 2.0, offset));

    float dst = dist(pos).x;
    float scale = hardPix;
    float wa = gauss(dst - 2.0, scale);
    float wb = gauss(dst - 1.0, scale);
    float wc = gauss(dst,       scale);
    float wd = gauss(dst + 1.0, scale);
    float we = gauss(dst + 2.0, scale);

    return (a*wa + b*wb + c*wc + d*wd + e*we) / (wa + wb + wc + wd + we);
}

// Scanline weight
float scan(vec2 pos, float offset)
{
    float dst = dist(pos).y;
    return gauss(dst + offset, hardScan);
}

// Allow nearest three scanlines to affect pixel
vec3 tri(vec2 pos)
{
    vec3 a = horz3(pos, -1.0);
    vec3 b = horz5(pos,  0.0);
    vec3 c = horz3(pos,  1.0);
    float wa = scan(pos, -1.0);
    float wb = scan(pos,  0.0);
    float wc = scan(pos,  1.0);
    return a * wa + b * wb + c * wc;
}

// Distortion of scanlines, and end of screen alpha
vec2 warpCoord(vec2 pos)
{
    pos = pos * 2.0 - 1.0;
    pos *= vec2(1.0 + (pos.y * pos.y) * warp.x,
                1.0 + (pos.x * pos.x) * warp.y);

    return pos * 0.5 + 0.5;
}

// Shadow mask
vec3 mask(vec2 pos)
{
    pos.x += pos.y * 3.0;
    vec3 m = vec3(maskDark);
    float px = fract(pos.x / 6.0);

    if (px < 0.333)      m.r = maskLight;
    else if (px < 0.666) m.g = maskLight;
    else                 m.b = maskLight;

    return m;
}

// ---------- Main Fragment Shader ----------
void main()
{
    // Convert output pixel to normalized position
    vec2 uv = fragTexCoord;

    // Apply barrel distortion warp
    vec2 crtUV = warpCoord(uv);

    // Get CRT color
    vec3 color = tri(crtUV);

    // Apply shadow mask
    color *= mask(gl_FragCoord.xy);

    // Output color (no need to convert to sRGB, Raylib expects linear RGB)
    gl_FragColor = vec4(color, 1.0);
}
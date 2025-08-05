#version 330

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec2 emuRes;
uniform float time;
uniform int pass;

out vec4 finalColor;

// 4x4 Bayer matrix for dithering
const float bayerMatrix[16] = float[16]
(
    0.0/ 16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
);

// Simple film grain effect
float random(vec2 st)
{
    return fract(sin(dot(st, vec2(12.9898, 78.233))) * 43758.5453123);
}

void main()
{
    vec2 uv = fragTexCoord;
    vec4 color = texture2D(texture0, uv) * fragColor;

    if (pass == 0)
    {
        // Film grain pass
        float grain = random(uv * time) * 0.0;
        finalColor = vec4(color.rgb + grain, color.a);
    }
    else if (pass == 1)
    {
        // Bayer dithering pass with color preservation
        vec2 pixel = floor(uv * emuRes);
        int index = int(mod(pixel.x, 4.0)) + int(mod(pixel.y, 4.0)) * 4;
        float threshold = bayerMatrix[index];

        // Apply dithering to each color channel separately
        vec3 dithered = vec3(
            color.r < threshold ? 0.0 : 1.0,
            color.g < threshold ? 0.0 : 1.0,
            color.b < threshold ? 0.0 : 1.0
        );

        color.rgb = pow(color.rgb, vec3(0.25));
        //color.rgb *= 1.5;

        finalColor = vec4(dithered * color.rgb, color.a);
    }
    else
    {
        // CRT effect pass
        vec2 curvedUV = uv;

        // Apply CRT curvature
        vec2 curve = vec2(0.02, 0.02);
        curvedUV = curvedUV * 2.0 - 1.0;
        vec2 offset = abs(curvedUV) * curve;
        curvedUV = curvedUV * (1.0 + offset);
        curvedUV = curvedUV * 0.5 + 0.5;

        // Vignette
        float vignette = smoothstep(0.0, 0.7, 1.0 - length(curvedUV - 0.5));

        // Scanlines
        float scanline = sin(curvedUV.y * emuRes.y * 1.5) * 0.1 + 0.9;

        color = texture2D(texture0, curvedUV);

        if (curvedUV.x < 0.0 || curvedUV.x > 1.0 || curvedUV.y < 0.0 || curvedUV.y > 1.0)
        {
            color = vec4(0.0);
        }

        finalColor = vec4(color.rgb * vignette * scanline, color.a);
    }
}

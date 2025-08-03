#version 330

in vec3 fragPosition;
uniform sampler2D environmentMap;
out vec4 finalColor;

void main()
{
    // Normalize fragPosition to use as direction vector
    vec3 dir = normalize(fragPosition);

    // Map to 2D UV coordinates for a panoramic sky
    vec2 uv = vec2(
        0.5 + (atan(dir.x, dir.z) / (2.0 * 3.14159)) * 2.0, // Horizontal wrap, match 2048/1024 aspect
        0.5 - (asin(dir.y / 3.14159))   // Vertical angle, clamped
    );

    // Ensure seamless wrapping
    uv = fract(uv); // Reset to 0-1 range
    //uv += vec2(0.001, 0.001); // Small offset
    //uv = clamp(uv, 0.01, 0.99); // Avoid edges

    vec3 color = texture(environmentMap, uv).rgb;

    finalColor = vec4(color, 1.0);
}

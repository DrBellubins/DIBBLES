// fog.fs
#version 330

// Input vertex attributes (from vertex shader)
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragPosition;
in vec3 fragNormal;

// Input uniform values
uniform sampler2D texture0;
uniform vec4 colDiffuse;

// Custom uniforms
uniform vec3 fogColor;
uniform float fogStart;
uniform float fogEnd;

// Output fragment color
out vec4 finalColor;

void main()
{
    vec4 color = texture(texture0, fragTexCoord);
    vec3 normal = normalize(fragNormal);
    float depth = length(viewPos - fragPosition)

    float fogFactor = clamp((depth - fogStart) / (fogEnd - fogStart), 0.0, 1.0);
    finalColor = mix(color, vec4(fogColor, 1.0), fogFactor);
}

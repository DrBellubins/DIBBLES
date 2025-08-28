#version 330

in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragPosition; // World position from vertex shader

out vec4 finalColor;

uniform sampler2D texture0;

// Fog uniforms
uniform vec3 cameraPos;
uniform float fogNear;
uniform float fogFar;
uniform vec4 fogColor;

void main()
{
    vec4 texColor = texture(texture0, fragTexCoord);
    vec4 blockColor = texColor * fragColor;

    // Fog calculation (engine-style, matches post-process)
    float dist = length(fragPosition - cameraPos);
    float fogFactor = smoothstep(fogNear, fogFar, dist);

    // Mix blockColor and fogColor
    finalColor = mix(blockColor, fogColor, fogFactor);
    finalColor.a = blockColor.a; // preserve alpha
}

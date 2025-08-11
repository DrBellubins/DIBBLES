#version 330

in vec2 fragTexCoord;

uniform sampler2D sceneTex;   // Rendered scene color
uniform sampler2D depthTex;   // Depth buffer texture

uniform float fogNear;
uniform float fogFar;
uniform vec4 fogColor;

out vec4 finalColor;

void main()
{
    float depth = texture(depthTex, fragTexCoord).r;

    // Reconstruct linear depth (depends on depth buffer encoding)
    float linearDepth = depth; // For default, may need custom logic

    float fogFactor = smoothstep(fogNear, fogFar, linearDepth);

    vec4 scene = texture(sceneTex, fragTexCoord);

    finalColor = vec4(depth, depth, depth, 1.0);
    //finalColor = mix(scene, fogColor, fogFactor);
}

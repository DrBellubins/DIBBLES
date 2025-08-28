#version 330

in vec2 fragTexCoord;

uniform sampler2D sceneTex;   // Rendered scene color
uniform sampler2D depthTex;   // Depth buffer texture

uniform float zNear;
uniform float zFar;

uniform float fogNear;
uniform float fogFar;

uniform vec4 fogColor;

uniform mat4 invProj;
uniform mat4 invView;
uniform vec3 cameraPos;

out vec4 finalColor;

void main()
{
    float depth = texture(depthTex, fragTexCoord).r;

    if (depth >= 1)
    {
        finalColor = texture(sceneTex, fragTexCoord);
        return;
    }

    // Reconstruct NDC
    vec4 ndcPos = vec4(fragTexCoord * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

    // Get view-space position
    vec4 viewPos = invProj * ndcPos;
    viewPos /= viewPos.w;

    // Get world-space position
    vec4 worldPos = invView * viewPos;

    // Calculate world distance
    float dist = length(worldPos.xyz - cameraPos);

    float fogFactor = smoothstep(fogNear, fogFar, dist);

    vec4 scene = texture(sceneTex, fragTexCoord);
    finalColor = mix(scene, fogColor, fogFactor);
}

#version 330

in vec2 fragTexCoord;

uniform sampler2D sceneTex;   // Rendered scene color
uniform sampler2D depthTex;   // Depth buffer texture

uniform float zNear;
uniform float zFar;

uniform float fogNear;
uniform float fogFar;

uniform vec4 fogColor;

out vec4 finalColor;

void main()
{
    float depth = texture(depthTex, fragTexCoord).r;

    // If background/sky, don't apply fog
    if (depth >= 1)
    {
        finalColor = texture(sceneTex, fragTexCoord);
        return;
    }

    float ndcDepth = depth * 2.0 - 1.0;
    float linearDepth = (2.0 * zNear * zFar) / (zFar + zNear - ndcDepth * (zFar - zNear));

    float fogFactor = smoothstep(fogNear, fogFar, linearDepth);

    vec4 scene = texture(sceneTex, fragTexCoord);

    //finalColor = vec4(linearDepth, linearDepth, linearDepth, 1.0);
    finalColor = mix(scene, fogColor, fogFactor);
}

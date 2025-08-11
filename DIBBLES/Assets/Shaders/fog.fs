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
    float linearDepth = texture(depthTex, fragTexCoord).r;
    float depth = zNear + (zFar - zNear) * linearDepth;

    float fogFactor = smoothstep(fogNear, fogFar, depth);

    vec4 scene = texture(sceneTex, fragTexCoord);

    //finalColor = vec4(linearDepth, linearDepth, linearDepth, 1.0);
    finalColor = mix(scene, fogColor, fogFactor);
}

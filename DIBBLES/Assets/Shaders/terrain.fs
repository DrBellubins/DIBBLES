#version 330

in vec3 fragWorldPos;
in vec3 fragNormal;
in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec3 cameraPos;
uniform float fogNear;
uniform float fogFar;
uniform vec4 fogColor;

out vec4 finalColor;

void main()
{
    vec4 albedo = texture(texture0, fragTexCoord) * fragColor;

    float dist = length(fragWorldPos - cameraPos);
    float fogFactor = smoothstep(fogNear, fogFar, dist);

    finalColor = mix(albedo, fogColor, fogFactor);
    finalColor.a = albedo.a;

    if (finalColor.a < 0.01)
        discard;
}

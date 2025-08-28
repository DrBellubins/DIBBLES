#version 330

in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 fragWorldPos;
out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragNormal;

void main()
{
    fragWorldPos = (model * vec4(vertexPosition, 1.0)).xyz;
    fragTexCoord = vertexTexCoord;
    fragColor    = vertexColor;
    fragNormal   = vertexNormal;
    gl_Position = projection * view * model * vec4(vertexPosition, 1.0);
}

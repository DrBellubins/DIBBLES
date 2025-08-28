#version 330

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec3 vertexNormal;
layout(location = 2) in vec2 vertexTexCoord;
layout(location = 3) in vec4 vertexColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 fragWorldPos;
out vec3 fragNormal;
out vec2 fragTexCoord;
out vec4 fragColor;

void main()
{
    fragWorldPos = (model * vec4(vertexPosition, 1.0)).xyz;
    fragNormal   = vertexNormal;
    fragTexCoord = vertexTexCoord;
    fragColor    = vertexColor;

    gl_Position = projection * view * model * vec4(vertexPosition, 1.0);
}

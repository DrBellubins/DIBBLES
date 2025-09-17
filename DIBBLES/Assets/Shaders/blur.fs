#version 330

// Input vertex attributes (from vertex shader)
in vec2 fragTexCoord;
in vec4 fragColor;

// Input uniform values
uniform sampler2D texture0;
uniform vec4 colDiffuse;

// Output fragment color
out vec4 finalColor;

// NOTE: Add here your custom variables
uniform sampler2D maskTexture;
uniform int pass;
uniform vec2 texelSize;
uniform float radius;

vec4 Box4(vec4 p0, vec4 p1, vec4 p2, vec4 p3)
{
	return (p0 + p1 + p2 + p3) * 0.25f;
}

vec4 DownsamplePS(vec2 texCoord)
{
	vec2 offset = vec2(texelSize.x, texelSize.y);

	vec4 c0 = texture(texture0, texCoord + vec2(-2, -2) * offset);
	vec4 c1 = texture(texture0, texCoord + vec2(0,-2) * offset);
	vec4 c2 = texture(texture0, texCoord + vec2(2, -2) * offset);
	vec4 c3 = texture(texture0, texCoord + vec2(-1, -1) * offset);
	vec4 c4 = texture(texture0, texCoord + vec2(1, -1) * offset);
	vec4 c5 = texture(texture0, texCoord + vec2(-2, 0) * offset);
	vec4 c6 = texture(texture0, texCoord);
	vec4 c7 = texture(texture0, texCoord + vec2(2, 0) * offset);
	vec4 c8 = texture(texture0, texCoord + vec2(-1, 1) * offset);
	vec4 c9 = texture(texture0, texCoord + vec2(1, 1) * offset);
	vec4 c10 = texture(texture0, texCoord + vec2(-2, 2) * offset);
	vec4 c11 = texture(texture0, texCoord + vec2(0, 2) * offset);
	vec4 c12 = texture(texture0, texCoord + vec2(2, 2) * offset);

	return Box4(c0, c1, c5, c6) * 0.125f +
	Box4(c1, c2, c6, c7) * 0.125f +
	Box4(c5, c6, c10, c11) * 0.125f +
	Box4(c6, c7, c11, c12) * 0.125f +
	Box4(c3, c4, c8, c9) * 0.5f;
}

vec4 UpsamplePS(vec2 texCoord)
{
	vec2 offset = vec2(texelSize.x, texelSize.y) * radius;

	vec4 c0 = texture(texture0, texCoord + vec2(-1, -1) * offset);
	vec4 c1 = texture(texture0, texCoord + vec2(0, -1) * offset);
	vec4 c2 = texture(texture0, texCoord + vec2(1, -1) * offset);
	vec4 c3 = texture(texture0, texCoord + vec2(-1, 0) * offset);
	vec4 c4 = texture(texture0, texCoord);
	vec4 c5 = texture(texture0, texCoord + vec2(1, 0) * offset);
	vec4 c6 = texture(texture0, texCoord + vec2(-1,1) * offset);
	vec4 c7 = texture(texture0, texCoord + vec2(0, 1) * offset);
	vec4 c8 = texture(texture0, texCoord + vec2(1, 1) * offset);

	//Tentfilter
	return 0.0625f * (c0 + 2 * c1 + c2 + 2 * c3 + 4 * c4 + 2 * c5 + c6 + 2 * c7 + c8);
}

void main()
{
	vec4 outColor = vec4(0, 0, 0, 0);

	if (pass == 0)
	{
		vec4 blur = DownsamplePS(fragTexCoord);
		//vec4 blur = texture(texture0, fragTexCoord);
		outColor = vec4(blur.xyz, 1.0f);
	}
	else // mask pass
	{
		vec4 blurUpscaled = UpsamplePS(fragTexCoord);
		//vec4 blurUpscaled = texture(texture0, fragTexCoord);
		vec4 uiMaskColor = texture(maskTexture, fragTexCoord);

		if (ceil(uiMaskColor.a) == 1)
			outColor = blurUpscaled;
		else
			outColor = vec4(0, 0, 0, 0);
	}

    finalColor = outColor;
}

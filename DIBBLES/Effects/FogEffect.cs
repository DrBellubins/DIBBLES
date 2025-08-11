using System.Numerics;
using Raylib_cs;

namespace DIBBLES.Effects;

public class FogEffect
{
    Vector3 fogColor = new  Vector3(0f, 0.5f, 0.5f);
    private float fogStart = 500f;
    private float fogEnd = 1000f;
    
    private Shader fogShader;
    private RenderTexture2D fogRT;

    public void Start()
    {
        fogShader = Resource.LoadShader("terrain.vs", "fog.fs");
        
        Raylib.SetShaderValue(fogShader, Raylib.GetShaderLocation(fogShader, "fogColor"), fogColor, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(fogShader, Raylib.GetShaderLocation(fogShader, "fogStart"), fogStart, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(fogShader, Raylib.GetShaderLocation(fogShader, "fogEnd"), fogEnd, ShaderUniformDataType.Float);
        
        fogRT = Raylib.LoadRenderTexture(Engine.ScreenWidth, Engine.ScreenHeight);
        Raylib.SetTextureFilter(fogRT.Texture, TextureFilter.Bilinear);
    }

    public void DrawStart(Vector3 position)
    {
        Raylib.SetShaderValue(fogShader, Raylib.GetShaderLocation(fogShader, "viewPos"), position, ShaderUniformDataType.Vec3);
        
        Raylib.BeginTextureMode(fogRT);
        Raylib.BeginShaderMode(fogShader);
    }
    
    public void DrawEnd()
    {
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();
    }

    public void DrawBuffer()
    {
        Raylib.DrawTexture(fogRT.Texture, 0, 0, Color.White);
    }
}
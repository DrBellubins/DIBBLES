using System.Numerics;
using Raylib_cs;

namespace DIBBLES.Effects;

public class FogEffect
{
    private Shader fogShader;
    private RenderTexture2D target;
    private int sceneTexLoc;
    private int depthTexLoc;
    private int fogNearLoc, fogFarLoc, fogColorLoc;
    
    private uint depthTexId;
    
    private float fogNear = 5.0f;
    private float fogFar = 40.0f;
    private Vector4 fogColor = new Vector4(0.5f, 0.6f, 0.7f, 1.0f);

    public void Start()
    {
        depthTexId = Rlgl.LoadTextureDepth(Engine.ScreenWidth, Engine.ScreenHeight, true);
        
        fogShader = Resource.LoadShader(null, "fog.fs");
        target = Raylib.LoadRenderTexture(Engine.ScreenWidth, Engine.ScreenHeight);

        sceneTexLoc = Raylib.GetShaderLocation(fogShader, "sceneTex");
        depthTexLoc = Raylib.GetShaderLocation(fogShader, "depthTex");
        fogNearLoc = Raylib.GetShaderLocation(fogShader, "fogNear");
        fogFarLoc = Raylib.GetShaderLocation(fogShader, "fogFar");
        fogColorLoc = Raylib.GetShaderLocation(fogShader, "fogColor");

        Raylib.SetShaderValue(fogShader, fogNearLoc, fogNear, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(fogShader, fogFarLoc, fogFar, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(fogShader, fogColorLoc, fogColor, ShaderUniformDataType.Vec4);
    }

    public void DrawStart()
    {
        Raylib.BeginTextureMode(target);
    }

    public void DrawEnd()
    {
        Raylib.EndTextureMode();

        Raylib.BeginShaderMode(fogShader);

        // Set scene color tex (color buffer)
        Raylib.SetShaderValueTexture(fogShader, sceneTexLoc, target.Texture);

        // Attach the depth texture (as a raw OpenGL texture id)
        Rlgl.ActiveTextureSlot(1);
        Rlgl.EnableTexture(depthTexId);
        Raylib.SetShaderValueTexture(fogShader, depthTexLoc, new Texture2D { Id = depthTexId, Width = Engine.ScreenWidth, Height = Engine.ScreenHeight, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 });

        // Draw fullscreen quad with shader
        Raylib.DrawTextureRec(target.Texture, new Rectangle(0, 0, Engine.ScreenWidth, -Engine.ScreenHeight), Vector2.Zero, Color.White);

        Rlgl.DisableTexture();
        Raylib.EndShaderMode();
    }

    public void Unload()
    {
        Raylib.UnloadShader(fogShader);
        Raylib.UnloadRenderTexture(target);
    }
}
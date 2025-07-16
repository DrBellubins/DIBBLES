using Raylib_cs;
using System.Numerics;

namespace DIBBLES.Effects;

public class CRTEffect
{
    private Shader crtShader;
    private RenderTexture2D target;
    private RenderTexture2D crtOutput;

    private Rectangle sourceRec;
    private Rectangle destRec;
    
    public void Start()
    {
        target = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);
        Raylib.SetTextureFilter(target.Texture, TextureFilter.Point);
        
        crtOutput = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);
        Raylib.SetTextureFilter(crtOutput.Texture, TextureFilter.Bilinear);
        
        crtShader = Raylib.LoadShader(null, "Assets/Shaders/CRT.glsl");
        
        Raylib.SetShaderValue(crtShader, Raylib.GetShaderLocation(crtShader, "emuRes"),
            new Vector2(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight), ShaderUniformDataType.Vec2);
        
        // Calculate the scale to fit while preserving aspect ratio
        float scaleX = (float)Engine.ScreenWidth / (float)Engine.VirtualScreenWidth;
        float scaleY = (float)Engine.ScreenHeight / (float)Engine.VirtualScreenHeight;
        float scale = MathF.Min(scaleX, scaleY);

        // Calculate destination size
        float destWidth = Engine.VirtualScreenWidth * scale;
        float destHeight = Engine.VirtualScreenHeight * scale;

        // Center the rectangle
        float destX = (Engine.ScreenWidth - destWidth) * 0.5f;
        float destY = (Engine.ScreenHeight - destHeight) * 0.5f;
        
        sourceRec = new Rectangle(0.0f, 0.0f, (float)target.Texture.Width, -(float)target.Texture.Height);
        destRec = new Rectangle(destX, destY, destWidth, destHeight);
    }

    public void DrawStart()
    {
        Raylib.BeginTextureMode(target);
    }

    public void DrawEnd()
    {
        Raylib.EndTextureMode();
        
        Raylib.BeginTextureMode(crtOutput);
        
        Raylib.BeginShaderMode(crtShader);
        Raylib.DrawTextureRec(target.Texture, sourceRec, Vector2.Zero, Color.White);
        Raylib.EndShaderMode();
        
        Raylib.EndTextureMode();
        
        Raylib.DrawTexturePro(crtOutput.Texture, sourceRec, destRec, Vector2.Zero, 0.0f, Color.White);
    }
}
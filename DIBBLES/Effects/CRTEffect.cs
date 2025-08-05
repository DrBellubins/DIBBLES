using Raylib_cs;
using System.Numerics;

namespace DIBBLES.Effects;

public class CRTEffect
{
    private Shader crtShader;
    private RenderTexture2D target;
    
    private RenderTexture2D filmicEffectRT;
    private RenderTexture2D filmGrainRT;
    private RenderTexture2D ditherRT;
    //private RenderTexture2D crtOutput;

    private Rectangle sourceRec;
    private Rectangle destRec;
    
    private FilmicEffect filmicEffect = new FilmicEffect();
    
    public void Start()
    {
        target = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);
        Raylib.SetTextureFilter(target.Texture, TextureFilter.Bilinear);
        
        filmicEffectRT = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);
        Raylib.SetTextureFilter(filmicEffectRT.Texture, TextureFilter.Bilinear);
        
        filmGrainRT = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);
        Raylib.SetTextureFilter(filmGrainRT.Texture, TextureFilter.Bilinear);
        
        ditherRT = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);
        Raylib.SetTextureFilter(ditherRT.Texture, TextureFilter.Bilinear);
        
        //crtOutput = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);
        //Raylib.SetTextureFilter(crtOutput.Texture, TextureFilter.Bilinear);
        
        crtShader = Resource.LoadShader(null, "CRT2.fs");
        
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
        
        filmicEffect.Start(ditherRT);
    }

    public void DrawStart(float time)
    {
        Raylib.SetShaderValue(crtShader, Raylib.GetShaderLocation(crtShader, "time"), time, ShaderUniformDataType.Float);
        
        Raylib.BeginTextureMode(target);
    }

    public void DrawEnd()
    {
        Raylib.EndTextureMode();

        // Film grain pass
        /*Raylib.BeginTextureMode(filmGrainRT);
        Raylib.SetShaderValue(crtShader, Raylib.GetShaderLocation(crtShader, "pass"), 0, ShaderUniformDataType.Int);

        Raylib.BeginShaderMode(crtShader);
        Raylib.DrawTextureRec(filmicEffectRT.Texture, sourceRec, Vector2.Zero, Color.White);
        Raylib.EndShaderMode();

        Raylib.EndTextureMode();*/
    
        // Bayer dithering pass
        Raylib.BeginTextureMode(ditherRT);
        Raylib.SetShaderValue(crtShader, Raylib.GetShaderLocation(crtShader, "pass"), 1, ShaderUniformDataType.Int);
    
        Raylib.BeginShaderMode(crtShader);
        Raylib.DrawTextureRec(target.Texture, sourceRec, Vector2.Zero, Color.White);
        Raylib.EndShaderMode();
    
        Raylib.EndTextureMode();
    
        // CRT pass
        /*Raylib.BeginTextureMode(crtOutput);
        Raylib.SetShaderValue(crtShader, Raylib.GetShaderLocation(crtShader, "pass"), 2, ShaderUniformDataType.Int);
    
        Raylib.BeginShaderMode(crtShader);
        Raylib.DrawTextureRec(ditherOutput.Texture, sourceRec, Vector2.Zero, Color.White);
        Raylib.EndShaderMode();
    
        Raylib.EndTextureMode();*/
        
        // FilmicEffect pass
        Raylib.BeginTextureMode(filmicEffectRT);

        Raylib.BeginShaderMode(filmicEffect.Shader);
        Raylib.DrawTextureRec(ditherRT.Texture, sourceRec, Vector2.Zero, Color.White);
        Raylib.EndShaderMode();

        Raylib.EndTextureMode();
        
        Raylib.DrawTexturePro(filmicEffectRT.Texture, sourceRec, destRec, Vector2.Zero, 0.0f, Color.White);
        //Raylib.DrawTexturePro(target.Texture, sourceRec, destRec, Vector2.Zero, 0.0f, Color.White);
    }
}
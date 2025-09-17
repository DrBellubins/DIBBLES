using System.Numerics;
using Raylib_cs;

namespace DIBBLES.Effects;

public class UIBlur
{
    private Vector2 texelSize;

    // Main buffers
    private RenderTexture2D backBuffer;
    private RenderTexture2D postBuffer;
    private RenderTexture2D UIBuffer;
    private Rectangle screenRect;
    private Rectangle screenUpRect;

    // Blur
    public RenderTexture2D BlurMaskBuffer;
    private Shader blurShader;
    private RenderTexture2D blurBuffer;
        
    private Rectangle blurDownRect;

    private int blurUITexLoc;
    private int blurPassLoc;

    public void Start(RenderTexture2D bBuffer, RenderTexture2D uiBuffer)
    {
        backBuffer = bBuffer;
        UIBuffer = uiBuffer;

        texelSize = new Vector2(1f / backBuffer.Texture.Width, 1f / backBuffer.Texture.Height);

        postBuffer = Raylib.LoadRenderTexture(backBuffer.Texture.Width, backBuffer.Texture.Height);

        screenRect = new Rectangle(0f, 0f, (float)backBuffer.Texture.Width,
            (float)-backBuffer.Texture.Height);

        screenUpRect = new Rectangle(0f, 0f, (float)(backBuffer.Texture.Width), (float)(backBuffer.Texture.Height));

        blurShader = Raylib.LoadShader(null, "Assets/Shaders/blur.fs");

        blurUITexLoc = Raylib.GetShaderLocation(blurShader, "maskTexture");
        blurPassLoc = Raylib.GetShaderLocation(blurShader, "pass");

        var blurTexelLoc = Raylib.GetShaderLocation(blurShader, "texelSize");
        var blurRadiuLoc = Raylib.GetShaderLocation(blurShader, "radius");

        blurBuffer = Raylib.LoadRenderTexture(64, 36);
        BlurMaskBuffer = Raylib.LoadRenderTexture(backBuffer.Texture.Width, backBuffer.Texture.Height);

        Raylib.SetTextureFilter(blurBuffer.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(BlurMaskBuffer.Texture, TextureFilter.Point);

        Raylib.SetTextureWrap(blurBuffer.Texture, TextureWrap.MirrorClamp);
        Raylib.SetTextureWrap(BlurMaskBuffer.Texture, TextureWrap.MirrorClamp);

        Raylib.SetShaderValue(blurShader, blurTexelLoc, texelSize * ((float)backBuffer.Texture.Width / blurBuffer.Texture.Width),
            ShaderUniformDataType.Vec2);

        Raylib.SetShaderValue(blurShader, blurRadiuLoc, 4f, ShaderUniformDataType.Float);

        blurDownRect = new Rectangle(0f, 0f, (float)(blurBuffer.Texture.Width), (float)(blurBuffer.Texture.Height));
    }
    
    public void Draw()
    {
        // Blur downscale
        Raylib.BeginTextureMode(blurBuffer);
        Raylib.BeginShaderMode(blurShader);

        Raylib.SetShaderValue(blurShader, blurPassLoc, 0, ShaderUniformDataType.Int);

        Raylib.DrawTexturePro(backBuffer.Texture, screenRect, blurDownRect, Vector2.Zero, 0.0f, Color.White);

        Raylib.EndShaderMode();
        Raylib.EndTextureMode();

        // Blur mask
        Raylib.BeginTextureMode(BlurMaskBuffer);
        Raylib.ClearBackground(Color.Blank);

        Raylib.BeginShaderMode(blurShader);

        Raylib.SetShaderValueTexture(blurShader, blurUITexLoc, UIBuffer.Texture);
        Raylib.SetShaderValue(blurShader, blurPassLoc, 1, ShaderUniformDataType.Int);

        Raylib.DrawTexturePro(blurBuffer.Texture, blurDownRect, screenUpRect, Vector2.Zero, 0.0f, Color.White);

        Raylib.EndShaderMode();
        Raylib.EndTextureMode();

        // Draw blur to screen
        Raylib.DrawTexture(BlurMaskBuffer.Texture, 0, 0, Color.White);
    }
}
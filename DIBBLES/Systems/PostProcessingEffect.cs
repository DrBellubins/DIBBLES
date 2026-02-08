using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects;

public abstract class PostProcessingEffect
{
    private static readonly List<PostProcessingEffect> _effects = new();

    protected RenderTarget2D? effectBuffer;

    protected Texture2D? _colorBuffer;

    protected GraphicsDevice Graphics
    {
        get { return Engine.Graphics; }
    }

    protected PostProcessingEffect()
    {
        _effects.Add(this);
    }

    public static IReadOnlyList<PostProcessingEffect> All
    {
        get { return _effects; }
    }

    public void SetBuffers(Texture2D color)
    {
        _colorBuffer = color;
    }

    public virtual void Start(RenderTarget2D input)
    {
        effectBuffer = new RenderTarget2D(
            Graphics,
            input.Width,
            input.Height,
            false,
            SurfaceFormat.HdrBlendable,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
    }

    public virtual void Dispose()
    {
        effectBuffer?.Dispose();
        effectBuffer = null;
    }

    public RenderTarget2D OutputBuffer
    {
        get { return effectBuffer ?? new RenderTarget2D(Engine.Graphics, Engine.ScreenWidth, Engine.ScreenHeight); }
    }

    public Texture2D ColorBuffer
    {
        get { return _colorBuffer ?? new RenderTarget2D(Engine.Graphics, Engine.ScreenWidth, Engine.ScreenHeight); }
    }

    // Begin drawing to the effect's own backbuffer
    public virtual void DrawStart()
    {
        Graphics.SetRenderTarget(effectBuffer);
        Graphics.Clear(Color.Transparent);
    }

    // End drawing; restore default render target
    public virtual void DrawEnd()
    {
        Graphics.SetRenderTarget(null);
    }
}
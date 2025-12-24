using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects;

public abstract class PostProcessingEffect
{
    private static readonly List<PostProcessingEffect> _effects = new();

    protected RenderTarget2D _effectBuffer;

    protected Texture2D _colorBuffer;
    protected Texture2D _normalBuffer;
    protected Texture2D _depthBuffer;

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

    public void SetBuffers(Texture2D color, Texture2D normal, Texture2D depth)
    {
        _colorBuffer = color;
        _normalBuffer = normal;
        _depthBuffer = depth;
    }

    public virtual void Start(int width, int height)
    {
        _effectBuffer = new RenderTarget2D(
            Graphics,
            width,
            height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
    }

    public virtual void Dispose()
    {
        _effectBuffer?.Dispose();
        _effectBuffer = null;
    }

    public RenderTarget2D OutputBuffer
    {
        get { return _effectBuffer; }
    }

    public Texture2D ColorBuffer
    {
        get { return _colorBuffer; }
    }

    public Texture2D NormalBuffer
    {
        get { return _normalBuffer; }
    }

    public Texture2D DepthBuffer
    {
        get { return _depthBuffer; }
    }

    // Begin drawing to the effect's own backbuffer
    public virtual void DrawStart()
    {
        Graphics.SetRenderTarget(_effectBuffer);
        Graphics.Clear(Color.Transparent);
    }

    // End drawing; restore default render target
    public virtual void DrawEnd()
    {
        Graphics.SetRenderTarget(null);
    }
}
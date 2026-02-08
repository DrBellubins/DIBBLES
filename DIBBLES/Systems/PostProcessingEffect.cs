using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Effects;

public abstract class PostProcessingEffect
{
    private static readonly List<PostProcessingEffect> _effects = new();

    protected RenderTarget2D? _colorBuffer;
    protected RenderTarget2D? effectBuffer;

    private static Effect? blitEffect;
    private static VertexBuffer? blitVB;
    private static IndexBuffer? blitIB;
    
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

    public void SetBuffers(RenderTarget2D color)
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

    public RenderTarget2D ColorBuffer
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

    public int SafeI(int i)
    {
        return i < 0 ? 0 : i;
    }
    
    /// <summary>
    /// GPU blit with scaling: copies 'source' into 'dest' (any size) with a shader.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="dest"></param>
    public void Blit(Texture2D source, RenderTarget2D dest)
    {
        if (source == null || dest == null)
            return;
    
        EnsureBlitResources();
    
        var graphics = Engine.Graphics;
        var prevViewport = graphics.Viewport;

        // States for a clean blit
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.None;
        graphics.RasterizerState = RasterizerState.CullNone;
        graphics.SamplerStates[0] = SamplerState.LinearClamp;

        // Bind destination RT and viewport sized to dest
        graphics.SetRenderTarget(dest);
        graphics.Viewport = new Viewport(0, 0, dest.Width, dest.Height);
        graphics.Clear(Color.Transparent);

        // Bind geometry
        graphics.SetVertexBuffer(blitVB);
        graphics.Indices = blitIB;

        // Bind effect
        blitEffect!.CurrentTechnique = blitEffect.Techniques["Blit"];
        EffectParams.SetTexture(blitEffect, "SourceTex", source);

        foreach (var pass in blitEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }

        // Restore
        graphics.Viewport = prevViewport;
        graphics.SetRenderTarget(null);
        graphics.SetVertexBuffer(null);
        graphics.Indices = null;
    }
    
    // Initialize fullscreen quad and effect once.
    private static void EnsureBlitResources()
    {
        if (blitEffect == null)
        {
            blitEffect = Engine.Instance.Content.Load<Effect>("Shaders/Blit");
        }
    
        if (blitVB == null)
        {
            var verts = new VertexPositionTexture[4];

            // Clip-space fullscreen quad with UVs
            verts[0] = new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1));
            verts[1] = new VertexPositionTexture(new Vector3( 1, -1, 0), new Vector2(1, 1));
            verts[2] = new VertexPositionTexture(new Vector3( 1,  1, 0), new Vector2(1, 0));
            verts[3] = new VertexPositionTexture(new Vector3(-1,  1, 0), new Vector2(0, 0));

            blitVB = new VertexBuffer(Engine.Graphics, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
            blitVB.SetData(verts);
        }
    
        if (blitIB == null)
        {
            var indices = new short[] { 0, 1, 2, 0, 2, 3 };
            blitIB = new IndexBuffer(Engine.Graphics, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            blitIB.SetData(indices);
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class BloomEffect : PostProcessingEffect
{
    public const int SampleCount = 4;
    
    public float Intensity { get; set; } = 10.0f;
    public float Radius { get; set; } = 2.0f;
    
    public RenderTarget2D BloomOutput;
    public List<RenderTarget2D> BloomRenderTargets = new();
    
    private Effect bloomEffect;
    private VertexBuffer quadVertexBuffer;
    private IndexBuffer quadIndexBuffer;
    
    public override void Start(int width, int height)
    {
        // Allocate OutputBuffer (effectBuffer) in base
        base.Start(width, height);
        
        bloomEffect = Engine.Instance.Content.Load<Effect>("Shaders/Bloom");
        ensureFullscreenQuad();
        buildChain(width, height);
    }

    // Main draw
    public override void DrawStart()
    {
        // Early out if no source
        if (ColorBuffer == null)
        {
            Graphics.SetRenderTarget(OutputBuffer);
            Graphics.Clear(Color.Black);
            Graphics.SetRenderTarget(null);
            Utils.Debug.Error("NULL");
            return;
        }
    
        // Ensure chain matches source size
        if (BloomRenderTargets == null
            || BloomRenderTargets.Count != SampleCount + 1
            || BloomRenderTargets[0].Width != ColorBuffer.Width
            || BloomRenderTargets[0].Height != ColorBuffer.Height)
        {
            buildChain(ColorBuffer.Width, ColorBuffer.Height);
        }
    
        // Render states (consistent with SSAO)
        Graphics.BlendState = BlendState.Opaque;
        Graphics.DepthStencilState = DepthStencilState.None;
        Graphics.RasterizerState = RasterizerState.CullNone;
        Graphics.SamplerStates[0] = SamplerState.LinearClamp;
    
        // Bind fullscreen quad
        Graphics.SetVertexBuffer(quadVertexBuffer);
        Graphics.Indices = quadIndexBuffer;
    
        // Downsample chain
        Texture2D src = ColorBuffer;
    
        for (int i = 0; i < BloomRenderTargets.Count; i++)
        {
            var texel = new Vector2(1f / src.Width, 1f / src.Height);
    
            bloomEffect.Parameters["SourceTex"]?.SetValue(src);
            bloomEffect.Parameters["TexelSize"]?.SetValue(texel);
    
            drawPass(BloomRenderTargets[i], bloomEffect, "BloomDownsample");
            src = BloomRenderTargets[i];
        }
    
        // Upsample back up
        Texture2D up = BloomRenderTargets[^1];
        float intensityIter = Math.Max(0f, Intensity);
        float radiusIter = Math.Max(0.0001f, Radius);
    
        for (int i = BloomRenderTargets.Count - 2; i >= 0; i--)
        {
            var texel = new Vector2(1f / up.Width, 1f / up.Height);
    
            intensityIter = Math.Max(0f, intensityIter - (Intensity * 0.5f));
            radiusIter = Math.Max(0.0001f, radiusIter - (Radius * 0.5f));
    
            bloomEffect.Parameters["SourceTex"]?.SetValue(up);
            bloomEffect.Parameters["TexelSize"]?.SetValue(texel);
            bloomEffect.Parameters["Intensity"]?.SetValue(intensityIter);
            bloomEffect.Parameters["Radius"]?.SetValue(radiusIter);
    
            drawPass(BloomRenderTargets[i], bloomEffect, "BloomUpsample");
            up = BloomRenderTargets[i];
        }
    
        // Expose full-res bloom layer for buffer debug
        BloomOutput = BloomRenderTargets[0];
    
        // Combine into our OutputBuffer (screen blend in shader)
        var previousViewport = Graphics.Viewport;
        Graphics.SetRenderTarget(OutputBuffer);
        Graphics.Viewport = new Viewport(0, 0, OutputBuffer.Width, OutputBuffer.Height);
        Graphics.Clear(Color.Black);

        bloomEffect.Parameters["SceneTex"]?.SetValue(ColorBuffer);
        bloomEffect.Parameters["BloomTex"]?.SetValue(BloomOutput);
        bloomEffect.Parameters["BloomIntensity"]?.SetValue(Intensity);

        bloomEffect.CurrentTechnique = bloomEffect.Techniques["BloomCombine"];

        foreach (var pass in bloomEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }

        // Restore viewport and leave RT unbound to the manager's DrawEnd
        Graphics.Viewport = previousViewport;
    }

    // Unbind and restore default RT
    public override void DrawEnd()
    {
        Graphics.SetVertexBuffer(null);
        Graphics.Indices = null;
        Graphics.SetRenderTarget(null);
    }
    
    // Dispose resources
    public override void Dispose()
    {
        for (int i = 0; i < BloomRenderTargets.Count; i++)
            BloomRenderTargets[i]?.Dispose();
    
        quadVertexBuffer?.Dispose();
        quadIndexBuffer?.Dispose();
    }
    
    // Create fullscreen quad in clip space [-1,1]
    private void ensureFullscreenQuad()
    {
        var verts = new VertexPositionTexture[4];

        verts[0] = new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1));
        verts[1] = new VertexPositionTexture(new Vector3( 1, -1, 0), new Vector2(1, 1));
        verts[2] = new VertexPositionTexture(new Vector3( 1,  1, 0), new Vector2(1, 0));
        verts[3] = new VertexPositionTexture(new Vector3(-1,  1, 0), new Vector2(0, 0));

        short[] idx = { 0, 1, 2, 0, 2, 3 };

        quadVertexBuffer = new VertexBuffer(Graphics, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
        quadVertexBuffer.SetData(verts);

        quadIndexBuffer = new IndexBuffer(Graphics, IndexElementSize.SixteenBits, idx.Length, BufferUsage.WriteOnly);
        quadIndexBuffer.SetData(idx);
    }
    
    // Allocate chain sized from source
    private void buildChain(int width, int height)
    {
        for (int i = 0; i < BloomRenderTargets.Count; i++)
            BloomRenderTargets[i]?.Dispose();

        BloomRenderTargets.Clear();

        BloomOutput?.Dispose();
        BloomOutput = new RenderTarget2D(
            Graphics,
            width,
            height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );

        BloomRenderTargets.Add(BloomOutput);

        int count = Math.Max(1, SampleCount);

        int rtWidth = width;
        int rtHeight = height;

        for (int i = 0; i < count; i++)
        {
            rtWidth = Math.Max(1, rtWidth / 2);
            rtHeight = Math.Max(1, rtHeight / 2);

            var rt = new RenderTarget2D(
                Graphics,
                rtWidth,
                rtHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );

            BloomRenderTargets.Add(rt);
        }
    }
    
    // Draw one pass
    private void drawPass(RenderTarget2D target, Effect fx, string technique)
    {
        var previousViewport = Graphics.Viewport;

        Graphics.SetRenderTarget(target);
        Graphics.Viewport = new Viewport(0, 0, target.Width, target.Height);
        Graphics.Clear(Color.Black);

        fx.CurrentTechnique = fx.Techniques[technique];

        // Ensure quad is bound (safe to rebind here)
        Graphics.SetVertexBuffer(quadVertexBuffer);
        Graphics.Indices = quadIndexBuffer;

        foreach (var pass in fx.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }

        // Restore viewport and unbind RT
        Graphics.Viewport = previousViewport;
        Graphics.SetRenderTarget(null);
    }
}
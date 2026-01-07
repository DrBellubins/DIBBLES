using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class BloomEffect : PostProcessingEffect
{
    public const int SampleCount = 4;
    
    public float Intensity { get; set; }
    public float Radius { get; set; }
    
    public RenderTarget2D BloomOutput;
    public List<RenderTarget2D> BloomRenderTargets = new();
    
    private Effect bloomEffect;
    private VertexBuffer quadVertexBuffer;
    private IndexBuffer quadIndexBuffer;
    
    public override void Start(int width, int height)
    {
        bloomEffect = Engine.Instance.Content.Load<Effect>("Shaders/Bloom");
        ensureFullscreenQuad();
        buildChain(width, height);
    }

    public override void DrawStart()
    {

    }

    public override void DrawEnd()
    {
        
    }
    
    // Main apply entry (call this from your post manager)
    public void Apply(RenderTarget2D scene, RenderTarget2D destination)
    {
        if (BloomRenderTargets == null || BloomRenderTargets.Count != Math.Max(1, SampleCount) ||
            BloomRenderTargets[0].Width * 2 != scene.Width || BloomRenderTargets[0].Height * 2 != scene.Height)
        {
            buildChain(scene.Width, scene.Height);
        }
    
        // Downsample
        Texture2D current = scene;
    
        for (int i = 0; i < BloomRenderTargets.Count; i++)
        {
            var texel = new Vector2(1f / current.Width, 1f / current.Height);
    
            bloomEffect.Parameters["SourceTex"]?.SetValue(current);
            bloomEffect.Parameters["TexelSize"]?.SetValue(texel);
    
            drawPass(BloomRenderTargets[i], bloomEffect, "BloomDownsample");
            current = BloomRenderTargets[i];
        }
    
        // Upsample
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
    
        // Combine
        bloomEffect.Parameters["SceneTex"]?.SetValue(scene);
        bloomEffect.Parameters["BloomTex"]?.SetValue(up);
        bloomEffect.Parameters["BloomIntensity"]?.SetValue(Intensity);
    
        drawPass(destination, bloomEffect, "BloomCombine");
    }
    
    // Optional: dispose resources
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

        int count = Math.Max(1, SampleCount);

        int rtWidth = width;
        int rtHeight = height;
        
        BloomOutput = new RenderTarget2D(Graphics, width, height);
        
        BloomRenderTargets.Add(BloomOutput);

        for (int i = 0; i < count; i++)
        {
            rtWidth = Math.Max(1, rtWidth / 2);
            rtHeight = Math.Max(1, rtHeight / 2);

            var renderTarget = new RenderTarget2D(Graphics, rtWidth, rtHeight, false, SurfaceFormat.Color, DepthFormat.None);
            BloomRenderTargets.Add(renderTarget);
        }
    }
    
    // Draw one pass
    private void drawPass(RenderTarget2D target, Effect fx, string technique)
    {
        Graphics.SetRenderTarget(target);
        Graphics.Clear(Color.Transparent);

        fx.CurrentTechnique = fx.Techniques[technique];

        Graphics.SetVertexBuffer(quadVertexBuffer);
        Graphics.Indices = quadIndexBuffer;

        foreach (var pass in fx.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }

        Graphics.SetVertexBuffer(null);
        Graphics.Indices = null;

        Graphics.SetRenderTarget(null);
    }
}
using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

// TODO: Implement quadratic threshold
public class BloomEffect : PostProcessingEffect
{
    public const int SampleCount = 6;
    
    public float Intensity { get; set; } = 1.0f; // Overall intensity
    public float Strength { get; set; } = 0.7f;  // Per sample intensity
    public float Radius { get; set; } = 2.0f;
    
    public float Threshold { get; set; } = 0.1f;
    public float ThresholdSoftKnee { get; set; } = 0.5f;
    
    public RenderTarget2D BloomOutput;

    public List<RenderTarget2D> DownsampleRTs = new();
    public List<RenderTarget2D> UpsampleRTs = new();
    
    private RenderTarget2D thresholdRT;
    
    private Effect bloomEffect;
    private VertexBuffer quadVertexBuffer;
    private IndexBuffer quadIndexBuffer;
    
    public override void Start(RenderTarget2D input)
    {
        // Allocate OutputBuffer (effectBuffer) in base
        base.Start(input);
        
        bloomEffect = Engine.Instance.Content.Load<Effect>("Shaders/Bloom");
        ensureFullscreenQuad();
        buildChain(input.Width, input.Height);
        
        // Full-res threshold buffer
        thresholdRT = new RenderTarget2D(
            Graphics,
            input.Width,
            input.Height,
            false,
            GameScene.BackBufferFormat,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
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
            Debug.Error("Bloom ColorBuffer NULL");
            return;
        }
    
        // Ensure chain matches source size
        ensureChainMatchesSource(ColorBuffer.Width, ColorBuffer.Height);
    
        // Render states
        Graphics.BlendState = BlendState.Opaque;
        Graphics.DepthStencilState = DepthStencilState.None;
        Graphics.RasterizerState = RasterizerState.CullNone;
        Graphics.SamplerStates[0] = SamplerState.LinearClamp;
    
        // Bind fullscreen quad
        Graphics.SetVertexBuffer(quadVertexBuffer);
        Graphics.Indices = quadIndexBuffer;
    
        // 1) Threshold: ColorBuffer -> thresholdRT
        EffectParams.SetTexture(bloomEffect, "SourceTex", ColorBuffer);
        EffectParams.SetVector2(bloomEffect, "TexelSize", new Vector2(1f / ColorBuffer.Width, 1f / ColorBuffer.Height));
        EffectParams.SetFloat(bloomEffect, "Threshold", Threshold);
        EffectParams.SetVector3(bloomEffect, "ThresholdCurve", genThresholdCurve(Threshold, ThresholdSoftKnee));

        drawPass(thresholdRT, bloomEffect, "BloomThreshold");
        
        // 2) Downsample chain:
        //    First level: thresholdRT -> DownsampleRTs[0]
        EffectParams.SetTexture(bloomEffect, "SourceTex", thresholdRT);
        EffectParams.SetVector2(bloomEffect, "TexelSize", new Vector2(1f / thresholdRT.Width, 1f / thresholdRT.Height));
        
        drawPass(DownsampleRTs[0], bloomEffect, "BloomDownsample");
    
        // Subsequent levels: DownsampleRTs[i-1] -> DownsampleRTs[i]
        for (int i = 1; i < DownsampleRTs.Count; i++)
        {
            var src = DownsampleRTs[i - 1];
            var dst = DownsampleRTs[i];

            EffectParams.SetTexture(bloomEffect, "SourceTex", src);
            EffectParams.SetVector2(bloomEffect, "TexelSize", new Vector2(1f / src.Width, 1f / src.Height));

            drawPass(dst, bloomEffect, "BloomDownsample");
        }
        
        // 3) Upsample chain:
        //    Start from the smallest downsample result and progressively upsample to larger targets
        RenderTarget2D upsampleSrc = DownsampleRTs[^1]; // last downsample RT (smallest)
    
        for (int i = UpsampleRTs.Count - 1; i >= 0; i--)
        {
            var target = UpsampleRTs[i];
    
            EffectParams.SetTexture(bloomEffect, "SourceTex", upsampleSrc);
            EffectParams.SetVector2(bloomEffect, "TexelSize", new Vector2(1f / upsampleSrc.Width, 1f / upsampleSrc.Height));
            
            EffectParams.SetFloat(bloomEffect, "Strength", Strength);
            EffectParams.SetFloat(bloomEffect, "Radius", Radius);
    
            drawPass(target, bloomEffect, "BloomUpsample");
    
            // Prepare for next stage
            upsampleSrc = target;
        }
    
        // Full-res bloom layer for buffer debug and combine
        BloomOutput = UpsampleRTs[0];
    
        // 4) Combine bloom with the scene into OutputBuffer
        var previousViewport = Graphics.Viewport;
        Graphics.SetRenderTarget(OutputBuffer);
        Graphics.Viewport = new Viewport(0, 0, OutputBuffer.Width, OutputBuffer.Height);
        Graphics.Clear(Color.Black);

        EffectParams.SetTexture(bloomEffect, "SceneTex", ColorBuffer);
        EffectParams.SetTexture(bloomEffect, "BloomTex", BloomOutput);
        
        EffectParams.SetFloat(bloomEffect, "Intensity", Intensity);
    
        bloomEffect.CurrentTechnique = bloomEffect.Techniques["BloomCombine"];
    
        foreach (var pass in bloomEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    
        // Restore viewport; DrawEnd will unbind targets
        Graphics.Viewport = previousViewport;
    }

    // Unbind and restore default RT
    public override void DrawEnd()
    {
        Graphics.SetVertexBuffer(null);
        Graphics.Indices = null;
        Graphics.SetRenderTarget(null);
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
        // Dispose old
        foreach (var rt in DownsampleRTs)
            rt?.Dispose();
        
        foreach (var rt in UpsampleRTs)
            rt?.Dispose();

        DownsampleRTs.Clear();
        UpsampleRTs.Clear();

        // Allocate downsample chain: half res first, then quarter, etc.
        int count = Math.Max(1, SampleCount);

        for (int i = 0; i < count; i++)
        {
            int dsW = Math.Max(1, width >> (i + 1));   // /2, /4, /8...
            int dsH = Math.Max(1, height >> (i + 1));

            var ds = new RenderTarget2D(
                Graphics,
                dsW,
                dsH,
                false,
                GameScene.BackBufferFormat,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );

            DownsampleRTs.Add(ds);
        }

        // Allocate upsample chain: start at smallest level size, end at full res
        for (int i = 0; i < count; i++)
        {
            int usW = (i == 0) ? width : DownsampleRTs[i - 1].Width;   // full, half, quarter...
            int usH = (i == 0) ? height : DownsampleRTs[i - 1].Height;

            var us = new RenderTarget2D(
                Graphics,
                usW,
                usH,
                false,
                GameScene.BackBufferFormat,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );

            UpsampleRTs.Add(us);
        }

        // Recreate full-res BloomOutput compatible with current size
        BloomOutput?.Dispose();
        BloomOutput = new RenderTarget2D(
            Graphics,
            width,
            height,
            false,
            SurfaceFormat.HdrBlendable,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
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
    
    private void ensureChainMatchesSource(int width, int height)
    {
        int count = Math.Max(1, SampleCount);

        bool needsRebuild =
            DownsampleRTs.Count != count ||
            UpsampleRTs.Count != count ||
            UpsampleRTs.Count == 0 ||
            DownsampleRTs.Count == 0 ||
            UpsampleRTs[0].Width != width ||
            UpsampleRTs[0].Height != height ||
            DownsampleRTs[0].Width != Math.Max(1, width / 2) ||
            DownsampleRTs[0].Height != Math.Max(1, height / 2);

        if (needsRebuild)
        {
            buildChain(width, height);
        }
    }
    
    private Vector3 genThresholdCurve(float threshold, float softKnee)
    {
        float k = MathF.Max(threshold * softKnee, 1e-5f);
        float cx = threshold - k;
        float cy = 2.0f * k;
        float cz = 0.25f / k;
        
        return new Vector3(cx, cy, cz);
    }
    
    // Dispose resources
    public override void Dispose()
    {
        base.Dispose();

        foreach (var rt in DownsampleRTs)
            rt?.Dispose();
        
        DownsampleRTs.Clear();

        foreach (var rt in UpsampleRTs)
            rt?.Dispose();
        
        UpsampleRTs.Clear();

        BloomOutput?.Dispose();
        BloomOutput = null;
        
        thresholdRT?.Dispose();
        thresholdRT = null;

        quadVertexBuffer?.Dispose();
        quadVertexBuffer = null;

        quadIndexBuffer?.Dispose();
        quadIndexBuffer = null;
    }
}
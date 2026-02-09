using DIBBLES.Systems.DebugMenu;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class TonemappingEffect : PostProcessingEffect
{
    public bool Enabled = true;
    public float PreBrightness = 1.4f;
    public float PostBrightness = 1.3f;
    
    private Effect tonemapEffect;
    
    private VertexBuffer quadVertexBuffer;
    private IndexBuffer quadIndexBuffer;
    
    // Initialize
    public override void Start(RenderTarget2D input)
    {
        base.Start(input);
        
        tonemapEffect = Engine.Instance.Content.Load<Effect>("Shaders/Tonemap");
        
        // Fullscreen quad in clip-space [-1..1]
        var verts = new VertexPositionTexture[4];
        verts[0] = new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1));
        verts[1] = new VertexPositionTexture(new Vector3( 1, -1, 0), new Vector2(1, 1));
        verts[2] = new VertexPositionTexture(new Vector3( 1,  1, 0), new Vector2(1, 0));
        verts[3] = new VertexPositionTexture(new Vector3(-1,  1, 0), new Vector2(0, 0));
    
        quadVertexBuffer = new VertexBuffer(Engine.Graphics, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
        quadVertexBuffer.SetData(verts);
    
        short[] idx = { 0, 1, 2, 0, 2, 3 };
        quadIndexBuffer = new IndexBuffer(Engine.Graphics, IndexElementSize.SixteenBits, idx.Length, BufferUsage.WriteOnly);
        quadIndexBuffer.SetData(idx);
        
        DebugMenu.CreateButton();
        
        DebugMenu.RegisterParams
        (
            new SliderParam("PreBrightness", 0.0f, 10.0f, () => PreBrightness, v => PreBrightness = v),
            new SliderParam("PostBrightness", 0.0f, 10.0f, () => PostBrightness, v => PostBrightness = v),
            new CheckBoxParam("Enabled", () => Enabled, v => Enabled = v)
        );
    }

    // Main drawing here
    public override void DrawStart()
    {
        if (tonemapEffect == null || quadVertexBuffer == null || quadIndexBuffer == null)
            return;

        if (!Enabled)
        {
            // Ensure our output is transparent this frame so composite draws nothing.
            Graphics.SetRenderTarget(OutputBuffer);
            Graphics.Clear(Color.Transparent);
        
            // Restore default RT immediately.
            Graphics.SetRenderTarget(null);
        
            // Also ensure no stray buffers are left bound.
            Graphics.SetVertexBuffer(null);
            Graphics.Indices = null;
        
            return;
        }
        
        var graphics = Engine.Graphics;

        // States for a clean fullscreen draw
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.None;
        graphics.RasterizerState = RasterizerState.CullNone;
        graphics.SamplerStates[0] = SamplerState.LinearClamp;

        // Bind geometry
        graphics.SetVertexBuffer(quadVertexBuffer);
        graphics.Indices = quadIndexBuffer;

        // Output target (display-referred)
        graphics.SetRenderTarget(OutputBuffer);
        graphics.Clear(Color.Transparent);

        // Set effect params (only the source texture is needed)
        EffectParams.SetTexture(tonemapEffect, "SourceTex", ColorBuffer);
        
        EffectParams.SetFloat(tonemapEffect, "PreBrightness", PreBrightness);
        EffectParams.SetFloat(tonemapEffect, "PostBrightness", PostBrightness);
        
        // Use the ACES technique implemented in Tonemap.fx
        tonemapEffect.CurrentTechnique = tonemapEffect.Techniques["TonemapACES"];

        foreach (var pass in tonemapEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    }
    
    // Return to previous states/buffers
    public override void DrawEnd()
    {
        var graphics = Engine.Graphics;
        graphics.SetVertexBuffer(null);
        graphics.Indices = null;
        graphics.SetRenderTarget(null);
    }

    // Cleanup
    public override void Dispose()
    {
        base.Dispose();
    
        quadVertexBuffer?.Dispose();
        quadVertexBuffer = null;
    
        quadIndexBuffer?.Dispose();
        quadIndexBuffer = null;
    }
}
using DIBBLES.Systems.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

// Use this as a template for new post processing effects
public class BasicPostProcess : PostProcessingEffect
{
    private Effect basicEffect;
    
    private VertexBuffer quadVertexBuffer;
    private IndexBuffer quadIndexBuffer;
    
    // Initialize
    public override void Start(RenderTarget2D input)
    {
        base.Start(input);
        
        //basicEffect = Engine.Instance.Content.Load<Effect>("Shaders/BasicPostProcess");
        
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
    }

    // Main drawing here
    public override void DrawStart()
    {
    }
    
    // Return to previous states/buffers
    public override void DrawEnd()
    {
    }

    // Cleanup
    public override void Dispose()
    {
        base.Dispose();
    }
}
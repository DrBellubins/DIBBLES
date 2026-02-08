using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

// Use this as a template for new post processing effects
public class BasicPostProcess : PostProcessingEffect
{
    private Effect basicEffect;
    
    // Initialize
    public override void Start(RenderTarget2D input)
    {
        base.Start(input);
        
        //basicEffect = Engine.Instance.Content.Load<Effect>("Shaders/BasicPostProcess");
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
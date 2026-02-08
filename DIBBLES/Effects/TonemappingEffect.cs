using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class TonemappingEffect : PostProcessingEffect
{
    private Effect tonemapEffect;
    
    // Initialize
    public override void Start(RenderTarget2D input)
    {
        base.Start(input);
        
        tonemapEffect = Engine.Instance.Content.Load<Effect>("Shaders/Tonemap");
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects;

public class PostProcessingManager
{
    // Initialize every post processing effect in order here!
    public SSAOPostProcess ssaoPostProcess = new();
    public BloomEffect bloom = new();
    
    public void Initialize(int width, int height)
    {
        foreach (var effect in PostProcessingEffect.All)
            effect.Start(width, height);
    }

    // Pass the G-buffer textures to each effect and allow them to render to their backbuffer
    public void ApplyAll(RenderTarget2D color)
    {
        foreach (var effect in PostProcessingEffect.All)
        {
            effect.SetBuffers(color);
            effect.DrawStart();
            
            // Derived effects perform their draw here (override DrawStart/DrawEnd or render between)
            effect.DrawEnd();
        }
    }

    // Composite all effects' outputs over the scene (caller should enclose in a UIBatch)
    public void Draw()
    {
        // Force opaque compositing for post buffers so alpha=0 won’t hide them.
        UIBatch.SetBlendState(BlendState.Opaque);
        
        foreach (var effect in PostProcessingEffect.All)
        {
            var output = effect.OutputBuffer;

            if (output != null)
            {
                UIBatch.Draw(
                    output,
                    Vector2.Zero,
                    new Vector2(Engine.ScreenWidth, Engine.ScreenHeight),
                    Color.White
                );
            }
        }
        
        // Restore default for subsequent UI draws
        UIBatch.SetBlendState(BlendState.AlphaBlend);
    }
}
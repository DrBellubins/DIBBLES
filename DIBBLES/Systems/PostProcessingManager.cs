using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects;

public static class PostProcessingManager
{
    public static void Initialize(int width, int height)
    {
        foreach (var effect in PostProcessingEffect.All)
            effect.Start(width, height);
    }

    // Pass the G-buffer textures to each effect and allow them to render to their backbuffer
    public static void ApplyAll(RenderTarget2D color, RenderTarget2D normal, RenderTarget2D depth)
    {
        foreach (var effect in PostProcessingEffect.All)
        {
            effect.SetBuffers(color, normal, depth);
            effect.DrawStart();
            
            // Derived effects perform their draw here (override DrawStart/DrawEnd or render between)
            effect.DrawEnd();
        }
    }

    // Composite all effects' outputs over the scene (caller should enclose in a UIBatch)
    public static void Draw()
    {
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
    }
}
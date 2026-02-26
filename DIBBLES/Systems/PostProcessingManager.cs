using DIBBLES.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Effects;

// TODO: Implement ability to input MRT into shaders
public class PostProcessingManager
{
    // Initialize every post processing effect in order here!
    public SSAOPostProcess ssaoPostProcess = new();
    public BloomEffect bloom = new();
    public TonemappingEffect tonemapping = new();
    
    public void Initialize(int width, int height)
    {
        for (int i = 0; i < PostProcessingEffect.All.Count; i++)
        {
            var effect = PostProcessingEffect.All[i];
            var inputRT = i == 0 ? RenderEngine.BackBuffer :
                PostProcessingEffect.All[GMath.Clamp(i - 1, 0, PostProcessingEffect.All.Count)].OutputBuffer;
            
            effect.Start(inputRT);
        }
    }

    // Pass the G-buffer textures to each effect and allow them to render to their backbuffer
    public void ApplyAll(RenderTarget2D color)
    {
        RenderTarget2D inputTexture = color;
        
        foreach (var effect in PostProcessingEffect.All)
        {
            effect.SetBuffers(inputTexture);
            effect.DrawStart();
            
            // Derived effects perform their draw here (override DrawStart/DrawEnd or render between)
            effect.DrawEnd();
            
            // Next stage reads from the output of the previous stage
            inputTexture = effect.OutputBuffer;
        }
    }

    // Composite all effects' outputs over the scene (caller should enclose in a UIBatch)
    public void Draw()
    {
        // Composite only the final output in the chain
        UIBatch.SetBlendState(BlendState.Opaque);

        Texture2D finalOutput = (PostProcessingEffect.All.Count > 0)
            ? PostProcessingEffect.All[PostProcessingEffect.All.Count - 1].OutputBuffer
            : RenderEngine.BackBuffer;

        UIBatch.Draw(
            finalOutput,
            Vector2.Zero,
            new Vector2(Engine.ScreenWidth, Engine.ScreenHeight),
            Color.White
        );

        UIBatch.SetBlendState(BlendState.AlphaBlend);
    }
}
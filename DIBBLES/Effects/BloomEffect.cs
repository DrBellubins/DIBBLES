using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class BloomEffect : PostProcessingEffect
{
    public const int SampleCount = 4;
    
    public float Intensity { get; set; }
    public float Radius { get; set; }
    
    public const float BloomStrength1 = 0.5f;
    public const float BloomStrength2 = 1;
    public const float BloomStrength3 = 2;
    public const float BloomStrength4 = 1;
    public const float BloomStrength5 = 2;
    
    public const float BloomRadius1 = 1.0f;
    public const float BloomRadius2 = 2.0f;
    public const float BloomRadius3 = 2.0f;
    public const float BloomRadius4 = 4.0f;
    public const float BloomRadius5 = 4.0f;
    
    public List<RenderTarget2D> BloomRenderTargets = new();
    
    private RenderTarget2D bloomMip0;
    
    private Effect bloomEffect;
    
    public override void Start(int width, int height)
    {
        int rtWidth = width;
        int rtHeight = height;
        
        
        bloomEffect = Engine.Instance.Content.Load<Effect>("Effects/Bloom");

        bloomMip0 = new RenderTarget2D(Graphics, width, height);
        
        BloomRenderTargets.Add(bloomMip0);
        
        for (int i = 0; i < SampleCount; i++)
        {
            rtWidth /= 2;
            rtHeight /= 2;
            
            var renderTarget = new RenderTarget2D(Graphics, rtWidth, rtHeight);
            BloomRenderTargets.Add(renderTarget);
        }
    }

    public override void DrawStart()
    {
        // Downsample
        var bloomInput = bloomMip0;
        bloomEffect.Parameters["IsDownsample"]?.SetValue(true);

        for (int i = 0; i < SampleCount; ++i)
        {
            var downsampleRT = BloomRenderTargets[i];
            
            int width = downsampleRT.Width;
            int height = downsampleRT.Height;
            
            Vector2 currentTexelSize = new Vector2(1.0f / width, 1.0f / height);

            bloomEffect.Parameters["TexelSize"]?.SetValue(currentTexelSize);

            Scaler.SetInput(bloomInput);
            Scaler.SetOutput(downsampleRT);
            Scaler.Draw(context, $"Bloom downsample {i}");

            bloomInput = downsampleRT;
        }
        
        // Upsample
        bloomEffect.Parameters["IsDownsample"]?.SetValue(false);

        var intensityIterator = Intensity;
        var radiusIterator = Radius;
        
        for (int i = 0; i < SampleCount; ++i)
        {
            var upsampleRT = BloomRenderTargets[i];
            
            int width = upsampleRT.Width;
            int height = upsampleRT.Height;
            
            Vector2 currentTexelSize = new Vector2(1.0f / width, 1.0f / height);
            
            intensityIterator -= Intensity / 2;
            radiusIterator -= Radius / 2;

            bloomEffect.Parameters["TexelSize"]?.SetValue(currentTexelSize);
            bloomEffect.Parameters["Intensity"]?.SetValue(intensityIterator);
            bloomEffect.Parameters["Radius"]?.SetValue(radiusIterator);

            Scaler.SetInput(bloomInput);
            Scaler.SetOutput(upsampleRT);
            Scaler.Draw(context, $"Bloom upsample {i}");

            bloomInput = upsampleRT;
        }
    }

    public override void DrawEnd()
    {
        
    }

    public override void Dispose()
    {
        
    }
}
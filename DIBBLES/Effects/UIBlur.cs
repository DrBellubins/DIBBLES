using DIBBLES.Scenes;
using DIBBLES.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class UIBlur
{
    // Buffers
    public RenderTarget2D BlurBuffer;      // Low-res downsampled buffer (e.g. 128x72)
    public RenderTarget2D UIBlurBuffer;    // Final output buffer (same as screen/UI resolution)
    
    private const int blurBufferWidth = 128;
    private const int blurBufferHeight = 72;
    private const float blurRadius = 8f;
    
    private Effect uiBlurEffect;
    
    // Quad geometry
    private VertexPositionTexture[] quadVertices;
    private short[] quadIndices;

    private float[] kernel = new float[9];
    
    public void Start()
    {
        uiBlurEffect = Engine.Instance.Content.Load<Effect>("Shaders/UIBlur");
        
        BlurBuffer = new RenderTarget2D(Engine.Graphics, blurBufferWidth, blurBufferHeight, false, SurfaceFormat.Color, DepthFormat.None);
        UIBlurBuffer = new RenderTarget2D(Engine.Graphics, Engine.ScreenWidth, Engine.ScreenHeight, false, SurfaceFormat.Color, DepthFormat.None);
        
        // Fullscreen quad for drawing
        quadVertices = new VertexPositionTexture[]
        {
            new VertexPositionTexture(new Vector3(-1,  1, 0), new Vector2(0, 0)),
            new VertexPositionTexture(new Vector3( 1,  1, 0), new Vector2(1, 0)),
            new VertexPositionTexture(new Vector3( 1, -1, 0), new Vector2(1, 1)),
            new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
        };
        
        quadIndices = new short[] { 0, 1, 2, 0, 2, 3 };
        
        kernel = GaussianWeights(9, blurRadius * 0.5f);
    }

    public void Apply()
    {
        var graphics = Engine.Graphics;
        
        // ----------- STAGE 1: Downsample BackBuffer to BlurBuffer -----------
        graphics.SetRenderTarget(UIBlurBuffer);
        graphics.Clear(Color.Transparent);

        DrawFullscreenQuad();

        // ----------- Done, unset RenderTarget for final screen draw -----------
        graphics.SetRenderTarget(null);
    }
    
    public void Draw()
    {
        UIBatch.Draw(UIBlurBuffer, Vector2.Zero, new Vector2(Engine.ScreenWidth, Engine.ScreenHeight), Color.White);
    }
    
    private void DrawFullscreenQuad()
    {
        var graphics = Engine.Graphics;
        
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.None;
        graphics.RasterizerState = RasterizerState.CullNone;
        graphics.SamplerStates[0] = SamplerState.LinearClamp;

        foreach (var pass in uiBlurEffect.CurrentTechnique.Passes)
        {
            uiBlurEffect.Parameters["texelSize"].SetValue(new Vector2(1f / Engine.ScreenWidth, 1f / Engine.ScreenHeight));
            uiBlurEffect.Parameters["radius"].SetValue(blurRadius);
            
            pass.Apply();
            graphics.DrawUserIndexedPrimitives<VertexPositionTexture>(
                PrimitiveType.TriangleList,
                quadVertices, 0, 4,
                quadIndices, 0, 2);
        }
    }
    
    private float[] GaussianWeights(int kernelSize, float sigma)
    {
        float[] weights = new float[kernelSize];
        float sum = 0f;
        int half = kernelSize / 2;
        
        for (int i = 0; i < kernelSize; i++)
        {
            int x = i - half;
            
            weights[i] = (float)Math.Exp(-(x * x) / (2 * sigma * sigma));
            sum += weights[i];
        }
        
        // Normalize
        for (int i = 0; i < kernelSize; i++)
            weights[i] /= sum;
        
        return weights;
    }
}
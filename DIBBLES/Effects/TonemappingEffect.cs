using DIBBLES.Systems.DebugMenu;
using DIBBLES.Systems.Rendering;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class TonemappingEffect : PostProcessingEffect
{
    public bool Enabled = true;
    
    // ACES defaults
    private float preBrightnessACES = 1.15f;
    private float postBrightnessACES = 1.25f;
    
    // AgX defaults
    private float preBrightnessAgX = 1.0f;
    private float postBrightnessAgX = 1.2f;
    
    // Hable defaults
    private float preBrightnessHable = 1.875f;
    private float postBrightnessHable = 2.26f;
    
    private int algorithmSelectionIndex = 2; // 0 = ACES, 1 = AgX, 2 = Hable
    
    private float contrast = 0.04f;
    private float saturation = 1.0f;
    
    // AgX
    private float exposureEV = 0.5f;    // Stops
    private int agxLook = 1;            // 0=Low, 1=Medium, 2=High, 3=VeryHigh
    
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
        
        DebugMenu.RegisterMenuItem(
            "Tonemapping",
            new CheckBoxParam("Enabled", () => Enabled, v => Enabled = v),
            
            new DropdownParam("Algorithm", new []
            {
                "ACES",
                "AgX",
                "Hable"
            }, algorithmSelectionIndex, () => algorithmSelectionIndex, v => algorithmSelectionIndex = v),
            
            new SeparatorParam("ACES"),
            
            new SliderParam("Pre-Brightness ACES", 0.0f, 4.0f, () => preBrightnessACES, v => preBrightnessACES = v),
            new SliderParam("Post-Brightness ACES", 0.0f, 4.0f, () => postBrightnessACES, v => postBrightnessACES = v),
            
            new SeparatorParam("AgX"),
            
            new SliderParam("Pre-Brightness AgX", 0.0f, 4.0f, () => preBrightnessAgX, v => preBrightnessAgX = v),
            new SliderParam("Post-Brightness AgX", 0.0f, 4.0f, () => postBrightnessAgX, v => postBrightnessAgX = v),
            
            new SeparatorParam("Hable"),
            
            new SliderParam("Pre-Brightness Hable", 0.0f, 4.0f, () => preBrightnessHable, v => preBrightnessHable = v),
            new SliderParam("Post-Brightness Hable", 0.0f, 4.0f, () => postBrightnessHable, v => postBrightnessHable = v),
            
            new SeparatorParam("Color Corrections"),
            new SliderParam("Contrast", 0.0f, 4.0f, () => contrast, v => contrast = v),
            new SliderParam("Saturation", 0.0f, 4.0f, () => saturation, v => saturation = v)
        );
    }

    // Main drawing here
    public override void DrawStart()
    {
        if (tonemapEffect == null || quadVertexBuffer == null || quadIndexBuffer == null)
            return;

        if (!Enabled)
        {
            // Pass-through: copy input color to output so downstream effects still get the scene
            Blit(ColorBuffer, OutputBuffer);

            Graphics.SetVertexBuffer(null);
            Graphics.Indices = null;
            Graphics.SetRenderTarget(null);
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
        
        EffectParams.SetInt(tonemapEffect, "Algorithm", algorithmSelectionIndex);
        
        EffectParams.SetFloat(tonemapEffect, "Contrast", contrast);
        EffectParams.SetFloat(tonemapEffect, "Saturation", saturation);
        
        if (algorithmSelectionIndex == 0) // ACES
        {
            EffectParams.SetFloat(tonemapEffect, "PreBrightness", preBrightnessACES);
            EffectParams.SetFloat(tonemapEffect, "PostBrightness", postBrightnessACES);
        }
        else if (algorithmSelectionIndex == 1) // AgX
        {
            EffectParams.SetFloat(tonemapEffect, "PreBrightness", preBrightnessAgX);
            EffectParams.SetFloat(tonemapEffect, "PostBrightness", postBrightnessAgX);
            
            EffectParams.SetFloat(tonemapEffect, "ExposureEV", exposureEV);
            EffectParams.SetInt(tonemapEffect, "AgxLook", agxLook);
            EffectParams.SetFloat(tonemapEffect, "agxSaturation", 1.0f);
        }
        else if (algorithmSelectionIndex == 2) // Hable
        {
            EffectParams.SetFloat(tonemapEffect, "PreBrightness", preBrightnessHable);
            EffectParams.SetFloat(tonemapEffect, "PostBrightness", postBrightnessHable);
        }
        
        tonemapEffect.CurrentTechnique = tonemapEffect.Techniques["Tonemap"];

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
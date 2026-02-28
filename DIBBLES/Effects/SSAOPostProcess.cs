using System.Runtime.InteropServices;
using DIBBLES.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;
using DIBBLES.Systems.DebugMenu;
using DIBBLES.Systems.Rendering;
using DIBBLES.Utils;

namespace DIBBLES.Effects;

// TODO: SSAO Fog fadeout happens in world-space, when moving further from 0,0,0, ssao fades out
public class SSAOPostProcess : PostProcessingEffect
{
    public float Radius = 0.5f;
    public float Bias = 0.02f;
    public float TotalStrength = 0.8f;
    public float BaseAO = 0.05f;
    
    public static bool Enabled = true;
    
    private Effect? effect;

    private Texture2D? blueNoiseTex;
    
    private VertexBuffer? vertexBuffer;
    private IndexBuffer? indexBuffer;
    
    public RenderTarget2D? SSAOTarget;
    public RenderTarget2D? SSAOBlurTarget;

    public override void Start(RenderTarget2D input)
    {
        base.Start(input);

        effect = Engine.Instance.Content.Load<Effect>("Shaders/SSAOPostProcess");
        blueNoiseTex = Engine.Instance.Content.Load<Texture2D>("Textures/BlueNoise");

        // Allocate intermediate AO buffers
        SSAOTarget = new RenderTarget2D(Graphics, input.Width, input.Height, false, SurfaceFormat.Color, DepthFormat.None);
        SSAOBlurTarget = new RenderTarget2D(Graphics, input.Width, input.Height, false, SurfaceFormat.Color, DepthFormat.None);
        
        var verts = new VertexPositionTexture[]
        {
            new VertexPositionTexture(new Vector3(-1f, -1f, 0f), new Vector2(0f, 1f)),
            new VertexPositionTexture(new Vector3(-1f,  1f, 0f), new Vector2(0f, 0f)),
            new VertexPositionTexture(new Vector3( 1f,  1f, 0f), new Vector2(1f, 0f)),
            new VertexPositionTexture(new Vector3( 1f, -1f, 0f), new Vector2(1f, 1f))
        };

        vertexBuffer = new VertexBuffer(Graphics, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(verts);

        var indices = new short[] { 0, 1, 2, 0, 2, 3 };

        indexBuffer = new IndexBuffer(Graphics, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        indexBuffer.SetData(indices);

        DebugMenu.RegisterMenuItem(
            "SSAO",
            new CheckBoxParam("Enabled", () => Enabled, v => Enabled = v),
            new SliderParam("Radius", 0.0f, 5.0f, () => Radius, v => Radius = v),
            new SliderParam("Bias", 0.0f, 0.1f, () => Bias, v => Bias = v),
            new SliderParam("TotalStrength", 0.0f, 5.0f, () => TotalStrength, v => TotalStrength = v),
            new SliderParam("BaseAO", 0.0f, 0.1f, () => BaseAO, v => BaseAO = v),
            new TextureDisplayParam("SSAOTarget", () => SSAOBlurTarget)
        );
    }

    public override void DrawStart()
    {
        if (effect == null || blueNoiseTex == null || SSAOTarget == null || SSAOBlurTarget == null)
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
        
        // State
        Graphics.BlendState = BlendState.Opaque;
        Graphics.DepthStencilState = DepthStencilState.None;
        Graphics.RasterizerState = RasterizerState.CullNone;
    
        // Bind fullscreen quad buffers BEFORE drawing
        Graphics.SetVertexBuffer(vertexBuffer);
        Graphics.Indices = indexBuffer;
        
        // Set G-buffer textures
        
        effect.SetValue("DepthTex", RenderEngine.DepthBuffer);
        effect.SetValue("NormalTex", RenderEngine.NormalBuffer);
        effect.SetValue("RandomTex", blueNoiseTex);
    
        // Camera params
        var proj = GameScene.PlayerCharacter.Camera.Projection;
        var invProj = Matrix.Invert(proj);
    
        effect.SetValue("Projection", proj);
        effect.SetValue("InvProjection", invProj);
        
        effect.SetValue("CameraPos", GameScene.PlayerCharacter.Camera.Position.ToVector3());
        effect.SetValue("CameraNear", GameScene.PlayerCharacter.Camera.NearPlane);
        effect.SetValue("CameraFar", GameScene.PlayerCharacter.Camera.FarPlane);
        
        effect.SetValue("FogNear", FogEffect.FogNear);
        effect.SetValue("FogFar", FogEffect.FogFar);
        
        effect.SetValue("ScreenSize", new Vector2(Engine.ScreenWidth, Engine.ScreenHeight));
    
        float tanHalfFovY = 1.0f / proj.M22;
        float aspectRatio = proj.M22 / proj.M11;
    
        effect.SetValue("TanHalfFovY", tanHalfFovY);
        effect.SetValue("AspectRatio", aspectRatio);
    
        var noiseScale = new Vector2(
            (float)Engine.ScreenWidth / blueNoiseTex.Width,
            (float)Engine.ScreenHeight / blueNoiseTex.Height
        );
    
        effect.SetValue("NoiseScale", noiseScale);

        effect.SetValue("radius", Radius);
        effect.SetValue("bias", Bias);
        effect.SetValue("total_strength", TotalStrength);
        effect.SetValue("base_ao", BaseAO);
        
        effect.SetValue("BlurDepthSigma", 5.5f);
        effect.SetValue("BlurNormalPower", 14.0f);
    
        // Pass 1: SSAO -> SSAOTarget
        Graphics.SetRenderTarget(SSAOTarget);
        Graphics.Clear(Color.White);
        effect.CurrentTechnique = effect.Techniques["SSAO"];
    
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    
        // Pass 2: BlurH (read SSAOTarget, write SSAOBlurTarget)
        Graphics.SetRenderTarget(SSAOBlurTarget);
        Graphics.Clear(Color.White);
        
        effect.SetValue("AOTex", SSAOTarget);
        
        effect.CurrentTechnique = effect.Techniques["BlurH"];
    
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    
        // Pass 3: BlurV (read SSAOBlurTarget, write SSAOTarget)
        Graphics.SetRenderTarget(SSAOTarget);
        Graphics.Clear(Color.White);
        
        effect.SetValue("AOTex", SSAOBlurTarget);
        
        effect.CurrentTechnique = effect.Techniques["BlurV"];
    
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    
        // Pass 4: Composite (use blurred AO in SSAOTarget)
        Graphics.SetRenderTarget(OutputBuffer);
        Graphics.Clear(Color.Transparent);
        
        effect.SetValue("AOTex", SSAOTarget);
        
        // IMPORTANT: Sample color from the chained input (previous effect output or BackBuffer if first)
        effect.SetValue("ColorTex", ColorBuffer);
        
        effect.CurrentTechnique = effect.Techniques["Composite"];
    
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    }

    public override void DrawEnd()
    {
        // Unbind to avoid leaking state into other draws
        Graphics.SetVertexBuffer(null);
        Graphics.Indices = null;

        Graphics.SetRenderTarget(null);
    }

    public override void Dispose()
    {
        base.Dispose();
        SSAOTarget?.Dispose();
        SSAOBlurTarget?.Dispose();
        SSAOTarget = null;
        SSAOBlurTarget = null;
    }
}
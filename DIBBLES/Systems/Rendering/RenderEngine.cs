using DIBBLES.Effects;
using DIBBLES.Gameplay;
using DIBBLES.Scenes;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using static DIBBLES.Scenes.GameScene;

namespace DIBBLES.Systems.Rendering;

public class RenderEngine
{
    public static Skybox Sky { get; private set; } = new();
    
    public static RenderTarget2D BackBuffer;
    public static RenderTarget2D DepthBuffer;
    public static RenderTarget2D NormalBuffer;
    public static RenderTarget2D EmissiveBuffer;
    
    public static RenderTarget2D UIBuffer;

    public static bool PostProcessingEnabled = true;
    
    public static readonly SurfaceFormat BackBufferFormat = SurfaceFormat.HalfVector4;
    
    private bool backBuffersDebug = false;
    private GraphicsDevice graphics;
    
    public void DrawAll()
    {
        // Bind MRTs
        graphics.SetRenderTargets(
            new RenderTargetBinding(BackBuffer),
            new RenderTargetBinding(DepthBuffer),
            new RenderTargetBinding(NormalBuffer),
            new RenderTargetBinding(EmissiveBuffer)
        );
        
        // 1) Clear the depth-stencil actually used by the geometry pass (attached to the first RT)
        graphics.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1.0f, 0);

        // 2) Clear each color target individually
        graphics.SetRenderTarget(BackBuffer);
        graphics.Clear(Color.Black);

        graphics.SetRenderTarget(DepthBuffer);
        graphics.Clear(Color.White);         // far = 1.0 for the sampled depth texture

        graphics.SetRenderTarget(NormalBuffer);
        graphics.Clear(Color.Transparent);   // mark "no normal" with a=0
        
        graphics.SetRenderTarget(EmissiveBuffer);
        graphics.Clear(Color.Black);

        // 3) Render skybox
        graphics.SetRenderTargets(BackBuffer, EmissiveBuffer);
        Sky.Draw();
        
        // 4) Rebind MRTs for drawing, and draw world-space
        graphics.SetRenderTargets(BackBuffer, DepthBuffer, NormalBuffer, EmissiveBuffer);
        
        // 5) Draw opaque + cutout
        drawOpaque();
        
        // 6) Draw transparent
        drawTransparent();
        
        // 7) Draw UI
        if (UIEnabled)
            drawUI();
        
        graphics.SetRenderTarget(null);
        
        // 8) Draw post processing
        drawPostProcessing();
    }
    
    private void drawOpaque()
    {
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.Default;
        
        graphics.SamplerStates[0] = SamplerState.PointClamp; // Base atlas
        graphics.SamplerStates[1] = SamplerState.PointClamp; // Emissive atlas
        
        graphics.RasterizerState = RasterizerState.CullCounterClockwise;
        
        TerrainGen.DrawOpaque();
        TerrainGen.DrawBillboards();
        
        PlayerCharacter.Draw();
    }

    private void drawTransparent()
    {
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.Default;
        
        graphics.SamplerStates[0] = SamplerState.PointClamp; // Base atlas
        graphics.SamplerStates[1] = SamplerState.PointClamp; // Emissive atlas
        
        // Disable culling so billboards render double-sided
        graphics.RasterizerState = RasterizerState.CullNone;
    
        TerrainGen.DrawTransparent();
        
        // Draw block outline here: depth-tested against terrain, but NOT writing to Normal/Depth MRTs
        TerrainGeneration.Gameplay.Draw();
        Debug.Draw3D();
    }

    private void drawPostProcessing()
    {
        // Apply all registered post-processing effects, sampling color/normal/depth
        if (PostProcessingEnabled)
            postProcessingManager.ApplyAll(BackBuffer);
        
        UIBatch.Begin();
        
        // Draw buffers
        UIBatch.Draw(BackBuffer, Vector2.Zero, new Vector2(Engine.ScreenWidth, Engine.ScreenHeight), Color.White);
        
        // Composite all post-processing outputs
        if (PostProcessingEnabled)
            postProcessingManager.Draw();
        
        if (UIEnabled)
        {
            GameScene.UIBlur.Draw();
            UIBatch.Draw(UIBuffer, Vector2.Zero, new Vector2(Engine.ScreenWidth, Engine.ScreenHeight), Color.White);
        }

        if (backBuffersDebug)
        {
            var bufferWidth = Engine.ScreenWidth / 4.0f;
            var bufferHeight = Engine.ScreenHeight / 4.0f;
            //var testBuffer = postProcessingManager.ssaoPostProcess.SSAOBlurTarget;
            var testBuffer = postProcessingManager.bloom.BloomOutput;
            //var testBuffer = EmissiveBuffer;
            
            UIBatch.Draw(DepthBuffer, UI.TopRightPivot - new Vector2(bufferWidth, 0), 
                new Vector2(bufferWidth, bufferHeight), Color.White);
            
            UIBatch.Draw(NormalBuffer, UI.TopRightPivot - new Vector2(bufferWidth, -bufferHeight), 
                new Vector2(bufferWidth, bufferHeight), Color.White);
            
            UIBatch.Draw(testBuffer, UI.TopRightPivot - new Vector2(bufferWidth, -bufferHeight * 2.0f), 
                new Vector2(bufferWidth, bufferHeight), Color.White);
        }
        
        UIBatch.End();
    }

    private void drawUI()
    {
        graphics.BlendState = BlendState.NonPremultiplied;
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.RasterizerState = RasterizerState.CullCounterClockwise;
        
        graphics.SetRenderTarget(UIBuffer);
        graphics.Clear(Color.Transparent);
        
        // 3D UI
        TerrainGeneration.Gameplay.DrawPlane();
        
        UIBatch.Begin();
        
        PlayerCharacter.DrawUI();
        
        Inventory.Draw();
        
        GameChat.DrawBG();
        GameChat.Draw();
        
        Debug.Draw2D();
        Debug.Clear2D();
        
        _DebugMenu.Draw();
        
        UIBatch.End();
        
        GameScene.UIBlur.Apply(BackBuffer, UIBuffer);
    }
    
    public void Initialize()
    {
        graphics = Engine.Graphics;
        
        BackBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            BackBufferFormat,
            DepthFormat.Depth24,
            0,
            RenderTargetUsage.PreserveContents
        );
        
        DepthBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            SurfaceFormat.Single,   // 32-bit float
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
        
        NormalBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
        
        EmissiveBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            SurfaceFormat.HdrBlendable,   // emissive can exceed 1.0
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );

        UIBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents // safe for multi-pass UI composites
        );
        
        Sky.Initialize(
            Resource.Load<Texture2D>("Textures/Sun"),
            Resource.Load<Texture2D>("Textures/Moon"),
            Resource.Load<Effect>("Shaders/Skybox"));
        
        Commands.Register("bbd", "Toggle buffer debug to screen", toggleBBDCMD);
        Commands.Register("ao", "Toggle ambient occlusion", toggleAOCMD);
    }
    
    private void toggleBBDCMD(string[] args)
    {
        backBuffersDebug = !backBuffersDebug;
        Chat.Write($"Toggled back buffer debug: {backBuffersDebug}", ChatMessageType.Command);
    }

    private void toggleAOCMD(string[] args)
    {
        SSAOPostProcess.Enabled = !SSAOPostProcess.Enabled;
        Chat.Write($"Toggled ambient occlusion: {SSAOPostProcess.Enabled}", ChatMessageType.Command);
    }
}
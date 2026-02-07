using System.Diagnostics;
using DIBBLES.Effects;
using Microsoft.Xna.Framework;
using DIBBLES.Systems;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Inventory;
using DIBBLES.Gameplay.Player;
using DIBBLES.Terrain;
using DIBBLES.Terrain.Blocks;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Debug = DIBBLES.Utils.Debug;

namespace DIBBLES.Scenes;

public class GameScene : Scene
{
    public static TerrainGeneration TerrainGen = new();
    public static PlayerCharacter PlayerCharacter = new();
    public static InventorySystem Inventory = new();
    
    public static PostProcessingManager postProcessingManager = new();

    public static List<BlockLogic> BlockLogicList = new();

    public static Color SkyColor = new Color(0.08f, 0.14f, 0.2f, 1.0f);
    
    public static bool UIEnabled = true;
    
    // Buffers
    public static RenderTarget2D BackBuffer;
    public static RenderTarget2D DepthBuffer;
    public static RenderTarget2D NormalBuffer;
    
    public static RenderTarget2D UIBuffer;
    
    private bool backBuffersDebug = false;
    
    private Chat gameChat = new();
    private UIBlur uiBlur = new();
    
    public override void Start()
    {
        BackBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            SurfaceFormat.Color,
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
            DepthFormat.Depth24,
            0,
            RenderTargetUsage.PreserveContents
        );
        
        NormalBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24,
            0,
            RenderTargetUsage.PreserveContents
        );

        UIBuffer = new RenderTarget2D(
            Engine.Graphics,
            Engine.ScreenWidth,
            Engine.ScreenHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24,
            0,
            RenderTargetUsage.PreserveContents // safe for multi-pass UI composites
        );
        
        UIBatch.Initialize();
        Primatives3D.Initialize();
        
        TerrainGen.Start(); // Initial terrain generation
        Inventory.Start();
        PlayerCharacter.Start(); // Must be started after terrain
        gameChat.Start();
        
        uiBlur.Start();

        // Initialize all post processing effects before PostProcessingManager.Initialize!
        postProcessingManager.Initialize(Engine.ScreenWidth, Engine.ScreenHeight);
        
        Commands.Register("help", "Lists all available commands", Chat.WriteHelp);
        Commands.Register("db", "Toggle debug information", Debug.ToggleDebugCMD);
        Commands.Register("dbc", "Toggle chunk border debug", Debug.ToggleChunkDebugCMD);
        Commands.Register("dbl", "Toggle light level debug", Debug.ToggleLightDebugCMD);
        Commands.Register("bbd", "Toggle buffer debug to screen", toggleBBDCMD);
        Commands.Register("ao", "Toggle ambient occlusion", toggleAOCMD);
    }

    private int fpsCounter;
    private float fpsElapsed;
    public override void Update()
    {
        Input.Update();
        
        fpsElapsed += Time.DeltaTime;
        
        if (fpsElapsed >= 0.5f)
        {
            fpsCounter = (int)(1f / Time.DeltaTime);
            fpsElapsed -= 0.5f;
        }
        
        Debug.Draw2DText($"FPS: {fpsCounter}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGeneration.Seed}", Color.White);
        
        PlayerCharacter.Update();
        
        Inventory.Update();
        
        TerrainGen.Update(PlayerCharacter);
        TerrainGeneration.Gameplay.Update(PlayerCharacter.Camera);
        
        gameChat.Update();
        
        if (!Chat.IsOpen && Input.IsKeyPressed(Keys.L))
            WorldSave.SaveWorldData("test");
        
        Debug.Update(PlayerCharacter.Camera); // Must run after everything
    }

    public override void Draw()
    {
        var graphics = Engine.Graphics;
        
        // Bind MRTs
        graphics.SetRenderTargets(
            new RenderTargetBinding(BackBuffer),
            new RenderTargetBinding(DepthBuffer),
            new RenderTargetBinding(NormalBuffer)
        );

        // 1) Clear the depth-stencil actually used by the geometry pass (attached to the first RT)
        graphics.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1.0f, 0);

        // 2) Clear each color target individually
        graphics.SetRenderTarget(BackBuffer);
        graphics.Clear(SkyColor);

        graphics.SetRenderTarget(DepthBuffer);
        graphics.Clear(Color.White);         // far = 1.0 for the sampled depth texture

        graphics.SetRenderTarget(NormalBuffer);
        graphics.Clear(Color.Transparent);   // mark "no normal" with a=0

        // 3) Rebind MRTs for drawing, and draw world-space
        graphics.SetRenderTargets(BackBuffer, DepthBuffer, NormalBuffer);
        
        TerrainGen.Draw();

        //var terrainProgress = TerrainGen.VisualLoadProgress * 100f;
        
        //if (terrainProgress < 100f)
            //Debug.Info($"Visual progress: {terrainProgress}% - Terrain draw time (in ticks): {timer.ElapsedTicks}");
        
        PlayerCharacter.Draw();
        
        // 4) Switch to single target with the same depth-stencil to preserve terrain depth
        graphics.SetRenderTarget(BackBuffer);
        
        graphics.BlendState = BlendState.NonPremultiplied;
        graphics.DepthStencilState = DepthStencilState.DepthRead;
        graphics.RasterizerState = RasterizerState.CullNone;
    
        // Draw block overlays here: depth-tested against terrain, but NOT writing to Normal/Depth MRTs
        TerrainGeneration.Gameplay.Draw();
        Debug.Draw3D();
    
        // 5) Restore default depth state for subsequent passes
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.RasterizerState = RasterizerState.CullCounterClockwise;

        // Restore to screen/backbuffer for UI and post
        graphics.SetRenderTarget(null);
        
        // 6) Draw UI (UI Batch)
        if (UIEnabled)
        {
            graphics.SetRenderTarget(UIBuffer);
            graphics.Clear(Color.Transparent);
        
            UIBatch.Begin();
        
            PlayerCharacter.DrawUI();
        
            Inventory.Draw();
        
            gameChat.DrawBG();
            gameChat.Draw();
        
            Debug.Draw2D();
            Debug.Clear2D();
        
            UIBatch.End();
        
            graphics.SetRenderTarget(null);
        
            uiBlur.Apply(BackBuffer, UIBuffer);
        }
        
        // 7) Apply all registered post-processing effects, sampling color/normal/depth
        //Debug.TimerStart("Post processing");
        postProcessingManager.ApplyAll(BackBuffer);
        
        UIBatch.Begin();
        
        // 8) Draw buffers
        UIBatch.Draw(BackBuffer, Vector2.Zero, new Vector2(Engine.ScreenWidth, Engine.ScreenHeight), Color.White);
        
        // Composite all post-processing outputs
        postProcessingManager.Draw();
        
        if (UIEnabled)
        {
            uiBlur.Draw();
            UIBatch.Draw(UIBuffer, Vector2.Zero, new Vector2(Engine.ScreenWidth, Engine.ScreenHeight), Color.White);
        }

        if (backBuffersDebug)
        {
            var bufferWidth = Engine.ScreenWidth / 4.0f;
            var bufferHeight = Engine.ScreenHeight / 4.0f;
            //var aoBuffer = postProcessingManager.ssaoPostProcess.SSAOBlurTarget;
            //var tesBuffer = postProcessingManager.bloom.BloomOutput;
            
            UIBatch.Draw(DepthBuffer, UI.TopRightPivot - new Vector2(bufferWidth, 0), 
                new Vector2(bufferWidth, bufferHeight), Color.White);
            
            UIBatch.Draw(NormalBuffer, UI.TopRightPivot - new Vector2(bufferWidth, -bufferHeight), 
                new Vector2(bufferWidth, bufferHeight), Color.White);
            
            //UIBatch.Draw(tesBuffer, UI.TopRightPivot - new Vector2(bufferWidth, -bufferHeight * 2.0f), 
            //    new Vector2(bufferWidth, bufferHeight), Color.White);
        }
        
        UIBatch.End();
        
        //Debug.TimerStop();
        
        // Toggle UI
        if (Input.IsKeyPressed(Keys.F1))
            UIEnabled = !UIEnabled;
        
        // Take screenshot after full scene composite, but before UI
        if (Input.IsKeyPressed(Keys.F2))
            takeScreenshot(graphics);
    }
    
    private void toggleBBDCMD(string[] args)
    {
        backBuffersDebug = !backBuffersDebug;
        Chat.Write($"Toggled back buffer debug: {backBuffersDebug}", ChatMessageType.Command);
    }

    private void toggleAOCMD(string[] args)
    {
        SSAOPostProcess.AOEnabled = !SSAOPostProcess.AOEnabled;
        Chat.Write($"Toggled ambient occlusion: {SSAOPostProcess.AOEnabled}", ChatMessageType.Command);
    }
    
    private void takeScreenshot(GraphicsDevice graphicsDevice)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string folder = $"Screenshot-{timestamp}";

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        // Capture final on-screen output (backbuffer) including UI and post-processing
        int width = Engine.ScreenWidth;
        int height = Engine.ScreenHeight;

        Color[] pixels = new Color[width * height];
        graphicsDevice.GetBackBufferData(pixels);

        using (var outputTex = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color))
        {
            outputTex.SetData(pixels);

            using (var outputStream = new FileStream(Path.Combine(folder, "Output.png"), FileMode.Create))
                outputTex.SaveAsPng(outputStream, width, height);
        }
        
        // Save color buffer
        using (var colorStream = new FileStream(Path.Combine(folder, "Color.png"), FileMode.Create))
            BackBuffer.SaveAsPng(colorStream, BackBuffer.Width, BackBuffer.Height);
        
        // Save depth buffer
        using (var colorStream = new FileStream(Path.Combine(folder, "Depth.png"), FileMode.Create))
            DepthBuffer.SaveAsPng(colorStream, BackBuffer.Width, BackBuffer.Height);
        
        // Save normal buffer
        using (var colorStream = new FileStream(Path.Combine(folder, "Normal.png"), FileMode.Create))
            NormalBuffer.SaveAsPng(colorStream, BackBuffer.Width, BackBuffer.Height);
        
        // Save Ambient Occlusion buffer
        /*using (var colorStream = new FileStream(Path.Combine(folder, "AO.png"), FileMode.Create))
        {
            var aoBuffer = postProcessingManager.ssaoPostProcess.SSAOBlurTarget;
            aoBuffer.SaveAsPng(colorStream, BackBuffer.Width, BackBuffer.Height);
        }*/

        var outputString = $"Saved buffers to: {folder}";
        
        Debug.Info(outputString);
        Chat.Write(outputString, ChatMessageType.Command);
    }
}
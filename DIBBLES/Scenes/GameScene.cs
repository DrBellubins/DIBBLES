using System.Diagnostics;
using DIBBLES.Effects;
using Microsoft.Xna.Framework;
using DIBBLES.Systems;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Inventory;
using DIBBLES.Gameplay.Player;
using DIBBLES.Systems.DebugMenu;
using DIBBLES.Terrain;
using DIBBLES.Terrain.Blocks;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.ImGuiNet;

using Debug = DIBBLES.Utils.Debug;

namespace DIBBLES.Scenes;

public class GameScene : Scene
{
    public static RenderEngine Renderer = new();
    public static TerrainGeneration TerrainGen = new();
    public static PlayerCharacter PlayerCharacter = new();
    public static InventorySystem Inventory = new();
    public static DayNightCycle DayNightCycle = new();
    
    public static PostProcessingManager postProcessingManager = new();

    public static List<BlockLogic> BlockLogicList = new();
    
    public static bool UIEnabled = true;
    
    public static Chat GameChat = new();
    public static UIBlur UIBlur = new();
    
    // Debug menu
    public static DebugMenu _DebugMenu = new();
    public static ImGuiRenderer ImguiRenderer;
    
    public override void Start()
    {
        Renderer.Initialize();
        
        // IMGUI
        ImguiRenderer = new ImGuiRenderer(Engine.Instance);
        ImguiRenderer.RebuildFontAtlas();

        // Provide the texture binder so TextureDisplayParam can draw images
        DebugMenu.SetBindTextureFunc(tex =>
        {
            return ImguiRenderer.BindTexture(tex);
        });
        
        UIBatch.Initialize();
        Primatives3D.Initialize();
        
        DayNightCycle.Start();
        TerrainGen.Start(); // Initial terrain generation
        Inventory.Start();
        PlayerCharacter.Start(); // Must be started after terrain
        GameChat.Start();
        
        UIBlur.Start();

        // Initialize all post processing effects before PostProcessingManager.Initialize!
        postProcessingManager.Initialize(Engine.ScreenWidth, Engine.ScreenHeight);
        
        _DebugMenu.Start();
        
        Commands.Register("help", "Lists all available commands", Chat.WriteHelp);
        Commands.Register("db", "Toggle debug information", Debug.ToggleDebugCMD);
        Commands.Register("dbc", "Toggle chunk border debug", Debug.ToggleChunkDebugCMD);
        Commands.Register("dbl", "Toggle light level debug", Debug.ToggleLightDebugCMD);
        Commands.Register("atlas", "Save atlases to png", BlockData.SaveAtlasesCMD);
    }

    private int fpsCounter;
    private float fpsElapsed;
    public override void Update()
    {
        Input.Update();
        
        if (!Chat.IsOpenNotFocused && Input.Quit())
            Engine.Instance.Exit();
        
        fpsElapsed += Time.DeltaTime;
        
        if (fpsElapsed >= 0.5f)
        {
            fpsCounter = (int)(1f / Time.DeltaTime);
            fpsElapsed -= 0.5f;
        }
        
        Debug.Draw2DText($"FPS: {fpsCounter}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGeneration.Seed}", Color.White);
        Debug.Draw2DText($"Time: {DayNightCycle.TimeOfDay}");
        
        DayNightCycle.Update();
        
        PlayerCharacter.Update();
        
        Inventory.Update();
        
        TerrainGen.Update(PlayerCharacter);
        TerrainGeneration.Gameplay.Update(PlayerCharacter.Camera);
        
        GameChat.Update();
        
        if (!Chat.IsOpenNotFocused && Input.IsKeyPressed(Keys.L))
            WorldSave.SaveWorldData("test");
        
        _DebugMenu.Update();
        Debug.Update(PlayerCharacter.Camera); // Must run after everything
    }

    public override void Draw()
    {
        var graphics = Engine.Graphics;
        
        Renderer.DrawAll();
        
        // Draw IMGUI
        ImguiRenderer.BeginLayout(Engine.MonoGameTime);

        // Draw the IMGUI panel for the active type
        _DebugMenu.DrawIMGUI();

        ImguiRenderer.EndLayout();
        
        // Toggle UI
        if (Input.IsKeyPressed(Keys.F1))
            UIEnabled = !UIEnabled;
        
        // Take screenshot after full scene composite, but before UI
        if (Input.IsKeyPressed(Keys.F2))
            takeScreenshot(graphics);
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
        
        // Save HDR/float RTs by converting to LDR Color first
        SaveRenderTargetAsPng(RenderEngine.BackBuffer, Path.Combine(folder, "Color.png"));       // HdrBlendable
        SaveRenderTargetAsPng(RenderEngine.DepthBuffer, Path.Combine(folder, "Depth.png"));      // Single
        SaveRenderTargetAsPng(RenderEngine.NormalBuffer, Path.Combine(folder, "Normal.png"));    // Color
        
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
    
    private void SaveRenderTargetAsPng(RenderTarget2D rt, string path)
    {
        try
        {
            if (rt == null)
                return;
    
            // Direct save for LDR Color
            if (rt.Format == SurfaceFormat.Color)
            {
                using (var s = new FileStream(path, FileMode.Create))
                    rt.SaveAsPng(s, rt.Width, rt.Height);
                return;
            }
    
            // GPU resolve to a temporary LDR Color RT (safe for HdrBlendable/HalfVector4/Single)
            using (var temp = new RenderTarget2D(Engine.Graphics, rt.Width, rt.Height, false, SurfaceFormat.Color, DepthFormat.None))
            {
                var gd = Engine.Graphics;
                var prevTargets = gd.GetRenderTargets();
    
                gd.SetRenderTarget(temp);
                gd.Clear(Color.Transparent);
    
                UIBatch.Begin();
                // Draw the source as-is; sampling writes clamped [0..1] into Color
                UIBatch.Draw(rt, Vector2.Zero, new Vector2(temp.Width, temp.Height), Color.White);
                UIBatch.End();
    
                gd.SetRenderTargets(prevTargets);
    
                using (var s = new FileStream(path, FileMode.Create))
                    temp.SaveAsPng(s, temp.Width, temp.Height);
            }
        }
        catch
        {
            // Fallback only for Single (depth) if GPU resolve fails on some platforms
            try
            {
                if (rt.Format == SurfaceFormat.Single)
                {
                    var data = new float[rt.Width * rt.Height];
                    rt.GetData(data);
    
                    var pixels = new Color[data.Length];
                    for (int i = 0; i < data.Length; i++)
                    {
                        float c = MathF.Min(1f, MathF.Max(0f, data[i]));
                        byte v = (byte)(c * 255f + 0.5f);
                        pixels[i] = new Color(v, v, v, (byte)255);
                    }
    
                    using (var tex = new Texture2D(Engine.Graphics, rt.Width, rt.Height, false, SurfaceFormat.Color))
                    {
                        tex.SetData(pixels);
                        using (var s = new FileStream(path, FileMode.Create))
                            tex.SaveAsPng(s, rt.Width, rt.Height);
                    }
                    return;
                }
            }
            catch (Exception ex2)
            {
                Debug.Error($"Failed to save '{path}' (fallback): {ex2.Message}");
                return;
            }
    
            Debug.Error($"Failed to save '{path}': Value does not fall within the expected range.");
        }
    }
}
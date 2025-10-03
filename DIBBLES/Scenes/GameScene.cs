using Microsoft.Xna.Framework;
using DIBBLES.Systems;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Terrain;
using DIBBLES.Terrain.Blocks;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Scenes;

public class GameScene : Scene
{
    //public static TerrainGeneration TerrainGen = new();
    public static TerrainGeneration TerrainGen = new();
    public static PlayerCharacter PlayerCharacter = new();

    public static List<BlockLogic> BlockLogicList = new();

    public static Color SkyColor = new Color(0.4f, 0.7f, 1.0f, 1.0f);
    public static RenderTarget2D UIBuffer;
    
    private Chat gameChat = new();
    
    public override void Start()
    {
        UIBuffer = new RenderTarget2D(Engine.Graphics, Engine.ScreenWidth, Engine.ScreenHeight);
        
        UIBatch.Initialize();
        Primatives3D.Initialize();

        var skyColorVec = SkyColor.ToVector3();

        skyColorVec *= 0.2f;
        
        SkyColor = Color.FromNonPremultiplied(new Vector4(skyColorVec.X, skyColorVec.Y, skyColorVec.Z, 1.0f));
        
        TerrainGen.Start(); // Initial terrain generation
        PlayerCharacter.Start(); // Must be started after terrain
        gameChat.Start();
        
        Commands.RegisterCommand("help", "Lists all available commands", Chat.WriteHelp);
        Commands.RegisterCommand("debug", "Toggle debug information", Debug.ToggleDebug);
        Commands.RegisterCommand("debugEx", "Toggle extended debug information", Debug.ToggleDebugExtended);
    }

    public override void Update()
    {
        Input.Update();
        
        Debug.Draw2DText($"FPS: {1f / Time.DeltaTime}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGeneration.Seed}", Color.White);
        
        PlayerCharacter.Update();
        
        TerrainGen.Update(PlayerCharacter);
        TerrainGeneration.Gameplay.Update(PlayerCharacter.Camera);
        
        gameChat.Update();
        
        if (!Chat.IsOpen && Input.IsKeyPressed(Keys.L))
            WorldSave.SaveWorldData("test");
        
        //if (Raylib.IsKeyPressed(KeyboardKey.F2))
        //    Raylib.TakeScreenshot($"Screeenshot-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
        
        Debug.Update(PlayerCharacter.Camera); // Must run after everything
    }

    public override void Draw()
    {
        var gd = Engine.Graphics;
        
        gd.Clear(SkyColor);
        
        gd.BlendState = BlendState.NonPremultiplied;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullCounterClockwise;
        gd.SamplerStates[0] = SamplerState.PointClamp;
        
        TerrainGen.Draw();
        TerrainGeneration.Gameplay.Draw();
        
        PlayerCharacter.Draw();
        
        if (Input.IsKeyPressed(Keys.F2))
            takeScreenshot(gd);
        
        //Debug.Draw3D();
        
        // Draw UI (UI Batch)
        gd.SetRenderTarget(UIBuffer);
        gd.Clear(new Color());
        
        UIBatch.Begin();
        
        PlayerCharacter.DrawUI();
        
        gameChat.DrawBG();
        gameChat.Draw();
        
        Debug.Draw2D();
        Debug.Clear2D();
        
        UIBatch.End();
        
        gd.SetRenderTarget(null);
    }
    
    private void takeScreenshot(GraphicsDevice gd)
    {
        // Create a texture to store the backbuffer
        var width = gd.PresentationParameters.BackBufferWidth;
        var height = gd.PresentationParameters.BackBufferHeight;
        var screenshot = new Texture2D(gd, width, height, false, SurfaceFormat.Color);

        // Copy backbuffer data
        int[] pixelData = new int[width * height];
        gd.GetBackBufferData(pixelData);
        screenshot.SetData(pixelData);

        // Save to PNG
        string path = $"Screenshot-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.png";
        
        using (var fs = new FileStream(path, FileMode.Create))
            screenshot.SaveAsPng(fs, width, height);

        screenshot.Dispose();

        var outputString = $"Saved screenshot: {path}";
        Console.WriteLine(outputString);
        Chat.Write(outputString, ChatMessageType.Command);
    }
}
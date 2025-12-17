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

namespace DIBBLES.Scenes;

public class GameScene : Scene
{
    public static TerrainGeneration TerrainGen = new();
    public static PlayerCharacter PlayerCharacter = new();
    public static InventorySystem Inventory = new();

    public static List<BlockLogic> BlockLogicList = new();

    public static Color SkyColor = new Color(0.4f, 0.7f, 1.0f, 1.0f);
    
    // Buffers
    public static RenderTarget2D BackBuffer;
    public static RenderTarget2D UIBuffer;
    
    private Chat gameChat = new();
    private UIBlur uiBlur = new();
    
    public override void Start()
    {
        BackBuffer = new RenderTarget2D(Engine.Graphics, Engine.ScreenWidth, Engine.ScreenHeight, false, 
            SurfaceFormat.Color, DepthFormat.Depth24);
        
        UIBuffer = new RenderTarget2D(Engine.Graphics, Engine.ScreenWidth, Engine.ScreenHeight);
        
        UIBatch.Initialize();
        Primatives3D.Initialize();

        var skyColorVec = SkyColor.ToVector3();

        skyColorVec *= 0.2f;
        
        SkyColor = Color.FromNonPremultiplied(new Vector4(skyColorVec.X, skyColorVec.Y, skyColorVec.Z, 1.0f));
        
        TerrainGen.Start(); // Initial terrain generation
        TerrainGeneration.Gameplay.Start();
        Inventory.Start();
        PlayerCharacter.Start(); // Must be started after terrain
        gameChat.Start();
        
        uiBlur.Start();
        
        Commands.RegisterCommand("help", "Lists all available commands", Chat.WriteHelp);
        Commands.RegisterCommand("db", "Toggle debug information", Debug.ToggleDebug);
        Commands.RegisterCommand("dbc", "Toggle chunk border debug", Debug.ToggleChunkDebug);
        Commands.RegisterCommand("dbl", "Toggle light level debug", Debug.ToggleLightDebug);
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
        
        graphics.SetRenderTarget(BackBuffer);
        graphics.Clear(SkyColor);
        
        graphics.BlendState = BlendState.NonPremultiplied;
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.RasterizerState = RasterizerState.CullCounterClockwise;
        graphics.SamplerStates[0] = SamplerState.PointClamp;
        
        TerrainGen.Draw();
        TerrainGeneration.Gameplay.Draw();
        
        PlayerCharacter.Draw();
        
        Debug.Draw3D();
        
        if (Input.IsKeyPressed(Keys.F2))
            takeScreenshot(graphics);
        
        // Draw UI (UI Batch)
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
        
        TerrainGeneration.Gameplay.Apply();
        
        graphics.SetRenderTarget(null);
        
        uiBlur.Apply(BackBuffer, UIBuffer);
        
        UIBatch.Begin();
        
        // Draw buffers
        UIBatch.Draw(BackBuffer, Vector2.Zero, new Vector2(Engine.ScreenWidth, Engine.ScreenHeight), Color.White);
        uiBlur.Draw();
        UIBatch.Draw(UIBuffer, Vector2.Zero, new Vector2(Engine.ScreenWidth, Engine.ScreenHeight), Color.White);
        
        UIBatch.End();
    }
    
    private void takeScreenshot(GraphicsDevice graphicsDevice)
    {
        int width = graphicsDevice.PresentationParameters.BackBufferWidth;
        int height = graphicsDevice.PresentationParameters.BackBufferHeight;

        // Read backbuffer
        int[] pixelData = new int[width * height];
        graphicsDevice.GetBackBufferData(pixelData);

        // Flip vertically: swap rows in-place
        int rowStride = width;
        
        for (int y = 0; y < height / 2; y++)
        {
            int topIdx = y * rowStride;
            int bottomIdx = (height - 1 - y) * rowStride;

            for (int x = 0; x < rowStride; x++)
                (pixelData[topIdx + x], pixelData[bottomIdx + x]) = (pixelData[bottomIdx + x], pixelData[topIdx + x]);
        }

        // Create texture and write flipped data
        using var screenshot = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        screenshot.SetData(pixelData);

        string path = $"Screenshot-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.png";
        
        using (var fileStream = new FileStream(path, FileMode.Create))
            screenshot.SaveAsPng(fileStream, width, height);

        var outputString = $"Saved screenshot: {path}";
        Console.WriteLine(outputString);
        Chat.Write(outputString, ChatMessageType.Command);
    }
}
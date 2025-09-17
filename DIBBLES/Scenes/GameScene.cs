using System.Numerics;
using Raylib_cs;
using DIBBLES.Systems;
using DIBBLES.Effects;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Terrain;
using DIBBLES.Terrain.Blocks;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class GameScene : Scene
{
    public static TerrainGeneration TerrainGen = new();
    public static PlayerCharacter PlayerCharacter = new();

    public static List<BlockLogic> BlockLogicList = new();

    private Chat gameChat = new();
    
    private RenderTexture2D backBuffer;
    private RenderTexture2D UIBuffer;
    private UIBlur uiBlur = new();
    
    public override void Start()
    {
        // Must be set for proper depth buffers
        Rlgl.SetClipPlanes(0.01f, 1000f);
        
        // UI Blur setup
        backBuffer = Raylib.LoadRenderTexture(Engine.ScreenWidth, Engine.ScreenHeight);
        Raylib.SetTextureWrap(backBuffer.Texture, TextureWrap.MirrorClamp);
        Raylib.SetTextureFilter(backBuffer.Texture, TextureFilter.Point);
        
        UIBuffer = Raylib.LoadRenderTexture(Engine.ScreenWidth, Engine.ScreenHeight);
        UIBuffer.Texture.Format = PixelFormat.UncompressedR8G8B8A8;
        
        uiBlur.Start(backBuffer, UIBuffer);
        
        // Initial terrain generation
        TerrainGen.Start();
        TerrainGen.Update(PlayerCharacter);
        
        PlayerCharacter.Start(); // Must be started after terrain
        
        gameChat.Start();
        
        Commands.RegisterCommand("help", "Lists all available commands.", Chat.WriteHelp);
        Commands.RegisterCommand("debug", "Toggle debug information", Debug.ToggleDebug);
        Commands.RegisterCommand("debugEx", "Toggle extended debug information", Debug.ToggleDebugExtended);
    }

    public override void Update()
    {
        PlayerCharacter.Update();
        
        TerrainGen.Update(PlayerCharacter);
        TerrainGeneration.Gameplay.Update(PlayerCharacter.Camera);
        
        gameChat.Update();
        
        if (Raylib.IsKeyPressed(KeyboardKey.L))
            WorldSave.SaveWorldData("test");
        
        if (Raylib.IsKeyPressed(KeyboardKey.F2))
            Raylib.TakeScreenshot($"Screeenshot-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
        
        CursorManager.Update(); // Must happen after everything for MouseDelta to work.
        
        Debug.Update(PlayerCharacter.Camera); // Must run after everything
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        
        Raylib.BeginTextureMode(backBuffer);
        Raylib.ClearBackground(Color.SkyBlue);
        Raylib.BeginMode3D(PlayerCharacter.Camera);
        
        TerrainGen.Draw();
        PlayerCharacter.Draw();
        
        Debug.Draw3D();
        
        Raylib.EndMode3D();
        Raylib.EndTextureMode();
        
        Raylib.BeginTextureMode(UIBuffer);
        Raylib.ClearBackground(new Color(0, 0, 0, 0));
        
        PlayerCharacter.DrawUI();
        gameChat.Draw();
        
        Raylib.DrawTextureRec(
            gameChat.ChatTexture.Texture,
            new Rectangle(0, 0, gameChat.ChatTexture.Texture.Width, -gameChat.ChatTexture.Texture.Height),
            new Vector2(0f, gameChat.heightPos), // or the y position you want
            Color.White
        );
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);

        Debug.Draw2DText($"FPS: {1f / Time.DeltaTime}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGen.Seed}", Color.White);
        
        Debug.Draw2D();
        
        Raylib.EndTextureMode();
        
        uiBlur.Draw();
        
        // Render buffers
        Raylib.DrawTextureRec(backBuffer.Texture,
            new Rectangle(0f, 0f, backBuffer.Texture.Width, -backBuffer.Texture.Height),
            Vector2.Zero, Color.White);
        
        Raylib.DrawTexture(uiBlur.BlurMaskBuffer.Texture, 0, 0, Color.White);
        
        Raylib.DrawTextureRec(UIBuffer.Texture,
            new Rectangle(0f, 0f, UIBuffer.Texture.Width, -UIBuffer.Texture.Height),
            Vector2.Zero, Color.White);
        
        Raylib.EndDrawing();
    }
}
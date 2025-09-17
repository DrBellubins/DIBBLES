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
    
    public override void Start()
    {
        // Must be set for proper depth buffers
        Rlgl.SetClipPlanes(0.01f, 1000f);
        
        // Initial terrain generation
        TerrainGen.Start();
        TerrainGen.Update(PlayerCharacter);
        
        PlayerCharacter.Start(); // Must be started after terrain
        
        gameChat.Start();
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
        
        Debug.Update(PlayerCharacter.Camera); // Must run after everything
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        
        Raylib.ClearBackground(Color.SkyBlue);
        Raylib.BeginMode3D(PlayerCharacter.Camera);
        
        TerrainGen.Draw();
        PlayerCharacter.Draw();
        
        Debug.Draw3D();
        
        Raylib.EndMode3D();
        
        PlayerCharacter.DrawUI();
        
        gameChat.Draw();
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);

        Debug.Draw2DText($"FPS: {1f / Time.DeltaTime}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGen.Seed}", Color.White);
        
        Debug.Draw2D();
        
        Raylib.EndDrawing();
    }
}
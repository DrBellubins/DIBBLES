using Microsoft.Xna.Framework;
using DIBBLES.Systems;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Terrain;
using DIBBLES.Terrain.Blocks;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class GameSceneMono : Scene
{
    public static TerrainGeneration TerrainGen = new();
    public static PlayerCharacter PlayerCharacter = new();

    public static List<BlockLogic> BlockLogicList = new();

    private Chat gameChat = new();
    
    public override void Start()
    {
        // Initial terrain generation
        TerrainGen.Start();
        TerrainGen.Update(PlayerCharacter);
        
        PlayerCharacter.Start(); // Must be started after terrain
        
        gameChat.Start();
        
        Commands.RegisterCommand("help", "Lists all available commands", args => Chat.WriteHelp());
        Commands.RegisterCommand("debug", "Toggle debug information", args => Debug.ToggleDebug());
        Commands.RegisterCommand("debugEx", "Toggle extended debug information", args => Debug.ToggleDebugExtended());
    }

    public override void Update()
    {
        InputMono.Update();
        
        PlayerCharacter.Update();
        
        TerrainGen.Update(PlayerCharacter);
        TerrainGeneration.Gameplay.Update(PlayerCharacter.Camera);
        
        gameChat.Update();
        
        //if (!Chat.IsOpen && Raylib.IsKeyPressed(KeyboardKey.L))
        //    WorldSave.SaveWorldData("test");
        
        //if (Raylib.IsKeyPressed(KeyboardKey.F2))
        //    Raylib.TakeScreenshot($"Screeenshot-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
        
        Debug.Update(PlayerCharacter.Camera); // Must run after everything
    }

    public override void Draw()
    {
        var gd = MonoEngine.Graphics;
        var sprites = MonoEngine.Sprites;
        var font = MonoEngine.MainFont;
        
        gd.Clear(Color.Black);
        
        TerrainGen.Draw();
        //PlayerCharacter.Draw();
        
        //Debug.Draw3D();
        
        // Draw UI
        PlayerCharacter.DrawUI();
        
        /*if (Chat.IsOpen || Chat.IsClosedButShown)
            gameChat.DrawBG();
        
        // TODO: Monogame
        //Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);

        Debug.Draw2DText($"FPS: {1f / Time.DeltaTime}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGen.Seed}", Color.White);
        
        Debug.Draw2D();
        
        gameChat.Draw();*/
    }
}
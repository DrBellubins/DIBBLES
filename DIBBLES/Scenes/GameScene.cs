using Raylib_cs;
using DIBBLES.Systems;
using DIBBLES.Effects;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Terrain;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class GameScene : Scene
{
    public static TerrainGeneration TerrainGen = new TerrainGeneration();
    public static TerrainMesh TMesh = new TerrainMesh();
    public static TerrainLighting Lighting = new TerrainLighting();
    public static TerrainGameplay Gameplay = new TerrainGameplay();
    
    public static PlayerCharacter PlayerCharacter = new PlayerCharacter();
    
    private FogEffect fogEffect = new FogEffect();

    public override void Start()
    {
        // Must be set for proper depth buffers
        Rlgl.SetClipPlanes(0.01f, 1000f);
        
        // Initial terrain generation
        TerrainGen.Start();
        TerrainGen.Update(PlayerCharacter);
        
        PlayerCharacter.Start(); // Must be started after terrain
        
        fogEffect.Start();
    }

    public override void Update()
    {
        PlayerCharacter.Update();
        
        TerrainGen.Update(PlayerCharacter);
        Gameplay.Update(PlayerCharacter.Camera);

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
        
        fogEffect.DrawStart();
        Raylib.ClearBackground(Color.SkyBlue);
        Raylib.BeginMode3D(PlayerCharacter.Camera);
        
        TerrainGen.Draw();
        PlayerCharacter.Draw();
        
        Debug.Draw3D();
        
        Raylib.EndMode3D();
        
        fogEffect.DrawEnd();
        
        PlayerCharacter.DrawUI();
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);

        Debug.Draw2DText($"FPS: {1f / Time.DeltaTime}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGen.Seed}", Color.White);
        
        Debug.Draw2D();
        
        Raylib.EndDrawing();
    }
}
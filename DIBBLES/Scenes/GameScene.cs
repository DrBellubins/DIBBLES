using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Effects;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class GameScene : Scene
{
    private TerrainGeneration terrainGen = new TerrainGeneration();
    
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
        terrainGen.Start();
        terrainGen.Update(PlayerCharacter);
        
        PlayerCharacter.Start(); // Must be started after terrain
        
        fogEffect.Start();
    }

    public override void Update()
    {
        PlayerCharacter.Update();
        
        terrainGen.Update(PlayerCharacter);
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
        Raylib.ClearBackground(Color.Black);
        
        Raylib.BeginMode3D(PlayerCharacter.Camera);
        
        terrainGen.Draw();
        
        PlayerCharacter.Draw();
        
        Raylib.EndMode3D();
        
        fogEffect.DrawEnd();
        
        PlayerCharacter.DrawUI();
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);

        Debug.Draw2DText($"FPS: {1f / Time.DeltaTime}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGeneration.Seed}", Color.White);
        
        Debug.Draw2D();
        
        Raylib.EndDrawing();
    }
}
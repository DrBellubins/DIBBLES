using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Effects;
using DIBBLES.Gameplay.Player;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class VoxelTerrainScene : Scene
{
    private TerrainGeneration terrainGen = new TerrainGeneration();
    
    public static Player Player = new Player();
    
    private FogEffect fogEffect = new FogEffect();

    public override void Start()
    {
        // Must be set for proper depth buffers
        Rlgl.SetClipPlanes(0.01f, 1000f);
        
        // Initial terrain generation
        terrainGen.Start();
        terrainGen.Update(Player);
        
        Player.Start(); // Must be started after terrain
        
        fogEffect.Start();
    }

    public override void Update()
    {
        Player.Update();
        
        terrainGen.Update(Player);
        TerrainGeneration.Gameplay.Update(Player.Camera);

        if (Raylib.IsKeyPressed(KeyboardKey.L))
            WorldSave.SaveWorldData("test");
        
        Debug.Update(Player.Camera); // Must run after everything
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        
        //fogEffect.DrawStart();
        //Raylib.ClearBackground(Color.SkyBlue);
        
        Raylib.BeginMode3D(Player.Camera);
        
        terrainGen.Draw();
        
        Player.Draw();
        
        Raylib.EndMode3D();
        
        Player.DrawUI();
        
        //fogEffect.DrawEnd();
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);

        Debug.Draw2DText($"FPS: {1f / Time.DeltaTime}", Color.White);
        Debug.Draw2DText($"Seed: {TerrainGeneration.Seed}", Color.White);
        
        Debug.Draw2D();
        
        Raylib.EndDrawing();
    }
}
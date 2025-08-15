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
    //public static Freecam Freecam = new Freecam();
    
    private FogEffect fogEffect = new FogEffect();

    public override void Start()
    {
        // Must be set for proper depth buffers
        Rlgl.SetClipPlanes(0.01f, 1000f);
        
        Raylib.DisableCursor();
        
        Player.Start();
        //Freecam.Start();
        
        // Initial terrain generation
        terrainGen.Start();
        terrainGen.Update(Player);
        
        fogEffect.Start();
        
        WorldSave.Initialize();
    }

    public override void Update()
    {
        Player.Update();
        //Freecam.Update();
        
        terrainGen.Update(Player);
        TerrainGeneration.Gameplay.Update(Player.Camera);
        
        // Temporary world saving/loading
        if (Raylib.IsKeyDown(KeyboardKey.O))
            WorldSave.LoadWorldData("test");

        if (Raylib.IsKeyDown(KeyboardKey.L))
            WorldSave.SaveWorldData("test");
        
        // --- Block breaking and placing ---
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            TerrainGeneration.Gameplay.BreakBlock();
        
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
            TerrainGeneration.Gameplay.PlaceBlock(BlockType.Snow);
        
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
        //Freecam.Draw();
        
        Raylib.EndMode3D();
        
        Player.DrawUI();
        
        //fogEffect.DrawEnd();
        
        Debug.Draw2D();
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);
        
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }
}
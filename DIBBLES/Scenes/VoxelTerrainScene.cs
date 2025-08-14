using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Effects;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class VoxelTerrainScene : Scene
{
    private TerrainGeneration terrainGen = new TerrainGeneration();
    
    private Player player = new Player();
    //private Freecam freecam = new Freecam();
    
    private FogEffect fogEffect = new FogEffect();

    public override void Start()
    {
        // Must be set for proper depth buffers
        Rlgl.SetClipPlanes(0.01f, 1000f);
        
        Raylib.DisableCursor();
        
        player.Start();
        //freecam.Start();
        
        // Initial terrain generation
        terrainGen.Start();
        terrainGen.Update(player);
        
        fogEffect.Start();
        
        WorldSave.Initialize();
    }

    public override void Update()
    {
        player.Update();
        //freecam.Update();
        
        terrainGen.Update(player);
        TerrainGeneration.Gameplay.Update(player.Camera);
        
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
        
        Debug.Update(player.Camera); // Must run after everything
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        
        //fogEffect.DrawStart();
        //Raylib.ClearBackground(Color.SkyBlue);
        
        Raylib.BeginMode3D(player.Camera);
        
        terrainGen.Draw();
        player.Draw();
        //freecam.Draw();
        
        Raylib.EndMode3D();
        
        //fogEffect.DrawEnd();
        
        Debug.Draw2D();
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);
        
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }
}
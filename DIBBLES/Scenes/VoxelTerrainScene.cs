using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class VoxelTerrainScene : Scene
{
    private TerrainGeneration terrainGen = new TerrainGeneration();
    private Camera3D _camera;
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;
    
    public Vector3 CameraDirection = Vector3.Zero;

    public override void Start()
    {
        _camera = new Camera3D
        {
            Position = new Vector3(0f, TerrainGeneration.ChunkHeight, 0f),
            Target = new Vector3(0f, TerrainGeneration.ChunkHeight, 1f),
            Up = new Vector3(0, 1, 0),
            FovY = 60.0f,
            Projection = CameraProjection.Perspective
        };
        
        Raylib.DisableCursor();
        
        // Initial terrain generation
        terrainGen.Start();
        terrainGen.UpdateTerrain(_camera);
        
        WorldSave.Initialize();
    }

    public override void Update()
    {
        Debug.Update(_camera);
        
        //terrainGen.UpdateTerrain(_camera);
        terrainGen.UpdateMovement(_camera);
        
        // Temporary world saving/loading
        if (Raylib.IsKeyDown(KeyboardKey.O))
            WorldSave.LoadWorldData("test");

        if (Raylib.IsKeyDown(KeyboardKey.L))
            WorldSave.SaveWorldData("test");
        
        float currentMovespeed;

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift))
            currentMovespeed = 60f;
        else
            currentMovespeed = 20f;
        
        float moveSpeed = currentMovespeed * Time.DeltaTime;

        var direction = new Vector3();

        if (Raylib.IsKeyDown(KeyboardKey.W))
            _camera.Position += Vector3.Normalize(_camera.Target - _camera.Position) * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.S))
            _camera.Position -= Vector3.Normalize(_camera.Target - _camera.Position) * moveSpeed;

        if (Raylib.IsKeyDown(KeyboardKey.A))
            _camera.Position += Vector3.Normalize(Vector3.Cross(_camera.Up, _camera.Target - _camera.Position)) * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.D))
            _camera.Position -= Vector3.Normalize(Vector3.Cross(_camera.Up, _camera.Target - _camera.Position)) * moveSpeed;
        
        // --- Mouse input for camera rotation ---
        var mouseDeltaX = Raylib.GetMouseDelta().X * 0.1f;
        var mouseDeltaY = Raylib.GetMouseDelta().Y * 0.1f;
        
        cameraYaw += mouseDeltaX;
        cameraPitch -= mouseDeltaY;
        cameraPitch = Math.Clamp(cameraPitch, -89.0f, 89.0f);

        // Calculate camera direction
        CameraDirection = new Vector3(
            MathF.Cos(GMath.ToRadians(cameraYaw)) * MathF.Cos(GMath.ToRadians(cameraPitch)),
            MathF.Sin(GMath.ToRadians(cameraPitch)),
            MathF.Sin(GMath.ToRadians(cameraYaw)) * MathF.Cos(GMath.ToRadians(cameraPitch))
        );
        
        _camera.Target = _camera.Position + CameraDirection;
        
        // --- Block breaking and placing ---
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            terrainGen.BreakBlock();
        
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            BlockType blockToPlace = BlockType.Dirt; // Default
            
            // Select block type based on number keys
            if (Raylib.IsKeyDown(KeyboardKey.One)) blockToPlace = BlockType.Dirt;
            else if (Raylib.IsKeyDown(KeyboardKey.Two)) blockToPlace = BlockType.Stone;
            else if (Raylib.IsKeyDown(KeyboardKey.Three)) blockToPlace = BlockType.Grass;
            else if (Raylib.IsKeyDown(KeyboardKey.Four)) blockToPlace = BlockType.Sand;
            else if (Raylib.IsKeyDown(KeyboardKey.Five)) blockToPlace = BlockType.Snow;
            else if (Raylib.IsKeyDown(KeyboardKey.Nine)) blockToPlace = BlockType.Torch;
            
            terrainGen.PlaceBlock(blockToPlace);
        }
        
        //terrainGen.UpdateTerrain(_camera.Position);
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);
        Raylib.BeginMode3D(_camera);

        terrainGen.Draw();
        
        // Draw chunk coordinates in 2D after 3D rendering
        //foreach (var pos in terrainGen.chunks.Keys)
        //{
        //    var chunkCenter = pos + new Vector3(TerrainGeneration.ChunkSize / 2f, TerrainGeneration.ChunkHeight + 2f, TerrainGeneration.ChunkSize / 2f);
        //
        //    Debug.Draw3DText($"Chunk ({pos.X}, {pos.Z})", chunkCenter, Color.White);
        //}

        Raylib.EndMode3D();
        
        
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);
        
        // Draw block selection UI
        Raylib.DrawText("Block Selection:", 10, 40, 20, Color.White);
        Raylib.DrawText("1-Dirt 2-Stone 3-Grass 4-Sand 5-Snow 9-Torch", 10, 60, 16, Color.White);
        Raylib.DrawText("Right Click to place, Left Click to break", 10, 80, 16, Color.White);
        
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }
}
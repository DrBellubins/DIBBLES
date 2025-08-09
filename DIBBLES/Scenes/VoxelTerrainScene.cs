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
        terrainGen.Start(_camera);
        terrainGen.UpdateTerrain(_camera.Position);
    }

    public override void Update()
    {
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
            terrainGen.BreakBlock(_camera);
        
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
            terrainGen.PlaceBlock(BlockType.Dirt, _camera); // Default to placing dirt blocks
        
        //terrainGen.UpdateTerrain(_camera.Position);
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);
        Raylib.BeginMode3D(_camera);

        terrainGen.Draw();

        Raylib.EndMode3D();
        
        // Draw chunk coordinates in 2D after 3D rendering
        //foreach (var pos in terrainGen.chunks.Keys)
        //{
        //    Vector3 chunkCenter = pos + new Vector3(TerrainGeneration.ChunkSize / 2f, TerrainGeneration.ChunkHeight + 2f, TerrainGeneration.ChunkSize / 2f);
        //    Vector2 screenPos = Raylib.GetWorldToScreen(chunkCenter, _camera);
        //    
        //    Raylib.DrawText($"Chunk ({pos.X}, {pos.Z})", (int)screenPos.X, (int)screenPos.Y, 24, Color.Blue);
        //}
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);
        
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }
}
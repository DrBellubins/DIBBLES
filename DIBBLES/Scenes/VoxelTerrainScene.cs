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
    private Camera3D _camera;
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;
    
    private Vector3 cameraDirection = Vector3.Zero;
    
    private FogEffect fogEffect = new FogEffect();

    public override void Start()
    {
        _camera = new Camera3D
        {
            Position = new Vector3(0f, TerrainGeneration.ChunkSize, 0f),
            Target = new Vector3(0f, TerrainGeneration.ChunkSize, 1f),
            Up = new Vector3(0, 1, 0),
            FovY = 60.0f,
            Projection = CameraProjection.Perspective
        };
        
        // Must be set for proper depth buffers
        Rlgl.SetClipPlanes(0.01f, 1000f);
        
        Raylib.DisableCursor();
        
        // Initial terrain generation
        terrainGen.Start();
        terrainGen.Update(_camera);
        
        fogEffect.Start();
        
        WorldSave.Initialize();
    }

    public override void Update()
    {
        Debug.Update(_camera);
        
        terrainGen.Update(_camera);
        TerrainGeneration.Gameplay.Update(_camera);
        
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
        cameraDirection = new Vector3(
            MathF.Cos(GMath.ToRadians(cameraYaw)) * MathF.Cos(GMath.ToRadians(cameraPitch)),
            MathF.Sin(GMath.ToRadians(cameraPitch)),
            MathF.Sin(GMath.ToRadians(cameraYaw)) * MathF.Cos(GMath.ToRadians(cameraPitch))
        );
        
        _camera.Target = _camera.Position + cameraDirection;
        
        // --- Block breaking and placing ---
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            TerrainGeneration.Gameplay.BreakBlock();
        
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
            TerrainGeneration.Gameplay.PlaceBlock(BlockType.Snow);
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        
        //fogEffect.DrawStart();
        //Raylib.ClearBackground(Color.SkyBlue);
        
        Raylib.BeginMode3D(_camera);
        
        terrainGen.Draw();
        
        Raylib.EndMode3D();
        
        //fogEffect.DrawEnd();
        
        Raylib.DrawCircle(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2, 1f, Color.White);
        
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }
}
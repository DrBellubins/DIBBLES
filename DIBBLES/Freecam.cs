using System.Numerics;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES;

public class Freecam
{
    public Camera3D Camera;
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;
    private Vector3 cameraDirection = Vector3.Zero;
    
    public void Start()
    {
        Camera = new Camera3D
        {
            Position = new Vector3(0f, TerrainGeneration.ChunkSize, 0f),
            Target = new Vector3(0f, TerrainGeneration.ChunkSize, 1f),
            Up = new Vector3(0, 1, 0),
            FovY = 60.0f,
            Projection = CameraProjection.Perspective
        };
    }
    
    public void Update()
    {
        float currentMovespeed;

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift))
            currentMovespeed = 60f;
        else
            currentMovespeed = 20f;
        
        float moveSpeed = currentMovespeed * Time.DeltaTime;

        var direction = new Vector3();

        if (Raylib.IsKeyDown(KeyboardKey.W))
            Camera.Position += Vector3.Normalize(Camera.Target - Camera.Position) * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.S))
            Camera.Position -= Vector3.Normalize(Camera.Target - Camera.Position) * moveSpeed;

        if (Raylib.IsKeyDown(KeyboardKey.A))
            Camera.Position += Vector3.Normalize(Vector3.Cross(Camera.Up, Camera.Target - Camera.Position)) * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.D))
            Camera.Position -= Vector3.Normalize(Vector3.Cross(Camera.Up, Camera.Target - Camera.Position)) * moveSpeed;
        
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
        
        Camera.Target = Camera.Position + cameraDirection;
    }
}
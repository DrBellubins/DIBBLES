using Raylib_cs;
using System.Numerics;

namespace DIBBLES;

public class Player
{
    public const float WalkSpeed = 5.0f;
    
    public Vector3 Position { get; set; } = new Vector3(0.0f, 0.0f, 0.0f);
    public Camera3D Camera;
    
    private float mouseSensitivity = 0.1f;
    private float cameraYaw = 0f;   // Horizontal rotation
    private float cameraPitch = 0f; // Vertical rotation
    
    public void Start()
    {
        // Set up camera
        Camera = new Camera3D();
        Camera.Position = new Vector3(0.0f, 2.0f, 0.0f); // Camera slightly above ground
        Camera.Target = new Vector3(0.0f, 2.0f, 1.0f);  // Looking forward
        Camera.Up = new Vector3(0.0f, 1.0f, 0.0f);      // Up vector
        Camera.FovY = 90.0f;                            // Field of view
        Camera.Projection = CameraProjection.Perspective;
        
        // Hide and lock cursor
        Raylib.DisableCursor();
    }

    public void Update(float deltaTime)
    {
        // Handle movement input
        Vector3 moveDirection = Vector3.Zero;
        
        if (Raylib.IsKeyDown(KeyboardKey.W)) moveDirection.Z += 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.S)) moveDirection.Z -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.A)) moveDirection.X -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.D)) moveDirection.X += 1.0f;
        
        // Calculate camera's forward direction (ignoring pitch for movement)
        Vector3 cameraForward = new Vector3(
            MathF.Cos(MathHelper.ToRadians(cameraYaw)),
            0.0f, // Keep movement on XZ plane
            MathF.Sin(MathHelper.ToRadians(cameraYaw))
        );
        cameraForward = Vector3.Normalize(cameraForward);
        
        // Calculate right direction (perpendicular to forward)
        Vector3 cameraRight = new Vector3(
            MathF.Cos(MathHelper.ToRadians(cameraYaw + 90.0f)),
            0.0f,
            MathF.Sin(MathHelper.ToRadians(cameraYaw + 90.0f))
        );
        cameraRight = Vector3.Normalize(cameraRight);
        
        // Transform input direction to camera's coordinate system
        Vector3 worldMoveDirection = (cameraForward * moveDirection.Z) + (cameraRight * moveDirection.X);
        
        // Normalize a movement direction and apply speed
        if (worldMoveDirection.Length() > 0)
        {
            worldMoveDirection = Vector3.Normalize(worldMoveDirection);
            worldMoveDirection *= WalkSpeed * Raylib.GetFrameTime();
        }
        
        // Update player position
        Position += worldMoveDirection;
        
        // Handle mouse input for camera rotation
        float mouseDeltaX = Raylib.GetMouseDelta().X * mouseSensitivity;
        float mouseDeltaY = Raylib.GetMouseDelta().Y * mouseSensitivity;
        
        cameraYaw += mouseDeltaX;
        cameraPitch -= mouseDeltaY;
        cameraPitch = Math.Clamp(cameraPitch, -89.0f, 89.0f); // Limit vertical look
        
        // Calculate a camera direction for looking
        Vector3 cameraDirection = new Vector3(
            MathF.Cos(MathHelper.ToRadians(cameraYaw)) * MathF.Cos(MathHelper.ToRadians(cameraPitch)),
            MathF.Sin(MathHelper.ToRadians(cameraPitch)),
            MathF.Sin(MathHelper.ToRadians(cameraYaw)) * MathF.Cos(MathHelper.ToRadians(cameraPitch))
        );
        
        // Update camera
        Camera.Position = Position + new Vector3(0.0f, 1.8f, 0.0f); // Camera offset above player
        Camera.Target = Camera.Position + cameraDirection;
    }
}
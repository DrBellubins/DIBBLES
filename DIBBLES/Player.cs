using Raylib_cs;
using System.Numerics;
using DIBBLES.Systems;
using DIBBLES.Utils;

using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES;

public class Player
{
    // HL2 movement values, converted to meters and m/s.
    public const float WalkSpeed = 3.619f;        // 361.9 Hu
    public const float RunSpeed = 6.096f;         // 609.6 Hu
    public const float AirAcceleration = 10.0f;  // HL2 style air accel
    public const float GroundAcceleration = 10.0f; // HL2 style ground accel
    public const float GroundFriction = 8.0f;    // HL2 style ground friction
    public const float AirFriction = 0.0f;       // Less friction in air
    public const float Gravity = 20.32f;         // HL2 = 800 units/s² ≈ 20.32 m/s²
    public const float JumpImpulse = 3.048f * 2f;       // HL2 jump velocity ≈ 5 m/s
    public const float PlayerHeight = 1.83f;     // HL2 player height ≈ 72 units
    public const float CrouchHeight = 0.91f;     // HL2 crouch height ≈ 36 units
    public const float CrouchSpeed = 2.54f;      // HL2 crouch speed ≈ 100 units/s

    public Vector3 Position = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 Velocity = Vector3.Zero;
    public Vector3 CameraDirection = Vector3.Zero;
    
    public Camera3D Camera;

    private float currentSpeed = WalkSpeed;
    private float currentHeight = PlayerHeight;
    private float mouseSensitivity = 0.1f;
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;
    
    private bool isJumping = false;
    private bool isGrounded = false;
    private bool isCrouching = false;

    private bool justJumped = false;
    private bool justLanded = false;
    
    private AudioPlayer jumpLandPlayer = new AudioPlayer();
    
    public void Start()
    {
        Camera = new Camera3D();
        Camera.Position = new Vector3(0.0f, PlayerHeight * 0.5f, 0.0f);
        Camera.Target = new Vector3(0.0f, PlayerHeight * 0.5f, 1.0f);
        Camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        Camera.FovY = 90.0f;
        Camera.Projection = CameraProjection.Perspective;

        Raylib.DisableCursor();
    }

    public void Update()
    {
        // --- Input ---
        Vector3 inputDir = Vector3.Zero;

        // Allow tabbing out and back into game
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Raylib.EnableCursor();
        
        var mousePosition = Raylib.GetMousePosition();
        
        var isCursorInWindow = mousePosition.X >= 0 && mousePosition.X <= Engine.ScreenWidth &&
                                mousePosition.Y >= 0 && mousePosition.Y <= Engine.ScreenHeight;
        
        if (isCursorInWindow && Raylib.IsMouseButtonPressed(MouseButton.Left))
            Raylib.DisableCursor();
        
        if (Raylib.IsKeyDown(KeyboardKey.W)) inputDir.Z += 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.S)) inputDir.Z -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.A)) inputDir.X -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.D)) inputDir.X += 1.0f;

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift) && !isCrouching)
            currentSpeed = RunSpeed;
        else
            currentSpeed = isCrouching ? CrouchSpeed : WalkSpeed;
        
        var crouchKey = Raylib.IsKeyDown(KeyboardKey.C);
        isCrouching = crouchKey;
        
        isJumping = isCrouching ? Raylib.IsKeyPressed(KeyboardKey.Space) : Raylib.IsKeyDown(KeyboardKey.Space);

        // TODO: Add justJumped and justLanded
        
        // --- Gravity & Vertical Movement ---
        Velocity.Y -= Gravity * Time.DeltaTime;
        Position.Y += Velocity.Y * Time.DeltaTime;

        // --- Ground Collision ---
        var playerBox = GetPlayerBox(Position, currentHeight);
        var wasGrounded = isGrounded; // Track previous grounded state for jump timing

        // Reset one-frame flags at the start of each frame
        justJumped = false;
        justLanded = false;
        
        // Collision detection
        CheckCollisions(playerBox);

        // --- Mouse input for camera rotation ---
        var mouseDeltaX = Raylib.GetMouseDelta().X * mouseSensitivity;
        var mouseDeltaY = Raylib.GetMouseDelta().Y * mouseSensitivity;
        
        cameraYaw += mouseDeltaX;
        cameraPitch -= mouseDeltaY;
        cameraPitch = Math.Clamp(cameraPitch, -89.0f, 89.0f);

        // Calculate camera direction
        CameraDirection = new Vector3(
            MathF.Cos(GMath.ToRadians(cameraYaw)) * MathF.Cos(GMath.ToRadians(cameraPitch)),
            MathF.Sin(GMath.ToRadians(cameraPitch)),
            MathF.Sin(GMath.ToRadians(cameraYaw)) * MathF.Cos(GMath.ToRadians(cameraPitch))
        );

        // Camera position
        Camera.Position = Position + new Vector3(0.0f, PlayerHeight * 0.5f, 0.0f);
        Camera.Target = Camera.Position + CameraDirection;
        
        // Camera-relative movement
        Vector3 cameraForward = new Vector3(
            MathF.Cos(GMath.ToRadians(cameraYaw)),
            0.0f,
            MathF.Sin(GMath.ToRadians(cameraYaw))
        );
        
        cameraForward = Vector3.Normalize(cameraForward);

        Vector3 cameraRight = new Vector3(
            MathF.Cos(GMath.ToRadians(cameraYaw + 90.0f)),
            0.0f,
            MathF.Sin(GMath.ToRadians(cameraYaw + 90.0f))
        );
        
        cameraRight = Vector3.Normalize(cameraRight);

        Vector3 wishDir = (cameraForward * inputDir.Z) + (cameraRight * inputDir.X);

        if (wishDir.Length() > 0)
            wishDir = Vector3.Normalize(wishDir);

        // --- HL2 Style Acceleration & Friction ---
        float accel = isGrounded ? GroundAcceleration : AirAcceleration;
        float friction = isGrounded ? GroundFriction : AirFriction;

        Vector3 wishVel = wishDir * currentSpeed;
        Vector3 velXZ = new Vector3(Velocity.X, 0f, Velocity.Z);
        
        float wishSpeed = wishVel.Length();

        // HL2-style friction: Only apply friction when no input and grounded
        if (wishSpeed == 0 && isGrounded)
        {
            float speed = velXZ.Length();
            
            if (speed != 0)
            {
                float drop = speed * friction * Time.DeltaTime;
                float newSpeed = Math.Max(speed - drop, 0);
                velXZ *= (newSpeed / speed);
            }
        }

        // HL2-style acceleration: Only accelerate toward wishDir when input is present
        if (wishSpeed > 0)
        {
            float currentSpeedInDir = Vector3.Dot(velXZ, wishDir);
            float addSpeed = wishSpeed - currentSpeedInDir;
            
            if (addSpeed > 0)
            {
                float accelSpeed = accel * Time.DeltaTime * wishSpeed;
                
                if (accelSpeed > addSpeed) accelSpeed = addSpeed;
                    velXZ += wishDir * accelSpeed;
            }
        }

        // Relax speed cap for bunnyhopping
        if (isGrounded && wasGrounded)
        {
            // Apply speed cap only when grounded for multiple frames (not a bunnyhop)
            if (velXZ.Length() > currentSpeed)
                velXZ = Vector3.Normalize(velXZ) * currentSpeed;
        }

        Velocity.X = velXZ.X;
        Velocity.Z = velXZ.Z;

        Position.X += Velocity.X * Time.DeltaTime;
        Position.Z += Velocity.Z * Time.DeltaTime;

        // --- Crouching ---
        var targetHeight = isCrouching ? CrouchHeight : PlayerHeight;
        var heightLerpSpeed = 20f;
        
        currentHeight = GMath.Lerp(currentHeight, targetHeight, heightLerpSpeed * Time.DeltaTime);
        
        // --- Jumping ---
        if (isGrounded && isJumping)
        {
            Velocity.Y = JumpImpulse;
            isGrounded = false;
            justJumped = true;
        }
        
        jumpLandPlayer.Update();
    }

    public void Draw()
    {
        Raylib.DrawSphereWires(Position, 10f, 8, 8, Color.Red);
    }

    public void CheckCollisions(BoundingBox playerBox)
    {
        var moveDelta = Velocity * Time.DeltaTime;
        var newPosition = Position;

        // Call once per frame before axis checks!
        var blockBoxes = GetBoundingBoxes(Position, 10f);

        // X axis
        newPosition.X += moveDelta.X;
        
        var playerBoxX = GetPlayerBox(newPosition, currentHeight);
        var collidedX = blockBoxes.Any(box => Raylib.CheckCollisionBoxes(playerBoxX, box));
        
        if (collidedX)
        {
            newPosition.X -= moveDelta.X;
            Velocity.X = 0;
        }

        // Y axis
        newPosition.Y += moveDelta.Y;
        
        var playerBoxY = GetPlayerBox(newPosition, currentHeight);
        var collidedY = blockBoxes.Any(box => Raylib.CheckCollisionBoxes(playerBoxY, box));
        
        if (collidedY)
        {
            newPosition.Y -= moveDelta.Y;
            Velocity.Y = 0;
            isGrounded = true;
        }

        // Z axis
        newPosition.Z += moveDelta.Z;
        
        var playerBoxZ = GetPlayerBox(newPosition, currentHeight);
        var collidedZ = blockBoxes.Any(box => Raylib.CheckCollisionBoxes(playerBoxZ, box));
        
        if (collidedZ)
        {
            newPosition.Z -= moveDelta.Z;
            Velocity.Z = 0;
        }

        Position = newPosition;
    }
    
    public static List<BoundingBox> GetBoundingBoxes(Vector3 center, float radius)
    {
        var result = new List<BoundingBox>();
        
        int minX = (int)MathF.Floor(center.X - radius);
        int maxX = (int)MathF.Floor(center.X + radius);
        int minY = (int)MathF.Floor(center.Y - radius);
        int maxY = (int)MathF.Floor(center.Y + radius);
        int minZ = (int)MathF.Floor(center.Z - radius);
        int maxZ = (int)MathF.Floor(center.Z + radius);

        float radiusSquared = radius * radius;

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            var blockCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
            
            if (Vector3.DistanceSquared(center, blockCenter) > radiusSquared)
                continue;

            // Find which chunk this block belongs to
            int chunkX = (int)Math.Floor((float)x / ChunkSize) * ChunkSize;
            int chunkY = (int)Math.Floor((float)y / ChunkSize) * ChunkSize;
            int chunkZ = (int)Math.Floor((float)z / ChunkSize) * ChunkSize;
            var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

            if (!Chunks.TryGetValue(chunkCoord, out var chunk))
                continue;

            int localX = x - chunkX;
            int localY = y - chunkY;
            int localZ = z - chunkZ;

            // Bounds check
            if (localX < 0 || localX >= ChunkSize ||
                localY < 0 || localY >= ChunkSize ||
                localZ < 0 || localZ >= ChunkSize)
                continue;

            var block = chunk.Blocks[localX, localY, localZ];
            
            // Only add solid blocks
            if (block != null && block.Info.Type != BlockType.Air && !block.Info.IsTransparent)
            {
                var blockMin = new Vector3(x, y, z);
                var blockMax = blockMin + Vector3.One;
                result.Add(new BoundingBox(blockMin, blockMax));
            }
        }
        
        return result;
    }
    
    // Player box size: width and depth ≈ 0.5m (Source player is 32 units wide ≈ 0.81m, but keep hitbox thin for simplicity)
    private BoundingBox GetPlayerBox(Vector3 position, float height)
    {
        Vector3 min = new Vector3(
            position.X - 0.25f,
            position.Y - height * 0.5f,
            position.Z - 0.25f
        );
        Vector3 max = new Vector3(
            position.X + 0.25f,
            position.Y + height * 0.5f,
            position.Z + 0.25f
        );
        return new BoundingBox(min, max);
    }
}
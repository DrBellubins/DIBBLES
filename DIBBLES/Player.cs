using Raylib_cs;
using System.Numerics;
using DIBBLES.Systems;
using DIBBLES.Utils;

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
    //private Sound jumpSound;
    //private Sound landSound;
    
    public void Start()
    {
        //jumpSound = Resource.Load<Sound>("grass_jump.ogg");
        //landSound = Resource.Load<Sound>("grass_land.ogg");
        
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
        
        // Player audio
        //var magnitude = MathF.Abs(Velocity.Length());
        
        /*if (justJumped)
        {
            jumpLandPlayer.Sound = jumpSound;
            //jumpLandPlayer.Play2D();
        }
        else if (justLanded)
        {
            jumpLandPlayer.Sound = landSound;
            //jumpLandPlayer.Play2D();
        }*/
        
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
        /*if (Raylib.CheckCollisionBoxes(playerBox, groundBox))
        {
            Position.Y = groundBox.Max.Y + currentHeight * 0.5f;
            Velocity.Y = 0.0f;
            isGrounded = true;
            
            // Detect landing (transition from not grounded to grounded)
            if (!wasGrounded)
                justLanded = true;
        }
        else
            isGrounded = false;*/

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

    public void CheckCollisions(BoundingBox playerBox)
    {
        foreach (var chunk in TerrainGeneration.Chunks.Values)
        {
            for (int x = 0; x < TerrainGeneration.ChunkSize; x++)
            for (int y = 0; y < TerrainGeneration.ChunkSize; y++)
            for (int z = 0; z < TerrainGeneration.ChunkSize; z++)
            {
                var block = chunk.Blocks[x, y, z];
                
                if (block.Info.Type == BlockType.Air || block.Info.IsTransparent)
                    continue;

                var blockPos = new Vector3(
                    chunk.Position.X + x,
                    chunk.Position.Y + y,
                    chunk.Position.Z + z);

                var blockBox = new BoundingBox(blockPos, blockPos + Vector3.One);

                if (Raylib.CheckCollisionBoxes(playerBox, blockBox))
                {
                    Position.Y = blockBox.Max.Y + currentHeight * 0.5f;
                    Velocity.Y = 0.0f;
                    isGrounded = true;
                }
            }
        }
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
using Raylib_cs;
using System.Numerics;
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
    public Camera3D Camera;

    private float currentSpeed = WalkSpeed;
    private float currentHeight = PlayerHeight;
    private float mouseSensitivity = 0.1f;
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;

    private Vector3 velocity = Vector3.Zero;
    
    private bool isJumping = false;
    private bool isGrounded = false;
    private bool isCrouching = false;

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

    public void Update(BoundingBox groundBox)
    {
        // --- Input ---
        Vector3 inputDir = Vector3.Zero;
        
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

        // --- Gravity & Vertical Movement ---
        velocity.Y -= Gravity * Time.DeltaTime;
        Position.Y += velocity.Y * Time.DeltaTime;

        // --- Ground Collision ---
        var playerBox = GetPlayerBox(Position, currentHeight);
        var wasGrounded = isGrounded; // Track previous grounded state for jump timing

        if (Raylib.CheckCollisionBoxes(playerBox, groundBox))
        {
            Position.Y = groundBox.Max.Y + currentHeight * 0.5f;
            velocity.Y = 0.0f;
            isGrounded = true;
        }
        else
            isGrounded = false;

        // --- Mouse input for camera rotation ---
        var mouseDeltaX = Raylib.GetMouseDelta().X * mouseSensitivity;
        var mouseDeltaY = Raylib.GetMouseDelta().Y * mouseSensitivity;
        
        cameraYaw += mouseDeltaX;
        cameraPitch -= mouseDeltaY;
        cameraPitch = Math.Clamp(cameraPitch, -89.0f, 89.0f);

        // Calculate camera direction
        Vector3 cameraDirection = new Vector3(
            MathF.Cos(MathHelper.ToRadians(cameraYaw)) * MathF.Cos(MathHelper.ToRadians(cameraPitch)),
            MathF.Sin(MathHelper.ToRadians(cameraPitch)),
            MathF.Sin(MathHelper.ToRadians(cameraYaw)) * MathF.Cos(MathHelper.ToRadians(cameraPitch))
        );

        // Camera position
        Camera.Position = Position + new Vector3(0.0f, PlayerHeight * 0.5f, 0.0f);
        Camera.Target = Camera.Position + cameraDirection;
        
        // Camera-relative movement
        Vector3 cameraForward = new Vector3(
            MathF.Cos(MathHelper.ToRadians(cameraYaw)),
            0.0f,
            MathF.Sin(MathHelper.ToRadians(cameraYaw))
        );
        
        cameraForward = Vector3.Normalize(cameraForward);

        Vector3 cameraRight = new Vector3(
            MathF.Cos(MathHelper.ToRadians(cameraYaw + 90.0f)),
            0.0f,
            MathF.Sin(MathHelper.ToRadians(cameraYaw + 90.0f))
        );
        
        cameraRight = Vector3.Normalize(cameraRight);

        Vector3 wishDir = (cameraForward * inputDir.Z) + (cameraRight * inputDir.X);

        if (wishDir.Length() > 0)
            wishDir = Vector3.Normalize(wishDir);

        // --- HL2 Style Acceleration & Friction ---
        float accel = isGrounded ? GroundAcceleration : AirAcceleration;
        float friction = isGrounded ? GroundFriction : AirFriction;

        Vector3 wishVel = wishDir * currentSpeed;
        Vector3 velXZ = new Vector3(velocity.X, 0f, velocity.Z);
        
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

        velocity.X = velXZ.X;
        velocity.Z = velXZ.Z;

        Position.X += velocity.X * Time.DeltaTime;
        Position.Z += velocity.Z * Time.DeltaTime;

        // --- Crouching ---
        var targetHeight = isCrouching ? CrouchHeight : PlayerHeight;
        var heightLerpSpeed = 20f;
        
        currentHeight = MathHelper.Lerp(currentHeight, targetHeight, heightLerpSpeed * Time.DeltaTime);
        
        // --- Jumping ---
        if (isGrounded && isJumping)
        {
            velocity.Y = JumpImpulse;
            isGrounded = false;
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
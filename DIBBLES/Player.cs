using Raylib_cs;
using System.Numerics;

namespace DIBBLES;

public class Player
{
    public const float WalkSpeed = 5.0f;
    public const float RunSpeed = 8.0f;
    public const float AirAcceleration = 6.0f;     // HL2 style air accel
    public const float GroundAcceleration = 30.0f; // HL2 style ground accel
    public const float GroundFriction = 20.0f;     // HL2 style ground friction
    public const float AirFriction = 2.0f;         // Less friction in air
    public const float Gravity = 15.0f;
    public const float JumpImpulse = 7.5f;
    public const float PlayerHeight = 2.0f;
    public const float CrouchHeight = 1.0f;
    public const float CrouchSpeed = 2.0f;

    public Vector3 Position = new Vector3(0.0f, 0.0f, 0.0f);
    public Camera3D Camera;

    private float currentSpeed = WalkSpeed;
    private float mouseSensitivity = 0.1f;
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;

    private Vector3 velocity = Vector3.Zero;
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

    public void Update(BoundingBox groundBox, float deltaTime)
    {
        // --- Handle Crouch ---
        bool crouchKey = Raylib.IsKeyDown(KeyboardKey.LeftControl);
        if (crouchKey && !isCrouching)
        {
            isCrouching = true;
        }
        else if (!crouchKey && isCrouching)
        {
            isCrouching = false;
        }

        float targetHeight = isCrouching ? CrouchHeight : PlayerHeight;
        // Smooth crouch transition (optional)
        float camHeight = Camera.Position.Y - Position.Y;
        float desiredCamHeight = targetHeight * 0.5f;
        float heightLerpSpeed = 12f;
        Camera.Position.Y = Position.Y + MathHelper.Lerp(camHeight, desiredCamHeight, heightLerpSpeed * deltaTime);

        // --- Gravity & Vertical Movement ---
        velocity.Y -= Gravity * deltaTime;
        Position.Y += velocity.Y * deltaTime;

        // --- Ground Collision ---
        var playerBox = GetPlayerBox(Position, targetHeight);

        if (Raylib.CheckCollisionBoxes(playerBox, groundBox))
        {
            Position.Y = groundBox.Max.Y + targetHeight * 0.5f;
            velocity.Y = 0.0f;
            isGrounded = true;
        }
        else
            isGrounded = false;

        // --- Mouse input for camera rotation ---
        float mouseDeltaX = Raylib.GetMouseDelta().X * mouseSensitivity;
        float mouseDeltaY = Raylib.GetMouseDelta().Y * mouseSensitivity;
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
        Camera.Position = Position + new Vector3(0.0f, desiredCamHeight, 0.0f);
        Camera.Target = Camera.Position + cameraDirection;

        // --- Movement Input ---
        Vector3 inputDir = Vector3.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) inputDir.Z += 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.S)) inputDir.Z -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.A)) inputDir.X -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.D)) inputDir.X += 1.0f;

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift) && !isCrouching)
            currentSpeed = RunSpeed;
        else
            currentSpeed = isCrouching ? CrouchSpeed : WalkSpeed;

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

        // Accelerate towards wishVel (HL2 style)
        Vector3 velXZ = new Vector3(velocity.X, 0f, velocity.Z);
        float wishSpeed = wishVel.Length();

        if (wishSpeed > 0)
        {
            float currentSpeed = Vector3.Dot(velXZ, wishDir);
            float addSpeed = wishSpeed - currentSpeed;
            
            if (addSpeed > 0)
            {
                float accelSpeed = accel * deltaTime * wishSpeed;
                if (accelSpeed > addSpeed) accelSpeed = addSpeed;
                velXZ += wishDir * accelSpeed;
            }
        }
        else if (isGrounded)
        {
            // Apply friction
            float speed = velXZ.Length();
            
            if (speed != 0)
            {
                float drop = speed * friction * deltaTime;
                float newSpeed = Math.Max(speed - drop, 0);
                velXZ *= (newSpeed / speed);
            }
        }

        // Clamp horizontal speed
        if (velXZ.Length() > currentSpeed)
            velXZ = Vector3.Normalize(velXZ) * currentSpeed;

        velocity.X = velXZ.X;
        velocity.Z = velXZ.Z;

        Position.X += velocity.X * deltaTime;
        Position.Z += velocity.Z * deltaTime;

        // --- Jumping ---
        if (isGrounded && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            velocity.Y = JumpImpulse;
            isGrounded = false;
        }
    }

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
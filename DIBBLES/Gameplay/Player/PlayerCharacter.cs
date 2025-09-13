using Raylib_cs;
using System.Numerics;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;

using static DIBBLES.Terrain.TerrainGeneration;
using Debug = DIBBLES.Utils.Debug;

namespace DIBBLES.Gameplay.Player;

// TODO: Crouching freezes player
public class PlayerCharacter
{
    // HL2 movement values, converted to meters and m/s.
    public const float WalkSpeed = 3.619f;        // 361.9 Hu
    public const float RunSpeed = 6.096f;         // 609.6 Hu
    public const float CrouchSpeed = 2.54f * 0.5f;      // HL2 crouch speed ≈ 100 units/s
    public const float AirAcceleration = 10.0f;  // HL2 style air accel
    public const float GroundAcceleration = 10.0f; // HL2 style ground accel
    public const float GroundFriction = 8.0f;    // HL2 style ground friction
    public const float AirFriction = 0.0f;       // Less friction in air
    public const float Gravity = 20.32f;         // HL2 = 800 units/s² ≈ 20.32 m/s²
    public const float JumpImpulse = 3.048f * 2.3f;       // HL2 jump velocity ≈ 5 m/s
    public const float PlayerHeight = 1.83f;     // HL2 player height ≈ 72 units
    public const float CrouchHeight = 0.91f;     // HL2 crouch height ≈ 36 units
    
    // Gameplay
    public int Health = 100;
    public Hotbar hotbar = new();
    
    private readonly Vector3 spawnPosition = new Vector3(0f, 0f, 0f); // Temporary
    
    // Systems
    public Vector3 Position = Vector3.Zero;
    public Vector3 Velocity = Vector3.Zero;
    
    public Camera3D Camera;

    public BoundingBox CollisionBox = new();

    public Quaternion CameraRotation = Quaternion.Identity;
    
    public Vector3 CameraForward = Vector3.Zero;
    public Vector3 CameraUp = Vector3.Zero;
    public Vector3 CameraRight = Vector3.Zero;
    
    public bool FreeCamEnabled = true;
    public Freecam freecam = new();

    public bool NeedsToSpawn = false;
    public bool ShouldUpdate = false;
    public bool IsDead = false;
    
    public float CameraPitch = 0f;
    public float CameraYaw = 0f;

    private Sound fallSound;
    private HandModel handModel = new();
    
    private float currentSpeed = WalkSpeed;
    private float currentHeight = PlayerHeight;
    private float mouseSensitivity = 0.1f;

    private float placeBreakTimer = 0f;
    
    private bool canMove = true;

    private bool isRunning = false;
    private bool isJumping = false;
    private bool isFalling = false;
    private bool isGrounded = false;
    private bool isCrouching = false;

    private bool wasGrounded = false;
    
    private bool justJumped = false;
    private bool justLanded = false;
    
    private float fallTimer = 0f;
    
    public void Start()
    {
        fallSound = Resource.LoadSoundSpecial("pain.ogg");
        
        Camera = new Camera3D();
        Camera.Position = new Vector3(0.0f, PlayerHeight * 0.5f, 0.0f);
        Camera.Target = new Vector3(0.0f, PlayerHeight * 0.5f, 1.0f);
        Camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        Camera.FovY = 90.0f;
        Camera.Projection = CameraProjection.Perspective;

        hotbar.Start();
        handModel.Start();
        
        //Raylib.DisableCursor();
    }
    
    float lastHeight = PlayerHeight;
    
    public void Update()
    {
        if (!ShouldUpdate)
            return;

        hotbar.Update(IsDead);
        
        // --- Block breaking and placing ---
        placeBreakTimer += Time.DeltaTime;

        if (Input.StartedBreaking) // Break immediately
        {
            TerrainGeneration.Gameplay.BreakBlock();
            placeBreakTimer = 0f;
        }
        
        if (Input.Break() && !Input.StartedBreaking) // Hold break
        {
            if (placeBreakTimer >= 0.3f)
            {
                TerrainGeneration.Gameplay.BreakBlock();
                placeBreakTimer = 0f;
            }
        }

        if (Input.StartedInteracting && hotbar.SelectedItem != null) // Place immediately
        {
            TerrainGeneration.Gameplay.PlaceBlock(this, hotbar.SelectedItem.Type);
            placeBreakTimer = 0f;
        }
        
        if (Input.Interact() && hotbar.SelectedItem != null) // Hold place
        {
            if (placeBreakTimer >= 0.3f)
            {
                TerrainGeneration.Gameplay.PlaceBlock(this, hotbar.SelectedItem.Type);
                placeBreakTimer = 0f;
            }
        }
        
        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            if (FreeCamEnabled)
                Velocity = Vector3.Zero;
            
            FreeCamEnabled = !FreeCamEnabled;
        }

        if (FreeCamEnabled)
        {
            freecam.Update(this);
            return;
        }
        
        isGrounded = false; // Reset ground state 
        
        // --- Input ---
        Vector3 inputDir = Vector3.Zero;

        // Allow tabbing out and back into game
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Raylib.EnableCursor();
        
        var mousePosition = Raylib.GetMousePosition();
        
        var isCursorInWindow = mousePosition.X >= 0 && mousePosition.X <= Engine.ScreenWidth &&
                                mousePosition.Y >= 0 && mousePosition.Y <= Engine.ScreenHeight;
        
        if (isCursorInWindow && Raylib.IsMouseButtonPressed(MouseButton.Left))
            Raylib.DisableCursor();
        
        if (Input.MoveForward()) inputDir.Z += 1.0f;
        if (Input.MoveBackward()) inputDir.Z -= 1.0f;
        if (Input.MoveLeft()) inputDir.X -= 1.0f;
        if (Input.MoveRight()) inputDir.X += 1.0f;

        // Run
        if (Input.Run())
            run();

        if (isRunning && isCrouching)
            isRunning = false;
        
        // Crouching
        isCrouching = Input.Crouch();

        // Run vs Crouch checks
        if (isCrouching)
            currentSpeed = CrouchSpeed;
        else if (isRunning)
            currentSpeed = RunSpeed;
        else
            currentSpeed = WalkSpeed;
        
        isJumping = Input.Jump(isCrouching);
        
        // --- Gravity  ---
        Velocity.Y -= Gravity * Time.DeltaTime;
        
        // Reset one-frame flags at the start of each frame
        justJumped = false;
        justLanded = false;
        
        // Collision detection
        checkCollisions();
        
        CollisionBox = getBoundingBox(Position, currentHeight); // Needs to be set after collision detection
        
        // Update falling state
        isFalling = !isGrounded && Velocity.Y < 0f;
        
        // Grounded/Landing checks
        if (isGrounded && !wasGrounded) // Just landed
        {
            justLanded = true;
        }
        else if (!isGrounded && wasGrounded && Velocity.Y < 0f) // Started falling
        {
            
        }
        
        if (isFalling)
            fallTimer += Time.DeltaTime;
        
        // --- Mouse input for camera rotation ---
        Vector2 lookDelta = Vector2.Zero;
        
        if (canMove)
            lookDelta = Input.LookDelta();
        
        var lookDeltaX = lookDelta.X * mouseSensitivity;
        var lookDeltaY = lookDelta.Y * mouseSensitivity;

        CameraYaw += GMath.ToRadians(-lookDeltaX); // Yaw: left and right
        CameraPitch += GMath.ToRadians(lookDeltaY); // Pitch: up and down

        CameraPitch = Math.Clamp(CameraPitch, GMath.ToRadians(-90f), GMath.ToRadians(90f));

        // Build quaternion from yaw and pitch (yaw first, then pitch)
        Quaternion rotYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, CameraYaw);
        Quaternion rotPitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, CameraPitch);

        CameraRotation = Quaternion.Normalize(rotYaw * rotPitch);

        // Calculate camera direction
        CameraForward = Vector3.Transform(Vector3.UnitZ, CameraRotation); // Forward
        CameraUp = Vector3.Transform(Vector3.UnitY, CameraRotation);
        CameraRight = Vector3.Transform(-Vector3.UnitX, CameraRotation); // This has to be flipped for some reason...

        // Camera position
        Camera.Position = Position + new Vector3(0.0f, PlayerHeight * 0.49f, 0.0f);
        Camera.Target = Camera.Position + CameraForward;
        Camera.Up = CameraUp;
        
        // Forward on XZ plane ignoring pitch
        Vector3 forwardXZ = new Vector3(
            MathF.Sin(CameraYaw),
            0.0f,
            MathF.Cos(CameraYaw)
        );

        // Right on XZ plane ignoring pitch
        Vector3 rightXZ = new Vector3(
            MathF.Cos(CameraYaw),
            0.0f,
            -MathF.Sin(CameraYaw)
        );
        
        Vector3 wishDir = (forwardXZ * inputDir.Z) + (-rightXZ * inputDir.X);
        
        if (wishDir.Length() > 0)
            wishDir = Vector3.Normalize(wishDir);

        // --- HL2 Style Acceleration & Friction ---
        float accel = isGrounded ? GroundAcceleration : AirAcceleration;
        float friction = isGrounded ? GroundFriction : AirFriction;

        Vector3 wishVel = Vector3.Zero;
        
        if (canMove)
            wishVel = wishDir * currentSpeed;
        
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
        if (velXZ.Length() > currentSpeed)
            velXZ = Vector3.Normalize(velXZ) * currentSpeed;

        Velocity.X = velXZ.X;
        Velocity.Z = velXZ.Z;

        // --- Crouching ---
        var targetHeight = isCrouching ? CrouchHeight : PlayerHeight;
        var heightLerpSpeed = 20f;
        
        currentHeight = GMath.Lerp(currentHeight, targetHeight, heightLerpSpeed * Time.DeltaTime);
        
        // TODO: Crouching can sometimes get stuck in the ground??
        float heightDelta = currentHeight - lastHeight;
        Position.Y += heightDelta * 0.5f; // Move up/down by half the change, since bounding box is centered
        lastHeight = currentHeight;
        
        // --- Jumping ---
        if (isGrounded && isJumping)
        {
            Velocity.Y = JumpImpulse;
            isGrounded = false;
            justJumped = true;
        }
        
        // --- Fall damage ---
        if (justLanded)
        {
            if (fallTimer > 1f) // Falling for more than a second
                Damage(10);
            
            fallTimer = 0f;
        }
        
        if (Health <= 0)
            Kill();

        // Needs to happen a frame late
        if (NeedsToSpawn)
        {
            spawn();
            NeedsToSpawn = false;
        }
        
        wasGrounded = isGrounded;
    }

    public void Damage(int damage)
    {
        if (Health > 0)
            Health -= damage;
        
        //Raylib.PlaySound(fallSound);
    }

    public void Kill()
    {
        IsDead = true;
        canMove = false;
    }
    
    public void SetCameraDirection(Vector3 direction)
    {
        direction = Vector3.Normalize(direction);

        CameraYaw = MathF.Atan2(direction.X, direction.Z); // Or whatever your yaw convention is
        CameraPitch = -MathF.Asin(direction.Y); // Negative sign for proper pitch direction

        // Now construct CameraRotation as usual
        Quaternion rotYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, CameraYaw);
        Quaternion rotPitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, CameraPitch);

        CameraRotation = Quaternion.Normalize(rotYaw * rotPitch);
    }
    
    public void Draw()
    {
        handModel.Draw(Camera, CameraForward, CameraRight, CameraUp, CameraRotation, hotbar.SelectedItem);
    }

    public void DrawUI()
    {
        hotbar.Draw(Health);
        
        Debug.Draw2DText($"Position: {Position}", Color.White);
        Debug.Draw2DText($"Camera Direction: {CameraForward}", Color.White);
        Debug.Draw2DText($"IsFalling: {isFalling} IsGrounded: {isGrounded} WasGrounded: {wasGrounded}", Color.White);
        //Debug.Draw2DText($"Velocity: {Velocity}", Color.White);
        
        // TODO: Temporary death screen
        if (IsDead)
        {
            var deathScreen = new Rectangle(0f, 0f, Engine.ScreenWidth, Engine.ScreenHeight);
            Raylib.DrawRectangleRec(deathScreen, new Color(1f, 0f, 0f, 0.5f));
        }
    }
    
    private void run()
    {
        if (!isCrouching)
            isRunning = !isRunning;
    }
    
    private void checkCollisions()
    {
        var moveDelta = Velocity * Time.DeltaTime;
        var newPosition = Position;

        // Call once per frame before axis checks!
        var blockBoxes = getBlockBoxes(Position, 10f);

        // X axis
        newPosition.X += moveDelta.X;
        
        var playerBoxX = getBoundingBox(newPosition, currentHeight);
        var collidedX = blockBoxes.Any(box => Raylib.CheckCollisionBoxes(playerBoxX, box));
        
        if (collidedX)
        {
            newPosition.X -= moveDelta.X;
            Velocity.X = 0f;
            
            CollisionBox = playerBoxX;
        }

        // Y axis
        newPosition.Y += moveDelta.Y;
        
        var playerBoxY = getBoundingBox(newPosition, currentHeight);
        var collidedY = blockBoxes.Any(box => Raylib.CheckCollisionBoxes(playerBoxY, box));
        
        if (collidedY)
        {
            if (Velocity.Y < 0f)
                isGrounded = true;
            
            newPosition.Y -= moveDelta.Y;
            Velocity.Y = 0f;
            
            CollisionBox = playerBoxY;
        }

        // Z axis
        newPosition.Z += moveDelta.Z;
        
        var playerBoxZ = getBoundingBox(newPosition, currentHeight);
        var collidedZ = blockBoxes.Any(box => Raylib.CheckCollisionBoxes(playerBoxZ, box));
        
        if (collidedZ)
        {
            newPosition.Z -= moveDelta.Z;
            Velocity.Z = 0f;

            CollisionBox = playerBoxZ;
        }
        
        Position = newPosition;
    }
    
    private void spawn()
    {
        Console.WriteLine($"Spawning at {WorldSave.Data.PlayerPosition}");
        
        if (WorldSave.Exists)
        {
            Position = WorldSave.Data.PlayerPosition;
            SetCameraDirection(WorldSave.Data.CameraDirection);
        }
        else
            Position = spawnPosition;
        
        Velocity = Vector3.Zero;
    }
    
    private void respawn()
    {
        Position = spawnPosition;
        Velocity = Vector3.Zero;
    }
    
    private static List<BoundingBox> getBlockBoxes(Vector3 center, float radius)
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

            if (!ECSChunks.TryGetValue(chunkCoord, out var chunk))
                continue;
            
            int localX = x - chunkX;
            int localY = y - chunkY;
            int localZ = z - chunkZ;

            // Bounds check
            if (localX < 0 || localX >= ChunkSize ||
                localY < 0 || localY >= ChunkSize ||
                localZ < 0 || localZ >= ChunkSize)
                continue;

            var block = chunk.GetBlock(localX, localY, localZ);
            
            // Only add solid blocks
            if (block.Type != BlockType.Air)
            {
                var blockMin = new Vector3(x, y, z);
                var blockMax = blockMin + Vector3.One;
                
                result.Add(new BoundingBox(blockMin, blockMax));
            }
        }
        
        return result;
    }
    
    // Player box size: width and depth ≈ 0.5m (Source player is 32 units wide ≈ 0.81m, but keep hitbox thin for simplicity)
    private BoundingBox getBoundingBox(Vector3 position, float height)
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
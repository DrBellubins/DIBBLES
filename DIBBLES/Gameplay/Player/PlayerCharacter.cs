using DIBBLES.Gameplay.Inventory;
using Microsoft.Xna.Framework;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;

using static DIBBLES.Terrain.TerrainGeneration;
//using Debug = DIBBLES.Utils.Debug;

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
    
    private Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
    
    // Systems
    public GVec3 Position = GVec3.Zero;
    public Vector3 Velocity = Vector3.Zero;
    
    public Camera3D Camera;

    public BoundingBox CollisionBox = new();

    public Quaternion CameraRotation = Quaternion.Identity;
    
    public Vector3 CameraForward = Vector3.Zero;
    public Vector3 CameraUp = Vector3.Zero;
    public Vector3 CameraRight = Vector3.Zero;
    
    public bool FreeCamEnabled = false;
    public Freecam freecam = new();

    public bool IsDead = false;
    public bool IsUIFrozen = false;
    
    public bool IsFrozen
    {
        get { return IsUIFrozen || IsDead; }
    }
    
    public bool ShouldUpdate = false;
    
    public float CameraPitch = 0f;
    public float CameraYaw = 0f;
    
    public bool IsSurvival = false;

    //private Sound fallSound;
    private HandModel handModel = new();
    
    private float currentSpeed = WalkSpeed;
    private float currentHeight = PlayerHeight;
    private float mouseSensitivity = 0.1f;

    private float placeBreakTimer = 0f;

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
        //fallSound = Resource.LoadSoundSpecial("pain.ogg");
        
        Camera = new Camera3D();
        Camera.Position = new GVec3(0.0d, PlayerHeight * 0.5d, 0.0d);
        Camera.Target = new Vector3(0.0f, PlayerHeight * 0.5f, 1.0f);
        Camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        Camera.Fov = 90.0f;
        Camera.SetPerspective();
        
        hotbar.Start();
        handModel.Start();
        
        Spawn();
        
        Commands.RegisterCommand("kill", "Kills the player", killCMD);
        Commands.RegisterCommand("spawn", "Respawns player at spawn point",  respawnCMD);
        Commands.RegisterCommand("heal", "Heals the player: /heal for full health", healCMD);
        Commands.RegisterCommand("tp", "Teleport to a position: /teleport x y z", teleportCMD);
        Commands.RegisterCommand("gm", "Toggle gamemode between creative and survival", gameModeCMD);
        
        CursorManager.LockCursor();

        // Update from UI state machine
        InventorySystem.StateMachine.OnUIStateChanged += state =>
        {
            IsUIFrozen = InventorySystem.StateMachine.IsAnyInventoryOpen;
        };
    }
    
    float lastHeight = PlayerHeight;
    
    public void Update()
    {
        var vec3Position = Position.ToVector3();
        Debug.Draw2DText($"Position: {vec3Position.X}, {vec3Position.Y}, {vec3Position.Z}", Color.White);
        Debug.Draw2DText($"Camera Direction: {CameraForward.X}, {CameraForward.Y}, {CameraForward.Z}", Color.White);
        Debug.Draw2DText($"IsFalling: {isFalling} IsGrounded: {isGrounded} IsRunning: {isRunning}", Color.White);
        
        hotbar.Update(IsDead, IsFrozen);
        
        if (!ShouldUpdate)
            return;
        
        // --- Block breaking and placing ---
        if (!IsFrozen)
        {
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
        }
        
        if (Input.FlyToggle() && !IsFrozen && !IsSurvival)
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
        isCrouching = !IsFrozen && Input.Crouch();

        // Run vs Crouch checks
        if (isCrouching)
            currentSpeed = CrouchSpeed;
        else if (isRunning)
            currentSpeed = RunSpeed;
        else
            currentSpeed = WalkSpeed;

        if (!IsFrozen)
            isJumping = Input.Jump();
        else
            isJumping = false;
        
        // --- Gravity  ---
        Velocity.Y -= Gravity * Time.DeltaTime;
        
        // Reset one-frame flags at the start of each frame
        justJumped = false;
        justLanded = false;
        
        // Collision detection
        if (!FreeCamEnabled)
        {
            checkCollisions();
        
            CollisionBox = getBoundingBox(Position, currentHeight); // Needs to be set after collision detection
        }
        
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
        
        if (!IsFrozen)
            lookDelta = Input.LookDelta;
        
        var lookDeltaX = lookDelta.X * mouseSensitivity;
        var lookDeltaY = lookDelta.Y * mouseSensitivity;

        CameraYaw += GMath.ToRadians(-lookDeltaX); // Yaw: left and right
        CameraPitch += GMath.ToRadians(lookDeltaY); // Pitch: up and down

        CameraPitch = Math.Clamp(CameraPitch, GMath.ToRadians(-89.9f), GMath.ToRadians(89.9f));

        Vector3 lookDirection = new Vector3(
            MathF.Sin(CameraYaw) * MathF.Cos(CameraPitch),
            -MathF.Sin(CameraPitch),
            MathF.Cos(CameraYaw) * MathF.Cos(CameraPitch)
        );
        
        SetCameraDirection(lookDirection);

        // Camera position
        Camera.Position = Position + new GVec3(0.0f, PlayerHeight * 0.49f, 0.0f);
        Camera.Target = Camera.Position.ToVector3() + CameraForward;
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
        
        if (!IsFrozen)
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
        
        wasGrounded = isGrounded;
    }

    public void SetHealth(int amount)
    {
        Health = amount;
    }
    
    public void Damage(int damage)
    {
        if (Health > 0 && IsSurvival)
            Health -= damage;
        
        //Raylib.PlaySound(fallSound);
    }

    public void Kill()
    {
        IsDead = true;
    }
    
    public void Spawn()
    {
        Console.WriteLine($"Spawning at {WorldSave.Data.PlayerPosition}");
        
        if (WorldSave.Exists)
        {
            Position = WorldSave.Data.PlayerPosition;
            SetCameraDirection(WorldSave.Data.CameraDirection);
            
            Camera.Position = Position + new GVec3(0.0f, PlayerHeight * 0.49f, 0.0f);
            Camera.Target = Camera.Position.ToVector3() + CameraForward;
            Camera.Up = CameraUp;
            
            Input.FlushLookDelta();
            
            spawnPosition = Position.ToVector3();
        }
        else
            Position = spawnPosition.ToGVec3();
        
        Velocity = Vector3.Zero;
    }

    public void Respawn()
    {
        Position = spawnPosition.ToGVec3();
        SetCameraDirection(WorldSave.Data.CameraDirection);

        Health = 100;
        IsDead = false;
        Velocity = Vector3.Zero;
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
        
        // Calculate camera direction
        CameraForward = Vector3.Transform(Vector3.UnitZ, CameraRotation); // Forward
        CameraUp = Vector3.Transform(Vector3.UnitY, CameraRotation);
        CameraRight = Vector3.Transform(-Vector3.UnitX, CameraRotation); // This has to be flipped for some reason...
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
        var blockBoxes = getBlockBoxes(Position.ToVector3(), 10f);

        // X axis
        newPosition.X += moveDelta.X;
        
        var playerBoxX = getBoundingBox(newPosition, currentHeight);
        var collidedX = blockBoxes.Any(box => box.Intersects(playerBoxX));
        
        if (collidedX)
        {
            newPosition.X -= moveDelta.X;
            Velocity.X = 0f;
            
            CollisionBox = playerBoxX;
        }

        // Y axis
        newPosition.Y += moveDelta.Y;
        
        var playerBoxY = getBoundingBox(newPosition, currentHeight);
        var collidedY = blockBoxes.Any(box => box.Intersects(playerBoxY));
        
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
        var collidedZ = blockBoxes.Any(box => box.Intersects(playerBoxZ));
        
        if (collidedZ)
        {
            newPosition.Z -= moveDelta.Z;
            Velocity.Z = 0f;

            CollisionBox = playerBoxZ;
        }
        
        Position = newPosition;
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

            if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
                continue;
            
            int localX = x - chunkX;
            int localY = y - chunkY;
            int localZ = z - chunkZ;

            // Bounds check
            if (localX < 0 || localX >= ChunkSize ||
                localY < 0 || localY >= ChunkSize ||
                localZ < 0 || localZ >= ChunkSize)
                continue;

            var blockType = chunk.GetTypeAt(localX, localY, localZ);
            
            // Only add solid blocks
            if (blockType != BlockType.Air &&
                !chunk.GetInfoAt(localX, localY, localZ).IsBillboard)
            {
                var blockMin = new Vector3(x, y, z);
                var blockMax = blockMin + Vector3.One;
                
                result.Add(new BoundingBox(blockMin, blockMax));
            }
        }
        
        return result;
    }
    
    private void UpdateClosestLightLevel()
    {
        var blockPos = new Vector3Int(
            (int)MathF.Floor((float)Position.X),
            (int)MathF.Floor((float)Position.Y),
            (int)MathF.Floor((float)Position.Z)
        );

        // Get the light level at this block (use global helper)
        byte lightLevel = Chunk.GetLightLevelGlobal(blockPos);

        closestLightLevel = lightLevel;
    }
    
    // Player box size: width and depth ≈ 0.5m (Source player is 32 units wide ≈ 0.81m, but keep hitbox thin for simplicity)
    private BoundingBox getBoundingBox(GVec3 position, float height)
    {
        GVec3 min = new GVec3(
            position.X - 0.25d,
            position.Y - height * 0.5d,
            position.Z - 0.25d
        );
        GVec3 max = new GVec3(
            position.X + 0.25d,
            position.Y + height * 0.5d,
            position.Z + 0.25d
        );
        
        return new BoundingBox(min.ToVector3(), max.ToVector3());
    }
    
    // Draw
    private static byte closestLightLevel = 0;
    public void Draw()
    {
        UpdateClosestLightLevel();
        handModel.Draw(Camera, CameraForward, CameraRight, CameraUp, CameraRotation, closestLightLevel, hotbar.SelectedItem);
    }

    public void DrawUI()
    {
        hotbar.Draw(Health);
        
        // TODO: Temporary death screen
        if (IsDead)
        {
            var deathScreen = new RectangleF(0f, 0f, Engine.ScreenWidth, Engine.ScreenHeight);
            
            UIBatch.DrawRect(deathScreen, new Color(1f, 0f, 0f, 0.5f));
        }
        
        // Draw cursor
        if (!IsFrozen)
            UIBatch.DrawCircle(Engine.ScreenWidth / 2f, Engine.ScreenHeight / 2f, 1f, Color.White);
    }
    
    // Commands
    private void killCMD(string[] args)
    {
        Kill();
        Chat.Write("Killed the player", ChatMessageType.Command);
    }
    
    private void respawnCMD(string[] args)
    {
        Respawn();
        Chat.Write($"Spawning at {WorldSave.Data.PlayerPosition}", ChatMessageType.Command);
    }
    
    private void healCMD(string[] args)
    {
        int healAmount = 0;

        if (args.Length != 1)
        {
            healAmount = 100;
            Chat.Write("Set player health to full health", ChatMessageType.Command);
        }
        else if (int.TryParse(args[0], out var amount))
        {
            healAmount = amount;
            Chat.Write($"Set player health to: {amount}", ChatMessageType.Command);
        }
        else
            Chat.Write("Usage: /heal amount", ChatMessageType.Error);
            
        SetHealth(healAmount);
    }

    private void teleportCMD(string[] args)
    {
        if (args.Length == 1 && args[0].Contains(','))
            args = args[0].Split(',');

        if (args.Length != 3 ||
            !double.TryParse(args[0], out var x) ||
            !double.TryParse(args[1], out var y) ||
            !double.TryParse(args[2], out var z))
        {
            Chat.Write("Usage: /teleport x y z", ChatMessageType.Error);
            return;
        }

        Position = new GVec3(x, y, z);
        Chat.Write($"Teleported to ({x}, {y}, {z})", ChatMessageType.Command);
    }

    private void gameModeCMD(string[] args)
    {
        IsSurvival = !IsSurvival;
        
        if (IsSurvival)
            Chat.Write("Set gamemode to survival",  ChatMessageType.Command);
        else
            Chat.Write("Set gamemode to creative",  ChatMessageType.Command);
    }
}

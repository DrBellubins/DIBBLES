using DIBBLES.Gameplay.Inventory;
using DIBBLES.Gameplay.Terrain;
using Microsoft.Xna.Framework;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Systems.Rendering;
using DIBBLES.Terrain;
using DIBBLES.Utils;

//using Debug = DIBBLES.Utils.Debug;

using static DIBBLES.Gameplay.Player.PlayerUtils;
using static DIBBLES.Gameplay.Player.PlayerCommands;

namespace DIBBLES.Gameplay.Player;

// TODO: Crouching freezes player
public class PlayerCharacter
{
    // HL2 movement values, converted to meters and m/s.
    public const float WalkSpeed = 3.619f;        // 361.9 Hu
    public const float RunSpeed = 6.096f * 1.3f;         // 609.6 Hu
    public const float CrouchSpeed = 2.54f * 0.5f;      // HL2 crouch speed ≈ 100 units/s
    public const float AirAcceleration = 10.0f;  // HL2 style air accel
    public const float GroundAcceleration = 10.0f; // HL2 style ground accel
    public const float GroundFriction = 1.075f;    // HL2 style ground friction
    public const float AirFriction = 0.0f;       // Less friction in air
    public const float Gravity = 20.32f * 1.5f;         // HL2 = 800 units/s² ≈ 20.32 m/s²
    public const float JumpImpulse = 3.048f * 3.8f;       // HL2 jump velocity ≈ 5 m/s
    public const float PlayerHeight = 1.83f;     // HL2 player height ≈ 72 units
    public const float CrouchHeight = 0.91f;     // HL2 crouch height ≈ 36 units
    
    public static bool CollisionBoxDebug = false;
    
    // Gameplay
    public int Health = 100;
    public Hotbar hotbar = new();
    
    // Systems
    public GVec3 Position = GVec3.Zero;
    public Vector3 Velocity = Vector3.Zero;
    
    public Camera3D Camera;

    public BoundingBox CollisionBox = new();

    public Quaternion CameraRotation = Quaternion.Identity;
    
    public Vector3 CameraForward = Vector3.Zero;
    public Vector3 CameraUp = Vector3.Zero;
    public Vector3 CameraRight = Vector3.Zero;

    public List<BoundingBox> CollisionBoxes = new();
    
    public bool FreeCamEnabled = true;
    public Freecam freecam = new();

    public bool IsDead = false;
    public bool Freeze = false;
    
    public bool IsFrozen
    {
        get { return Freeze || IsDead; }
    }
    
    public bool ShouldUpdate = false;
    
    public float CameraPitch = 0f;
    public float CameraYaw = 0f;
    
    public bool IsSurvival = false;
    
    public float CurrentSpeed = WalkSpeed;
    public float CurrentHeight = PlayerHeight;
    
    public bool IsRunning = false;
    public bool IsJumping = false;
    public bool IsFalling = false;
    public bool IsGrounded = false;
    public bool IsCrouching = false;

    private Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
    //private Sound fallSound;
    private HandModel handModel = new();

    private PlayerCommands playerCommands = new();

    private bool wasGrounded = false;
    
    private bool justJumped = false;
    private bool justLanded = false;
    
    private float fallTimer = 0f;
    
    private float mouseSensitivity = 0.1f;

    private float placeBreakTimer = 0f;
    
    public void Start()
    {
        //fallSound = Resource.LoadSoundSpecial("pain.ogg");
        
        Camera = new Camera3D();
        Camera.Position = new GVec3(0.0d, PlayerHeight * 0.5d, 0.0d);
        Camera.Target = new Vector3(0.0f, PlayerHeight * 0.5f, 1.0f);
        Camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        Camera.Fov = 90.0f;
        Camera.SetPerspective();

        playerCommands.Initialize();
        
        hotbar.Start();
        handModel.Start();
        
        Spawn();
        
        CursorManager.LockCursor();

        // Update from UI state machine
        InventorySystem.StateMachine.OnUIStateChanged += state =>
        {
            Interactions.PlayerFrozen = InventorySystem.StateMachine.IsAnyInventoryOpen;
        };
    }
    
    float lastHeight = PlayerHeight;
    
    public void Update()
    {
        var vec3Position = Position.ToVector3();
        Debug.Draw2DText($"Position: {vec3Position.X:F4}, {vec3Position.Y:F4}, {vec3Position.Z:F4}", Color.White);
        Debug.Draw2DText($"Camera Direction: {CameraForward.X:F4}, {CameraForward.Y:F4}, {CameraForward.Z:F4}", Color.White);
        Debug.Draw2DText($"IsFalling: {IsFalling} IsGrounded: {IsGrounded} IsRunning: {IsRunning}", Color.White);

        Freeze = Interactions.PlayerFrozen;
        
        hotbar.Update(IsDead, IsFrozen);
        
        if (!ShouldUpdate)
            return;
        
        // --- Block breaking and placing ---
        if (!IsFrozen)
        {
            placeBreakTimer += (float)Time.DeltaTime;
            
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
        
        IsGrounded = false; // Reset ground state 
        
        // --- Input ---
        Vector3 inputDir = Vector3.Zero;
        
        if (Input.MoveForward()) inputDir.Z += 1.0f;
        if (Input.MoveBackward()) inputDir.Z -= 1.0f;
        if (Input.MoveLeft()) inputDir.X -= 1.0f;
        if (Input.MoveRight()) inputDir.X += 1.0f;

        // Run
        if (Input.Run())
            IsRunning = true;
        else
            IsRunning = false;

        if (IsRunning && IsCrouching)
            IsRunning = false;
        
        // Crouching
        IsCrouching = !IsFrozen && Input.Crouch();

        // Run vs Crouch checks
        if (IsCrouching)
            CurrentSpeed = CrouchSpeed;
        else if (IsRunning)
            CurrentSpeed = RunSpeed;
        else
            CurrentSpeed = WalkSpeed;

        if (!IsFrozen)
            IsJumping = Input.Jump();
        else
            IsJumping = false;
        
        // --- Gravity  ---
        Velocity.Y -= Gravity * (float)Time.DeltaTime;
        
        // Reset one-frame flags at the start of each frame
        justJumped = false;
        justLanded = false;
        
        // Collision detection
        if (!FreeCamEnabled)
        {
            CheckCollisions();
        
            CollisionBox = GetBoundingBox(Position, CurrentHeight); // Needs to be set after collision detection
        }
        
        // Update falling state
        IsFalling = !IsGrounded && Velocity.Y < 0f;
        
        // Grounded/Landing checks
        if (IsGrounded && !wasGrounded) // Just landed
        {
            justLanded = true;
        }
        else if (!IsGrounded && wasGrounded && Velocity.Y < 0f) // Started falling
        {
            
        }
        
        if (IsFalling)
            fallTimer += (float)Time.DeltaTime;
        
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
        
        SetCameraDirection(this, lookDirection);

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
        float accel = IsGrounded ? GroundAcceleration : AirAcceleration;
        float friction = IsGrounded ? GroundFriction : AirFriction;

        Vector3 wishVel = Vector3.Zero;
        
        if (!IsFrozen)
            wishVel = wishDir * CurrentSpeed;
        
        Vector3 velXZ = new Vector3(Velocity.X, 0f, Velocity.Z);
        
        float wishSpeed = wishVel.Length();

        // HL2-style friction: Only apply friction when no input and grounded
        if (wishSpeed == 0 && IsGrounded)
        {
            float speed = velXZ.Length();
            
            if (speed != 0)
            {
                /*float drop = speed * friction * (float)Time.DeltaTime;
                float newSpeed = Math.Max(speed - drop, 0);
                velXZ *= (newSpeed / speed);*/
                velXZ /= friction;
            }
        }

        // HL2-style acceleration: Only accelerate toward wishDir when input is present
        if (wishSpeed > 0)
        {
            float currentSpeedInDir = Vector3.Dot(velXZ, wishDir);
            float addSpeed = wishSpeed - currentSpeedInDir;
            
            if (addSpeed > 0)
            {
                float accelSpeed = accel * (float)Time.DeltaTime * wishSpeed;
                
                if (accelSpeed > addSpeed) accelSpeed = addSpeed;
                    velXZ += wishDir * accelSpeed;
            }
        }

        // Relax speed cap for bunnyhopping
        if (velXZ.Length() > CurrentSpeed)
            velXZ = Vector3.Normalize(velXZ) * CurrentSpeed;

        Velocity.X = velXZ.X;
        Velocity.Z = velXZ.Z;

        // --- Crouching ---
        var targetHeight = IsCrouching ? CrouchHeight : PlayerHeight;
        var heightLerpSpeed = 20f;
        
        CurrentHeight = GMath.Lerp(CurrentHeight, targetHeight, heightLerpSpeed * (float)Time.DeltaTime);
        
        // TODO: Crouching can sometimes get stuck in the ground??
        float heightDelta = CurrentHeight - lastHeight;
        Position.Y += heightDelta * 0.5f; // Move up/down by half the change, since bounding box is centered
        lastHeight = CurrentHeight;
        
        // --- Jumping ---
        if (IsGrounded && IsJumping)
        {
            Velocity.Y = JumpImpulse;
            IsGrounded = false;
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
        
        wasGrounded = IsGrounded;
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
            SetCameraDirection(this, WorldSave.Data.CameraDirection);
            
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
        SetCameraDirection(this, WorldSave.Data.CameraDirection);

        Health = 100;
        IsDead = false;
        Velocity = Vector3.Zero;
    }
    
    private void run()
    {
        if (!IsCrouching)
            IsRunning = !IsRunning;
    }
    
    // Draw
    public void Draw()
    {
        UpdateClosestLightLevel(Position.ToVector3());
        handModel.Draw(Camera, CameraForward, CameraRight, CameraUp, CameraRotation, ClosestLightLevel, hotbar.SelectedItem);

        if (CollisionBoxDebug)
        {
            foreach (var collider in CollisionBoxes)
                Debug.DrawBox(collider.Min + new Vector3(1f), Vector3.One);
        }
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
}

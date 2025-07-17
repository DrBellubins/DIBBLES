using Raylib_cs;
using System.Numerics;
using DIBBLES.Utils;

namespace DIBBLES;

public class SrcPlayer
{
    // Movement constants (converted from Unreal's UPBPlayerMovement, in meters)
    public const float WalkSpeed = 2.8575f;      // 285.75 Hu (HL2 units)
    public const float RunSpeed = 3.619f;        // 361.9 Hu
    public const float SprintSpeed = 6.096f;     // 609.6 Hu
    public const float CrouchSpeed = 1.2063f;    // RunSpeed * 0.33333333
    public const float AirAcceleration = 10.0f;  // HL2 style air accel
    public const float GroundAcceleration = 10.0f; // HL2 style ground accel
    public const float GroundFriction = 8.0f;    // Unreal style ground friction
    public const float AirFriction = 0.0f;       // No friction in air by default
    public const float EdgeFrictionMultiplier = 2.0f; // Edge friction multiplier
    public const float EdgeFrictionDist = 0.3048f;   // 30.48 Hu
    public const float EdgeFrictionHeight = 0.6477f; // 64.77 Hu
    public const float Gravity = 20.32f;         // HL2 = 800 units/s² ≈ 20.32 m/s²
    public const float JumpImpulse = 3.048f * 2f;     // 304.8 Hu (Unreal's JumpZVelocity)
    public const float PlayerHeight = 1.83f;     // HL2 player height ≈ 72 units
    public const float CrouchHeight = 0.3429f;   // 34.29 Hu (Unreal's crouched height)
    public const float MaxStepHeight = 0.3429f;  // 34.29 Hu
    public const float MinStepHeight = 0.1f;     // 10 Hu
    public const float StepDownHeightFraction = 0.9f;
    public const float CrouchTime = 0.3f;        // Time to transition to crouch
    public const float UncrouchTime = 0.3f;      // Time to transition to uncrouch
    public const float CrouchJumpTime = 0.3f;    // Time for crouch jump
    public const float UncrouchJumpTime = 0.3f;  // Time for uncrouch jump
    public const float CrouchSlideBoostTime = 0.1f; // Crouch slide boost duration
    public const float CrouchSlideBoostMultiplier = 1.5f;
    public const float CrouchSlideSpeedRequirementMultiplier = 0.9f;
    public const float MinCrouchSlideBoost = SprintSpeed * CrouchSlideBoostMultiplier;
    public const float MaxCrouchSlideVelocityBoost = 6.0f;
    public const float MinCrouchSlideVelocityBoost = 2.7f;
    public const float CrouchSlideBoostSlopeFactor = 2.7f;
    public const float CrouchSlideCooldown = 1.0f;

    public Vector3 Position = new Vector3(0.0f, 0.0f, 0.0f);
    public Camera3D Camera;

    private float currentSpeed = RunSpeed;
    private float currentHeight = PlayerHeight;
    private float mouseSensitivity = 0.1f;
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;
    private Vector3 velocity = Vector3.Zero;
    private bool isGrounded = false;
    private bool isCrouching = false;
    private bool isInCrouchTransition = false;
    private bool isCrouchSliding = false;
    private bool deferCrouchSlideToLand = false;
    private float crouchSlideStartTime = 0.0f;
    private float moveSoundTime = 0.0f;
    private bool stepSide = false;
    private float currentStepHeight = MaxStepHeight;
    private float surfaceFriction = 1.0f;
    private bool hasEverLanded = false;

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
        // --- Handle Crouch ---
        var crouchKey = Raylib.IsKeyDown(KeyboardKey.C);
        bool wantsToCrouch = crouchKey;
        
        if (wantsToCrouch && !isInCrouchTransition)
        {
            Crouch();
        }
        else if (!wantsToCrouch && isCrouching)
        {
            UnCrouch(groundBox);
        }

        // Update crouch transition
        UpdateCrouching(Time.DeltaTime);

        Console.WriteLine(isCrouching);
        
        // --- Gravity & Vertical Movement ---
        velocity.Y -= Gravity * Time.DeltaTime;
        Position.Y += velocity.Y * Time.DeltaTime;

        // --- Ground Collision ---
        var playerBox = GetPlayerBox(Position, currentHeight);
        
        if (Raylib.CheckCollisionBoxes(playerBox, groundBox))
        {
            Position.Y = groundBox.Max.Y + currentHeight * 0.5f;
            velocity.Y = 0.0f;
            
            if (!isGrounded)
            {
                hasEverLanded = true;
                PlayJumpSound(false); // Play landing sound
                
                /*if (deferCrouchSlideToLand)
                {
                    deferCrouchSlideToLand = false;
                    StartCrouchSlide(groundBox);
                }*/
            }
            
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }

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
        Camera.Position = Position + new Vector3(0.0f, currentHeight * 0.5f, 0.0f);
        Camera.Target = Camera.Position + cameraDirection;

        // --- Movement Input ---
        Vector3 inputDir = Vector3.Zero;
        
        if (Raylib.IsKeyDown(KeyboardKey.W)) inputDir.Z += 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.S)) inputDir.Z -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.A)) inputDir.X -= 1.0f;
        if (Raylib.IsKeyDown(KeyboardKey.D)) inputDir.X += 1.0f;

        // Determine speed based on state
        bool isSprinting = Raylib.IsKeyDown(KeyboardKey.LeftShift) && !isCrouching;
        currentSpeed = isCrouching ? CrouchSpeed : (isSprinting ? SprintSpeed : RunSpeed);

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

        // --- Movement Physics ---
        float accel = isGrounded ? GroundAcceleration : AirAcceleration;
        float friction = isGrounded ? GroundFriction : AirFriction;

        // Apply edge friction
        if (isGrounded && inputDir == Vector3.Zero)
        {
            if (TraceEdge(groundBox))
            {
                friction *= EdgeFrictionMultiplier;
            }
        }

        Vector3 wishVel = wishDir * currentSpeed;
        Vector3 velXZ = new Vector3(velocity.X, 0f, velocity.Z);
        
        float wishSpeed = wishVel.Length();

        // Dynamic step height
        UpdateDynamicStepHeight(wishSpeed);

        // Apply friction when no input and grounded
        if (wishSpeed == 0 && isGrounded)
        {
            float speed = velXZ.Length();
            
            if (speed != 0)
            {
                float drop = speed * friction * surfaceFriction * Time.DeltaTime;
                float newSpeed = Math.Max(speed - drop, 0);
                velXZ *= (newSpeed / speed);
            }
        }

        // Apply crouch slide
        /*if (isCrouchSliding && isGrounded)
        {
            float timeDifference = Time.time - crouchSlideStartTime;
            Vector3 floorNormal = new Vector3(0, 1, 0); // Assuming flat ground for simplicity
            float slope = Vector3.Dot(GetForwardVector(), floorNormal);
            float newSpeed = Math.Max(MinCrouchSlideBoost, velocity.Length() * CrouchSlideBoostMultiplier);
            
            newSpeed = Math.Min(newSpeed, MaxCrouchSlideVelocityBoost);
    
            if (newSpeed > MinCrouchSlideBoost && slope < 0.0f)
            {
                newSpeed = Math.Clamp(newSpeed + CrouchSlideBoostSlopeFactor * (newSpeed - MinCrouchSlideBoost) * slope, MinCrouchSlideBoost, newSpeed);
            }
    
            float decay = MathHelper.Lerp(MaxCrouchSlideVelocityBoost, MinCrouchSlideVelocityBoost, Math.Clamp(timeDifference / CrouchSlideBoostTime, 0.0f, 1.0f));
            velXZ = GetForwardVector() * newSpeed * (1.0f + slope) * decay;
    
            if (velXZ.Length() < 0.1f)
            {
                StopCrouchSliding();
            }
        }
        else if (isGrounded)
        {
            StopCrouchSliding();
        }*/

        // Apply acceleration
        if (wishSpeed > 0)
        {
            float currentSpeedInDir = Vector3.Dot(velXZ, wishDir);
            float addSpeed = wishSpeed - currentSpeedInDir;
            
            if (addSpeed > 0)
            {
                float accelSpeed = accel * Time.DeltaTime * wishSpeed * surfaceFriction;
                accelSpeed = Math.Min(accelSpeed, addSpeed);
                velXZ += wishDir * accelSpeed;
            }
        }

        // Clamp horizontal speed
        if (velXZ.Length() > currentSpeed)
            velXZ = Vector3.Normalize(velXZ) * currentSpeed;

        velocity.X = velXZ.X;
        velocity.Z = velXZ.Z;

        Position.X += velocity.X * Time.DeltaTime;
        Position.Z += velocity.Z * Time.DeltaTime;

        // --- Jumping ---
        if (isGrounded && Raylib.IsKeyPressed(KeyboardKey.Space) && CanAttemptJump(groundBox))
        {
            velocity.Y = JumpImpulse;
            isGrounded = false;
            PlayJumpSound(true);
        }

        // Play movement sounds
        PlayMoveSound(Time.DeltaTime, groundBox);
    }

    private void Crouch()
    {
        if (!isInCrouchTransition && CanCrouch())
        {
            isInCrouchTransition = true;
            isCrouching = true; // Set immediately when crouch starts
            float forwardSpeed = Vector3.Dot(velocity, GetForwardVector());
            Vector3 normalizedVelocity = velocity.Length() > 0 ? Vector3.Normalize(velocity) : Vector3.Zero;
            float forwardAlignment = Vector3.Dot(normalizedVelocity, GetForwardVector());

            // Only trigger slide if moving mostly forward (within ~30 degrees) and speed is sufficient
            if (forwardSpeed >= SprintSpeed * CrouchSlideSpeedRequirementMultiplier && 
                forwardAlignment > 0.866f && // cos(30°) ≈ 0.866
                isGrounded)
            {
                //StartCrouchSlide(null);
            }
            else if (!isGrounded && velocity.Y < 0.0f)
            {
                deferCrouchSlideToLand = true;
            }
        }
    }

    private void UnCrouch(BoundingBox groundBox)
    {
        if (CanUnCrouch(groundBox))
        {
            isInCrouchTransition = true;
            isCrouching = false; // Set immediately when uncrouch starts
            //StopCrouchSliding();
        }
    }

    private void UpdateCrouching(float deltaTime)
    {
        if (!isInCrouchTransition)
            return;

        float targetHeight = isCrouching ? CrouchHeight : PlayerHeight;
        float targetTime = isCrouching ? (isGrounded ? CrouchTime : CrouchJumpTime) : (isGrounded ? UncrouchTime : UncrouchJumpTime);
    
        float fullCrouchDiff = PlayerHeight - CrouchHeight;
        float currentUnscaledHeight = currentHeight;
        float currentAlpha = 1.0f - (currentUnscaledHeight - CrouchHeight) / fullCrouchDiff;
    
        if (MathF.Abs(targetTime) < float.Epsilon)
        {
            currentHeight = targetHeight;
            // Adjust Position.Y to keep the player's feet on the ground
            if (!isCrouching) // Uncrouching
            {
                float heightDifference = PlayerHeight - currentUnscaledHeight;
                Position.Y += heightDifference * 0.5f;
            }
            isInCrouchTransition = false;
            return;
        }

        float targetAlphaDiff = deltaTime / targetTime;
        float targetAlpha = currentAlpha + (isCrouching ? targetAlphaDiff : -targetAlphaDiff); // Reverse direction for uncrouching

        if (targetAlpha >= 1.0f || Math.Abs(targetAlpha - 1.0f) < 0.0001f)
        {
            targetAlpha = 1.0f;
            isInCrouchTransition = false;
            
            // Adjust Position.Y to keep the player's feet on the ground
            if (!isCrouching) // Uncrouching
            {
                float heightDifference = PlayerHeight - currentUnscaledHeight;
                Position.Y += heightDifference * 0.5f;
            }
        }
        else if (targetAlpha <= 0.0f || Math.Abs(targetAlpha) < 0.0001f)
        {
            targetAlpha = 0.0f;
            isInCrouchTransition = false;
        }

        currentHeight = PlayerHeight - fullCrouchDiff * targetAlpha;
        currentHeight = Math.Max(currentHeight, 0.25f); // Ensure height doesn't go below radius
    }

    private bool CanCrouch()
    {
        return isGrounded || !isCrouching;
    }

    private bool CanUnCrouch(BoundingBox groundBox)
    {
        if (!isCrouching)
            return false;

        // Calculate the adjusted position for standing height
        float heightDifference = PlayerHeight - currentHeight;
        Vector3 standingPosition = Position + new Vector3(0.0f, heightDifference * 0.5f, 0.0f);

        // Check collision with the standing bounding box at the adjusted position
        var standingBox = GetPlayerBox(standingPosition, PlayerHeight);
        return !Raylib.CheckCollisionBoxes(standingBox, groundBox);
    }

    /*private void StartCrouchSlide(BoundingBox? groundBox)
    {
        if (crouchSlideStartTime + CrouchSlideCooldown > Time.time)
        {
            if (velocity.Length() >= MinCrouchSlideBoost)
                isCrouchSliding = true;
            return;
        }

        if (groundBox.HasValue)
        {
            float newSpeed = Math.Max(MinCrouchSlideBoost, velocity.Length() * CrouchSlideBoostMultiplier);
            newSpeed = Math.Min(newSpeed, MaxCrouchSlideVelocityBoost);
            
            Vector3 floorNormal = new Vector3(0, 1, 0); // Assuming flat ground
            float slope = Vector3.Dot(GetForwardVector(), floorNormal);
        
            if (newSpeed > MinCrouchSlideBoost && slope < 0.0f)
            {
                newSpeed = Math.Clamp(newSpeed + CrouchSlideBoostSlopeFactor * (newSpeed - MinCrouchSlideBoost) * slope, MinCrouchSlideBoost, newSpeed);
            }
        
            // Use forward vector for slide direction
            velocity = GetForwardVector() * newSpeed;
            crouchSlideStartTime = Time.time;
            isCrouchSliding = true;
        }
    }

    private void StopCrouchSliding()
    {
        isCrouchSliding = false;
        deferCrouchSlideToLand = false;
    }*/

    private bool CanAttemptJump(BoundingBox groundBox)
    {
        if (!isGrounded)
            return false;

        Vector3 floorNormal = new Vector3(0, 1, 0); // Assuming flat ground
        float floorZ = Vector3.Dot(floorNormal, Vector3.UnitY);
        float walkableFloorZ = 0.7f; // From Unreal's SetWalkableFloorZ
        
        return floorZ >= walkableFloorZ || Math.Abs(floorZ - walkableFloorZ) < 0.0001f;
    }

    private void UpdateDynamicStepHeight(float speed)
    {
        if (speed <= CrouchSpeed)
        {
            currentStepHeight = MaxStepHeight;
            return;
        }

        float speedScale = (speed - SprintSpeed * 1.7f) / (SprintSpeed * 2.5f - SprintSpeed * 1.7f);
        float speedMultiplier = Math.Clamp(speedScale, 0.0f, 1.0f);
        
        speedMultiplier *= speedMultiplier;
        
        if (isGrounded)
        {
            speedMultiplier = Math.Max((1.0f - surfaceFriction) * speedMultiplier, 0.0f);
        }
        
        currentStepHeight = MathHelper.Lerp(MaxStepHeight, MinStepHeight, speedMultiplier);
    }

    private bool TraceEdge(BoundingBox groundBox)
    {
        // Simplified edge detection: check if player is near an edge by tracing downward
        Vector3 traceStart = Position;
        
        if (velocity.Length() > 0)
            traceStart += Vector3.Normalize(velocity) * EdgeFrictionDist;
        else
            traceStart += GetForwardVector() * EdgeFrictionDist;

        traceStart.Y += currentHeight * 0.5f;
        Vector3 traceEnd = traceStart - new Vector3(0, EdgeFrictionHeight + currentHeight, 0);
        
        return !Raylib.GetRayCollisionBox(new Ray(traceStart, traceEnd - traceStart), groundBox).Hit;
    }

    private void PlayMoveSound(float deltaTime, BoundingBox groundBox)
    {
        if (moveSoundTime > 0.0f)
        {
            moveSoundTime = Math.Max(0.0f, moveSoundTime - 1000.0f * deltaTime);
            return;
        }

        float speed = velocity.Length();
        float walkSpeedThreshold = isCrouching ? CrouchSpeed : WalkSpeed;
        float sprintSpeedThreshold = isCrouching ? CrouchSpeed * 1.7f : SprintSpeed;

        bool playSound = (isGrounded || isCrouching) && speed >= walkSpeedThreshold && !isCrouchSliding;
        
        if (!playSound)
            return;

        bool isSprinting = speed >= sprintSpeedThreshold;
        moveSoundTime = isSprinting ? 300.0f : (isCrouching ? 500.0f : 400.0f);
        float moveSoundVolume = isSprinting ? 1.0f : 0.5f;
        
        if (isCrouching)
            moveSoundVolume *= 0.65f;

        // Placeholder for sound playback (Raylib sound loading would be needed)
        string soundFile = isSprinting ? "sprint_step.wav" : "walk_step.wav";
        // Note: Actual sound playback requires Resource.Load<Sound> and Raylib.PlaySound
        // Console.WriteLine($"Playing {soundFile} with volume {moveSoundVolume}");
        stepSide = !stepSide;
    }

    private void PlayJumpSound(bool isJump)
    {
        if (!hasEverLanded && !isJump)
            return;

        float moveSoundVolume = isJump ? (Raylib.IsKeyDown(KeyboardKey.LeftShift) ? 1.0f : 0.5f) : 0.5f;
        
        if (!isJump)
        {
            float fallSpeed = -velocity.Y;
            float minFallDamageSpeed = 10.0f; // Placeholder value
            if (fallSpeed > minFallDamageSpeed)
                moveSoundVolume = 1.0f;
            else if (fallSpeed > minFallDamageSpeed / 2.0f)
                moveSoundVolume = 0.85f;
            else
                moveSoundVolume = 0.5f;
        }
        
        if (isCrouching)
            moveSoundVolume *= 0.65f;

        string soundFile = isJump ? "jump.wav" : "land.wav";
        // Note: Actual sound playback requires Resource.Load<Sound> and Raylib.PlaySound
        // Console.WriteLine($"Playing {soundFile} with volume {moveSoundVolume}");
    }

    private Vector3 GetForwardVector()
    {
        var vec = new Vector3(
            MathF.Cos(MathHelper.ToRadians(cameraYaw)),
            0.0f,
            MathF.Sin(MathHelper.ToRadians(cameraYaw))
        );

        return Vector3.Normalize(vec);
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
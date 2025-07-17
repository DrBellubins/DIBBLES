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
    public const float MaxJumpHoldTime = 0.0f;   // No variable jump height
    public const int MaxJumpCount = 1;           // Single jump by default
    public const float MaxJumpTime = 0.6096f;    // Time to apex (derived from JumpZVelocity and Gravity)

    // Bunny hopping settings (emulating Unreal's console variables)
    public bool AutoBunnyHop = false;             // Emulates CVarAutoBHop
    public int JumpBoostMode = 2;                // Emulates CVarJumpBoost (1 = always boost, 2 = boost when aligned)
    public bool BunnyHoppingEnabled = true;      // Emulates CVarBunnyhop

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
    private bool isJumpPressed = false;
    private bool deferJumpStop = false;
    private bool isSprinting = false;
    private int jumpCurrentCount = 0;
    private float jumpKeyHoldTime = 0.0f;
    private float lastJumpBoostTime = 0.0f;

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
        else if (!wantsToCrouch && isInCrouchTransition && isCrouching)
        {
            UnCrouch(groundBox);
        }

        // Update crouch transition
        UpdateCrouching(Time.DeltaTime);

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
                if (deferCrouchSlideToLand)
                {
                    deferCrouchSlideToLand = false;
                    StartCrouchSlide(groundBox);
                }
                // Reset jump state on landing
                if (!isJumpPressed)
                {
                    ResetJumpState();
                }
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
        isSprinting = Raylib.IsKeyDown(KeyboardKey.LeftShift) && !isCrouching;
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
        if (isCrouchSliding && isGrounded)
        {
            float timeDifference = Time.time - crouchSlideStartTime;
            Vector3 floorNormal = new Vector3(0, 1, 0); // Assuming flat ground for simplicity
            float slope = Vector3.Dot(wishDir, floorNormal);
            float newSpeed = Math.Max(MinCrouchSlideBoost, velXZ.Length() * CrouchSlideBoostMultiplier);
            if (newSpeed > MinCrouchSlideBoost && slope < 0.0f)
            {
                newSpeed = Math.Clamp(newSpeed + CrouchSlideBoostSlopeFactor * (newSpeed - MinCrouchSlideBoost) * slope, MinCrouchSlideBoost, newSpeed);
            }
            float decay = MathHelper.Lerp(MaxCrouchSlideVelocityBoost, MinCrouchSlideVelocityBoost, Math.Clamp(timeDifference / CrouchSlideBoostTime, 0.0f, 1.0f));
            velXZ = Vector3.Normalize(velXZ) * newSpeed * (1.0f + slope) * decay;
            
            if (velXZ.Length() < 0.1f)
            {
                StopCrouchSliding();
            }
        }
        else if (isGrounded)
        {
            StopCrouchSliding();
        }

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

        // Clamp horizontal speed (unless bunny hopping is enabled)
        if (!BunnyHoppingEnabled || isCrouchSliding)
        {
            if (velXZ.Length() > currentSpeed)
                velXZ = Vector3.Normalize(velXZ) * currentSpeed;
        }

        velocity.X = velXZ.X;
        velocity.Z = velXZ.Z;

        Position.X += velocity.X * Time.DeltaTime;
        Position.Z += velocity.Z * Time.DeltaTime;

        // --- Jump Input Handling ---
        bool wasJumpPressed = isJumpPressed;
        isJumpPressed = Raylib.IsKeyDown(KeyboardKey.Space);

        if (isJumpPressed && !wasJumpPressed)
        {
            // Jump key pressed
            if (CanJump())
            {
                PerformJump(groundBox, wishDir);
            }
        }
        else if (!isJumpPressed && wasJumpPressed && !AutoBunnyHop && !deferJumpStop)
        {
            // Jump key released
            ResetJumpState();
        }

        // Clear deferJumpStop after processing
        if (deferJumpStop)
        {
            deferJumpStop = false;
        }

        // Auto bunny hop: attempt jump on landing if space is held
        if (AutoBunnyHop && isJumpPressed && isGrounded && CanJump())
        {
            PerformJump(groundBox, wishDir);
        }

        // Update jump hold time
        if (isJumpPressed && jumpCurrentCount > 0)
        {
            jumpKeyHoldTime += Time.DeltaTime;
        }

        // Play movement sounds
        PlayMoveSound(Time.DeltaTime, groundBox);
    }

    private void Crouch()
    {
        if (!isInCrouchTransition && CanCrouch())
        {
            isInCrouchTransition = true;
            float forwardSpeed = Vector3.Dot(velocity, GetForwardVector());
            if (forwardSpeed >= SprintSpeed * CrouchSlideSpeedRequirementMultiplier && isGrounded)
            {
                StartCrouchSlide(null);
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
            StopCrouchSliding();
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
            isCrouching = targetHeight == CrouchHeight;
            isInCrouchTransition = false;
            return;
        }

        float targetAlphaDiff = deltaTime / targetTime;
        float targetAlpha = currentAlpha + targetAlphaDiff;

        if (targetAlpha >= 1.0f || Math.Abs(targetAlpha - 1.0f) < 0.0001f)
        {
            targetAlpha = 1.0f;
            targetAlphaDiff = targetAlpha - currentAlpha;
            isCrouching = targetHeight == CrouchHeight;
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

        // Check if there's enough space to uncrouch
        var standingBox = GetPlayerBox(Position, PlayerHeight);
        return !Raylib.CheckCollisionBoxes(standingBox, groundBox);
    }

    private void StartCrouchSlide(BoundingBox? groundBox)
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
            Vector3 floorNormal = new Vector3(0, 1, 0); // Assuming flat ground
            float slope = Vector3.Dot(GetForwardVector(), floorNormal);
            if (newSpeed > MinCrouchSlideBoost && slope < 0.0f)
            {
                newSpeed = Math.Clamp(newSpeed + CrouchSlideBoostSlopeFactor * (newSpeed - MinCrouchSlideBoost) * slope, MinCrouchSlideBoost, newSpeed);
            }
            velocity = Vector3.Normalize(velocity) * newSpeed;
            crouchSlideStartTime = Time.time;
            isCrouchSliding = true;
        }
    }

    private void StopCrouchSliding()
    {
        isCrouchSliding = false;
        deferCrouchSlideToLand = false;
    }

    private bool CanJump()
    {
        bool canJump = true;

        if (jumpCurrentCount >= MaxJumpCount)
        {
            if (MaxJumpHoldTime <= 0.0f || jumpKeyHoldTime >= MaxJumpHoldTime)
            {
                canJump = false;
            }
            else
            {
                canJump = isJumpPressed && (isGrounded || jumpCurrentCount < MaxJumpCount);
            }
        }

        if (canJump && isGrounded)
        {
            Vector3 floorNormal = new Vector3(0, 1, 0); // Assuming flat ground
            float floorZ = Vector3.Dot(floorNormal, Vector3.UnitY);
            float walkableFloorZ = 0.7f; // From Unreal's SetWalkableFloorZ
            canJump = floorZ >= walkableFloorZ || Math.Abs(floorZ - walkableFloorZ) < 0.0001f;
        }

        return canJump;
    }

    private void PerformJump(BoundingBox groundBox, Vector3 inputDir)
    {
        if (!isGrounded || !CanAttemptJump(groundBox))
            return;

        velocity.Y = JumpImpulse;
        isGrounded = false;
        jumpCurrentCount++;
        if (isJumpPressed)
        {
            deferJumpStop = true;
        }
        PlayJumpSound(true);

        // Apply jump boost
        if (JumpBoostMode > 0 && Time.time >= lastJumpBoostTime + MaxJumpTime)
        {
            lastJumpBoostTime = Time.time;
            Vector3 facing = GetForwardVector();
            Vector3 currentVelXZ = new Vector3(velocity.X, 0, velocity.Z);
            Vector3 inputVel = inputDir * currentSpeed;

            float forwardSpeed = Vector3.Dot(inputVel, facing);
            float speedBoostPerc = isCrouching ? 0.1f : (isSprinting ? 0.1f : 0.5f);
            float speedAddition = Math.Abs(forwardSpeed * speedBoostPerc);
            float maxBoostedSpeed = currentSpeed * (1.0f + speedBoostPerc);
            float speedAdditionNoClamp = speedAddition;

            if (JumpBoostMode == 2)
            {
                // Only boost if input aligns with current movement
                float velDotInput = Vector3.Dot(Vector3.Normalize(currentVelXZ), inputDir);
                if (Math.Abs(velDotInput) < 0.01f)
                {
                    speedAddition = 0.0f;
                    speedAdditionNoClamp = 0.0f;
                }
            }

            float newSpeed = speedAddition + currentVelXZ.Length();
            if (newSpeed > maxBoostedSpeed)
            {
                speedAddition -= newSpeed - maxBoostedSpeed;
            }

            if (forwardSpeed < -currentSpeed * MathF.Sin(0.6981f))
            {
                speedAddition *= -1.0f;
                speedAdditionNoClamp *= -1.0f;
            }

            Vector3 jumpBoostedVel = currentVelXZ + facing * speedAddition;
            float jumpBoostedSizeSq = jumpBoostedVel.LengthSquared();

            if (BunnyHoppingEnabled)
            {
                Vector3 jumpBoostedUnclampVel = currentVelXZ + facing * speedAdditionNoClamp;
                float jumpBoostedUnclampSizeSq = jumpBoostedUnclampVel.LengthSquared();
                if (jumpBoostedUnclampSizeSq > jumpBoostedSizeSq)
                {
                    jumpBoostedVel = jumpBoostedUnclampVel;
                    jumpBoostedSizeSq = jumpBoostedUnclampSizeSq;
                }
            }

            if (currentVelXZ.LengthSquared() < jumpBoostedSizeSq)
            {
                velocity.X = jumpBoostedVel.X;
                velocity.Z = jumpBoostedVel.Z;
            }
        }
    }

    private void ResetJumpState()
    {
        jumpCurrentCount = 0;
        jumpKeyHoldTime = 0.0f;
        deferJumpStop = false;
    }

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

        string soundFile = isSprinting ? "sprint_step.wav" : "walk_step.wav";
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
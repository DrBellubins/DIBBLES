using Microsoft.Xna.Framework;
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Gameplay.Player;

public class Freecam
{
    private bool isRunning = false;
    
    public void Update(PlayerCharacter playerCharacter)
    {
        if (Chat.IsOpen)
            return;
        
        // Camera
        var lookDelta = Input.LookDelta;
        var lookDeltaX = lookDelta.X * 0.1f;
        var lookDeltaY = lookDelta.Y * 0.1f;

        playerCharacter.CameraYaw += GMath.ToRadians(-lookDeltaX); // Yaw: left and right
        playerCharacter.CameraPitch += GMath.ToRadians(lookDeltaY); // Pitch: up and down

        playerCharacter.CameraPitch = Math.Clamp(playerCharacter.CameraPitch, GMath.ToRadians(-90f), GMath.ToRadians(90f));

        // Build quaternion from yaw and pitch (yaw first, then pitch)
        Quaternion rotYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, playerCharacter.CameraYaw);
        Quaternion rotPitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, playerCharacter.CameraPitch);

        playerCharacter.CameraRotation = Quaternion.Normalize(rotYaw * rotPitch);

        // Calculate camera direction
        playerCharacter.CameraForward = Vector3.Transform(Vector3.UnitZ, playerCharacter.CameraRotation); // Forward
        playerCharacter.CameraUp = Vector3.Transform(Vector3.UnitY, playerCharacter.CameraRotation);
        playerCharacter.CameraRight = Vector3.Transform(-Vector3.UnitX, playerCharacter.CameraRotation); // This has to be flipped for some reason...

        // Camera position
        playerCharacter.Camera.Position = playerCharacter.Position + new Vector3(0.0f, PlayerCharacter.PlayerHeight * 0.5f, 0.0f);
        playerCharacter.Camera.Target = playerCharacter.Camera.Position + playerCharacter.CameraForward;
        playerCharacter.Camera.Up = playerCharacter.CameraUp;
        
        // Input
        Vector3 inputDir = Vector3.Zero;
        
        if (Input.MoveForward()) inputDir.Z += 1.0f;
        if (Input.MoveBackward()) inputDir.Z -= 1.0f;
        if (Input.MoveLeft()) inputDir.X -= 1.0f;
        if (Input.MoveRight()) inputDir.X += 1.0f;
        
        if (Input.Jump()) inputDir.Y += 1.0f;
        if (Input.Crouch()) inputDir.Y -= 1.0f;
        
        // Movement
        float currentMovespeed;

        if (Input.Run())
            isRunning = !isRunning;
        
        if (isRunning)
            currentMovespeed = 20f;
        else
            currentMovespeed = 5f;
        
        float moveSpeed = currentMovespeed * Time.DeltaTime;
        
        // Forward on XZ plane ignoring pitch
        Vector3 forwardXZ = new Vector3(
            MathF.Sin(playerCharacter.CameraYaw),
            0.0f,
            MathF.Cos(playerCharacter.CameraYaw)
        );

        // Right on XZ plane ignoring pitch
        Vector3 rightXZ = new Vector3(
            MathF.Cos(playerCharacter.CameraYaw),
            0.0f,
            -MathF.Sin(playerCharacter.CameraYaw)
        );
        
        Vector3 wishDir = (-rightXZ * inputDir.X) + (new Vector3(0f, inputDir.Y, 0)) + (forwardXZ * inputDir.Z);
        
        if (wishDir.Length() > 0)
            wishDir = Vector3.Normalize(wishDir);
        
        playerCharacter.Position += wishDir * moveSpeed;
    }
}
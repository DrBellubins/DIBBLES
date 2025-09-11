using System.Numerics;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay.Player;

public class Freecam
{
    public void Update(PlayerCharacter playerCharacter)
    {
        float currentMovespeed;

        if (Input.Run())
            currentMovespeed = 10f;
        else
            currentMovespeed = 5f;
        
        float moveSpeed = currentMovespeed * Time.DeltaTime;

        var direction = new Vector3();

        if (Input.MoveForward())
            playerCharacter.Position += playerCharacter.CameraForward * moveSpeed;
        
        if (Input.MoveBackward())
            playerCharacter.Position -= playerCharacter.CameraForward * moveSpeed;

        if (Input.MoveLeft())
            playerCharacter.Position -= playerCharacter.CameraRight * moveSpeed;
        
        if (Input.MoveRight())
            playerCharacter.Position += playerCharacter.CameraRight * moveSpeed;
        
        var lookDelta = Input.LookDelta();
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
    }
}
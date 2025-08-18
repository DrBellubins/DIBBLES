using System.Numerics;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay.Player;

public class Freecam
{
    public void Update(Player player)
    {
        float currentMovespeed;

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift))
            currentMovespeed = 60f;
        else
            currentMovespeed = 20f;
        
        float moveSpeed = currentMovespeed * Time.DeltaTime;

        var direction = new Vector3();

        if (Raylib.IsKeyDown(KeyboardKey.W))
            player.Position += player.CameraForward * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.S))
            player.Position -= player.CameraForward * moveSpeed;

        if (Raylib.IsKeyDown(KeyboardKey.A))
            player.Position -= player.CameraRight * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.D))
            player.Position += player.CameraRight * moveSpeed;
        
        var mouseDeltaX = Raylib.GetMouseDelta().X * 0.1f;
        var mouseDeltaY = Raylib.GetMouseDelta().Y * 0.1f;

        player.CameraYaw += GMath.ToRadians(-mouseDeltaX); // Yaw: left and right
        player.CameraPitch += GMath.ToRadians(mouseDeltaY); // Pitch: up and down

        player.CameraPitch = Math.Clamp(player.CameraPitch, GMath.ToRadians(-90f), GMath.ToRadians(90f));

        // Build quaternion from yaw and pitch (yaw first, then pitch)
        Quaternion rotYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.CameraYaw);
        Quaternion rotPitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, player.CameraPitch);

        player.CameraRotation = Quaternion.Normalize(rotYaw * rotPitch);

        // Calculate camera direction
        player.CameraForward = Vector3.Transform(Vector3.UnitZ, player.CameraRotation); // Forward
        player.CameraUp = Vector3.Transform(Vector3.UnitY, player.CameraRotation);
        player.CameraRight = Vector3.Transform(-Vector3.UnitX, player.CameraRotation); // This has to be flipped for some reason...

        // Camera position
        player.Camera.Position = player.Position + new Vector3(0.0f, Player.PlayerHeight * 0.5f, 0.0f);
        player.Camera.Target = player.Camera.Position + player.CameraForward;
        player.Camera.Up = player.CameraUp;
    }
}
using System.Numerics;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay.Player;

public class Freecam
{
    private Quaternion CameraRotation = Quaternion.Identity;
    
    private Vector3 CameraForward = Vector3.Zero;
    private Vector3 CameraUp = Vector3.Zero;
    private Vector3 CameraRight = Vector3.Zero;
    
    private float cameraPitch = 0f;
    private float cameraYaw = 0f;
    
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
            player.Position += CameraForward * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.S))
            player.Position -= CameraForward * moveSpeed;

        if (Raylib.IsKeyDown(KeyboardKey.A))
            player.Position -= CameraRight * moveSpeed;
        
        if (Raylib.IsKeyDown(KeyboardKey.D))
            player.Position += CameraRight * moveSpeed;
        
        var mouseDeltaX = Raylib.GetMouseDelta().X * 0.1f;
        var mouseDeltaY = Raylib.GetMouseDelta().Y * 0.1f;

        cameraYaw += GMath.ToRadians(-mouseDeltaX); // Yaw: left and right
        cameraPitch += GMath.ToRadians(mouseDeltaY); // Pitch: up and down

        cameraPitch = Math.Clamp(cameraPitch, GMath.ToRadians(-90f), GMath.ToRadians(90f));

        // Build quaternion from yaw and pitch (yaw first, then pitch)
        Quaternion rotYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, cameraYaw);
        Quaternion rotPitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, cameraPitch);

        CameraRotation = Quaternion.Normalize(rotYaw * rotPitch);

        // Calculate camera direction
        CameraForward = Vector3.Transform(Vector3.UnitZ, CameraRotation); // Forward
        CameraUp = Vector3.Transform(Vector3.UnitY, CameraRotation);
        CameraRight = Vector3.Transform(-Vector3.UnitX, CameraRotation); // This has to be flipped for some reason...

        // Camera position
        player.Camera.Position = player.Position + new Vector3(0.0f, Player.PlayerHeight * 0.5f, 0.0f);
        player.Camera.Target = player.Camera.Position + CameraForward;
        player.Camera.Up = CameraUp;
    }
}
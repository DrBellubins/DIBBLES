using Raylib_cs;
using System.Numerics;

namespace DIBBLES;

public class Engine
{
    public static void Initialize()
    {
        // Initialize window
        Raylib.InitWindow(1600, 900, "DIBBLES");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Q);
        
        // Start
        var player = new Player();
        player.Start();
        
        while (!Raylib.WindowShouldClose())
        {
            // --- Update ---
            player.Update(0f);
        
            // --- Draw ---
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.SkyBlue);
        
            Raylib.BeginMode3D(player.Camera);
        
            // Draw simple ground plane
            Raylib.DrawPlane(new Vector3(0.0f, 0.0f, 0.0f), new Vector2(50.0f, 50.0f), Color.Green);
        
            // Draw player representation (simple cube)
            //Raylib.DrawCube(playerPosition, 0.5f, 1.8f, 0.5f, Color.Red);
        
            Raylib.EndMode3D();
        
            // Draw simple crosshair
            Raylib.DrawRectangle(Raylib.GetScreenWidth() / 2 - 2, Raylib.GetScreenHeight() / 2 - 2, 4, 4, Color.White);
        
            Raylib.EndDrawing();
        }
        
        Raylib.CloseWindow();
    }
}
using Raylib_cs;
using System.Numerics;
using DIBBLES.Effects;

namespace DIBBLES;

public class Engine
{
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 900;
    public const int VirtualScreenWidth = 320;
    public const int VirtualScreenHeight = 240;
    
    public const int FPS = 60;
    public const float FrameTimestep = 1.0f / (float)FPS;
    
    public static bool IsRunning;
    
    public static void Initialize()
    {
        // Initialize window
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "DIBBLES");
        Raylib.SetTargetFPS(0);
        Raylib.SetExitKey(KeyboardKey.Q);
        
        var previousTimer = DateTime.Now;
        var currentTimer = DateTime.Now;

        var time = 0.0f;
        var deltaTime = 0.0f;

        IsRunning = true;
        
        // Setup ground material (temporary)
        var groundTexture = Raylib.LoadTexture("Assets/Textures/grass_dark.png");
        var groundMaterial = Raylib.LoadMaterialDefault();
        
        Raylib.SetMaterialTexture(ref groundMaterial, MaterialMapIndex.Albedo, groundTexture);
        
        // Create plane mesh
        var planeMesh = MeshUtils.GenMeshPlaneTiled(50.0f, 50.0f, 1, 1, 10.0f, 10.0f); // 50x50 plane, 1x1 subdivisions
        var planeModel = Raylib.LoadModelFromMesh(planeMesh);

        unsafe
        {
            planeModel.Materials[0] = groundMaterial; // Assign material to model
        }
        
        // Start
        var groundBox = new BoundingBox(
            new Vector3(-25.0f, 0f, -25.0f), // Slightly below y=0 for tolerance
            new Vector3(25.0f, 0.1f, 25.0f)
        );
        
        var player = new Player();
        player.Start();

        var crtEffect = new CRTEffect();
        crtEffect.Start();
        
        while (IsRunning)
        {
            if (Raylib.WindowShouldClose())
                Close();
            
            currentTimer = DateTime.Now;
            deltaTime = (currentTimer.Ticks - previousTimer.Ticks) / 10000000f;
            time += deltaTime;
            
            // --- Update ---
            player.Update(groundBox, deltaTime);
        
            // --- Draw ---
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
        
            crtEffect.DrawStart();
            
            Raylib.ClearBackground(Color.SkyBlue);
            Raylib.BeginMode3D(player.Camera);
        
            // Draw ground plane
            Raylib.DrawModel(planeModel, new Vector3(0.0f, 0.0f, 0.0f), 1.0f, Color.White);
        
            // Draw player representation (simple cube)
            //Raylib.DrawCube(playerPosition, 0.5f, 1.8f, 0.5f, Color.Red);
        
            Raylib.EndMode3D();
            
            crtEffect.DrawEnd();
            
            // Draw simple crosshair
            Raylib.DrawRectangle(Raylib.GetScreenWidth() / 2 - 2, Raylib.GetScreenHeight() / 2 - 2, 4, 4, Color.White);
        
            Raylib.DrawFPS(10, 10);
            
            Raylib.EndDrawing();
            
            previousTimer = currentTimer;
            
            Thread.Sleep((int)(FrameTimestep * 1000.0f));
        }
        
        // Unload resources
        Raylib.UnloadTexture(groundTexture);
        Raylib.UnloadModel(planeModel); // Also unloads the material
        
        Raylib.CloseWindow();
    }
    
    public static void Close()
    {
        IsRunning = false;
    }
}
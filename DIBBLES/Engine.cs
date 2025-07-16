using Raylib_cs;
using System.Numerics;
using DIBBLES.Effects;
using DIBBLES.Scenes;
using DIBBLES.Utils;

namespace DIBBLES;

public class Engine
{
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 900;
    public const int VirtualScreenWidth = 640;
    public const int VirtualScreenHeight = 480;
    
    public const int FPS = 60;
    public const float FrameTimestep = 1.0f / (float)FPS;
    
    public static bool IsRunning;
    public static List<Scene> Scenes = new List<Scene>();
    public static Font MainFont;
    
    public static void Initialize()
    {
        // Initialize window
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "DIBBLES");
        Raylib.SetTargetFPS(0);
        Raylib.SetExitKey(KeyboardKey.Q);
        
        var previousTimer = DateTime.Now;
        var currentTimer = DateTime.Now;

        IsRunning = true;
        
        MainFont = Raylib.LoadFont("Assets/Textures/romulus.png");
        
        // Scenes
        var testScene = new TestScene();
        
        // Start
        foreach (var scene in Scenes)
            scene.Start();
        
        while (IsRunning)
        {
            if (Raylib.WindowShouldClose())
                Close();
            
            currentTimer = DateTime.Now;
            Time.DeltaTime = (currentTimer.Ticks - previousTimer.Ticks) / 10000000f;
            Time.time += Time.DeltaTime;
            
            // --- Update ---
            foreach (var scene in Scenes)
                scene.Update();
        
            // --- Draw ---
            foreach (var scene in Scenes)
                scene.Draw();
            
            previousTimer = currentTimer;
            
            Thread.Sleep((int)(FrameTimestep * 1000.0f));
        }
        
        // Unload resources
        
        
        Raylib.CloseWindow();
    }
    
    public static void Close()
    {
        IsRunning = false;
    }
}
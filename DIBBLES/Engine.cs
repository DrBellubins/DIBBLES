using System.Diagnostics;
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

        var timer = new Stopwatch();
        timer.Start();
        
        long previousTicks = timer.ElapsedTicks;

        IsRunning = true;
        MainFont = Raylib.LoadFont("Assets/Textures/romulus.png");
        
        var testScene = new TestScene();
        
        foreach (var scene in Scenes)
            scene.Start();
        
        while (IsRunning)
        {
            if (Raylib.WindowShouldClose())
                Close();
            
            // Update and draw
            foreach (var scene in Scenes)
                scene.Update();
        
            foreach (var scene in Scenes)
                scene.Draw();
            
            // Calculate delta time after rendering
            long currentTicks = timer.ElapsedTicks;
            
            Time.DeltaTime = (currentTicks - previousTicks) / (float)Stopwatch.Frequency;
            Time.time += Time.DeltaTime;
            
            //Console.WriteLine($"DeltaTime: {Time.DeltaTime:F6} s, FPS: {1.0f / Time.DeltaTime:F2}");

            // Cap frame rate
            //long targetTicks = (long)(FrameTimestep * Stopwatch.Frequency);
            //while (timer.ElapsedTicks - previousTicks < targetTicks) {} // Spin-wait only

            previousTicks = timer.ElapsedTicks;
        }
        
        Raylib.CloseWindow();
    }
    
    public static void Close()
    {
        IsRunning = false;
    }
}
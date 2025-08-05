using System.Diagnostics;
using Raylib_cs;
using System.Numerics;
using DIBBLES.Effects;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using System.Threading;
using DIBBLES.Systems;

namespace DIBBLES;

public class Engine
{
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 900;
    public static readonly int VirtualScreenWidth = (int)((float)ScreenWidth / ScreenHeight * VirtualScreenHeight);
    //public const int VirtualScreenWidth = 640;
    public const int VirtualScreenHeight = 480;
    
    public const int FPS = 165;
    public const float FrameTimestep = 1.0f / (float)FPS;
    
    public static bool IsRunning;
    public static List<Scene> Scenes = new List<Scene>();
    public static Font MainFont;
    
    public static void Initialize()
    {
        // Initialize window
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "DIBBLES");
        Raylib.SetTargetFPS(0); // Disable Raylib's FPS cap
        Raylib.SetExitKey(KeyboardKey.Q);

        Raylib.InitAudioDevice();
        
        GMath.Init();
        
        var timer = new Stopwatch();
        timer.Start();
        
        long previousTicks = timer.ElapsedTicks;

        IsRunning = true;
        MainFont = Raylib.LoadFont("Assets/Textures/romulus.png");
        
        var testScene = new TestScene();
        //var bezierScene = new BezierMeshScene();
        //var pathTracerScene = new PathTracerScene();
        
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
            
            // Cap frame rate with optimized spin-wait
            long targetTicks = (long)(FrameTimestep * (double)Stopwatch.Frequency); // Use double for precision
            long beforeWait = timer.ElapsedTicks;
            long elapsedTicks = beforeWait - previousTicks;
            int spinCount = 0;
            
            while (elapsedTicks < targetTicks)
            {
                Thread.SpinWait(100); // Brief spin-wait to reduce CPU usage
                elapsedTicks = timer.ElapsedTicks - previousTicks;
                spinCount++;
            }
            
            long afterWait = timer.ElapsedTicks;
            
            // Calculate DeltaTime after spin-wait to include wait time
            Time.DeltaTime = (afterWait - previousTicks) / (float)Stopwatch.Frequency;
            Time.time += Time.DeltaTime;

            previousTicks = afterWait; // Update to the end of the frame
        }
        
        Raylib.CloseWindow();
    }
    
    public static void Close()
    {
        IsRunning = false;
    }
}
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Systems.Rendering;

namespace DIBBLES.Utils;

public class Debug
{
    private static List<(string, Color)> textBuffer2d = new();
    
    //private static Dictionary<Vector3, Vector3> debugBoxes = new();
    private static List<DebuBox> debugBoxes = new();
    
    // Cache for text textures: key is a combination of text and position
    private static Dictionary<(string Text, Vector3 Position), Texture2D> textTextureCache = new();

    public static bool ShowDebug { get; private set; } = true;
    public static bool ShowChunkDebug { get; private set; } = false;
    public static bool ShowLightDebug { get; private set; } = false;
    
    private static Stopwatch debugTimer = new();
    
    private static string logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");
    private static List<string> logLines = new();
    private static StreamWriter? logWriter;

    private static float logSaveTimer = 0f;
    private const float logSaveInterval = 0.5f;
    
    public static void Start()
    {
        if (!File.Exists(logPath))
            logWriter = File.CreateText(logPath);
        else
        {
            File.Delete(logPath);
            logWriter = File.CreateText(logPath);
        }
    }
    
    public static void Update(Camera3D camera)
    {
        // Log save interval
        logSaveTimer += Time.DeltaTime;

        if (logSaveTimer >= logSaveInterval)
        {
            logSaveTimer -= logSaveInterval; // keeps it consistent even if frame rate hiccups
            
            //Info("Saving log file...");
            File.WriteAllLines(logPath, logLines);
        }
    }

    // Dispose of anything before closing here
    public static void Close()
    {
        logWriter?.Close();
    }
    
    public static void Info(string info)
    {
        string output = $"[INFO] {info}";
        Console.WriteLine(output);
        logLines.Add(output);
        
        Trace.TraceInformation(output);
    }
    
    public static void Warning(string warning)
    {
        string output = $"[WARNING] {warning}";
        Console.WriteLine(output);
        logLines.Add(output);
        
        Trace.TraceWarning(output);
    }
    
    public static void Error(string error)
    {
        string output = $"[ERROR] {error}";
        Console.WriteLine(output);
        logLines.Add(output);

        File.WriteAllLines(logPath, logLines);
        throw new Exception(error);
    }

    private static string lastTimerName = string.Empty;
    public static void TimerStart(string name)
    {
        if (debugTimer.IsRunning)
            Error("Timer was not stopped before starting a new one! Crashing...");

        lastTimerName = name;
        
        Info($"Starting timer '{name}'");
        debugTimer.Restart();
    }
    
    public static void TimerStop()
    {
        if (!debugTimer.IsRunning)
        {
            Warning("Timer was stopped before ever starting! Call ignored...");
            return;
        }
        
        debugTimer.Stop();
        Info($"Stopped timer '{lastTimerName}' with {debugTimer.ElapsedTicks} ticks elapsed.");

        lastTimerName = string.Empty;
    }

    public static void Clear2D()
    {
        textBuffer2d.Clear();
    }
    
    public static void Draw2D()
    {
        if (ShowDebug)
        {
            int index = 0;
        
            foreach (var bufferText in textBuffer2d)
            {
                var text = bufferText.Item1;
                var color = bufferText.Item2;
                
                UIBatch.DrawString(Engine.MainFont, text, new Vector2(0f, index), color);
                index += 24;
            }
        }
    }

    public static void Draw3D()
    {
        var playerCamera = GameScene.PlayerCharacter.Camera;

        foreach (var box in debugBoxes)
        {
            if (playerCamera.InFrustum(box.Position, box.FrustumCullRadius))
            {
                Primatives3D.DrawCubeWiresThick(box.Position - (box.Size * 0.5f), 
                    box.Size.X, box.Size.Y, box.Size.Z, box.Color, box.Thickness);
            }
        }
        
        debugBoxes.Clear();
    }

    // Draw box Vector3
    public static void DrawBox(Vector3 position, Vector3 size, float thickness = 0.005f, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position, size, Color.White, thickness, frustumCullRadius);
        
        if (!debugBoxes.Contains(debugBox))
            debugBoxes.Add(debugBox);
    }
    
    public static void DrawBox(Vector3 position, Vector3 size, Color color, float thickness = 0.005f, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position, size, color, thickness, frustumCullRadius);
        
        if (!debugBoxes.Contains(debugBox))
            debugBoxes.Add(debugBox);
    }
    
    // Draw box Vector3Int
    public static void DrawBox(Vector3Int position, Vector3Int size, float thickness = 0.005f, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position.ToVector3(), size.ToVector3(), Color.White, thickness, frustumCullRadius);
        
        if (!debugBoxes.Contains(debugBox))
            debugBoxes.Add(debugBox);
    }
    
    public static void DrawBox(Vector3Int position, Vector3Int size, Color color, float thickness = 0.005f, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position.ToVector3(), size.ToVector3(), color, thickness, frustumCullRadius);
        
        if (!debugBoxes.Contains(debugBox))
            debugBoxes.Add(debugBox);
    }
    
    // Draw text
    public static void Draw2DText(string text, Color color)
    {
        textBuffer2d.Add((text, color));
    }
    
    public static void Draw2DText(string text)
    {
        textBuffer2d.Add((text, Color.White));
    }
    
    // TODO: Strings too long get cut off
    public static void Draw3DText(string text, Vector3 position, Color color, float scale = 1f)
    {
        // TODO: Monogame
        /*// Create a unique key for the text and position
        var cacheKey = (text, position);

        // Check if texture already exists in cache
        if (!textTextureCache.TryGetValue(cacheKey, out var imgTexture))
        {
            unsafe
            {
                // Measure text to get precise dimensions
                var textSize = Raylib.MeasureTextEx(Raylib.GetFontDefault(), text, 24, 1);
                var width = (int)textSize.X + 24; // Add padding
                var height = (int)textSize.Y + 24;
                
                // Create a blank image with a transparent background
                var textImg = Raylib.GenImageColor(width, height, new Color(0, 0, 0, 0));
                var bytes = Encoding.UTF8.GetBytes(text);
                
                fixed (byte* bytePtr = bytes)
                {
                    var sbytePtr = (sbyte*)bytePtr;
                    
                    // Draw text using the custom font
                    Raylib.ImageDrawTextEx(&textImg, Raylib.GetFontDefault(), sbytePtr, Vector2.Zero, 24, 1, color);
                }

                // Load texture from image
                imgTexture = Raylib.LoadTextureFromImage(textImg);

                // Cache the texture
                textTextureCache[cacheKey] = imgTexture;

                // Clean up the image
                Raylib.UnloadImage(textImg);
            }
        }
        
        // Draw the billboard with the cached texture
        Raylib.DrawBillboard(debugCamera, imgTexture, position, scale, Color.White);*/
    }
    
    public static void ToggleDebugCMD(string[] args)
    {
        ShowDebug = !ShowDebug;
        Chat.Write($"Toggled debug information: {ShowDebug}", ChatMessageType.Command);
    }
    
    public static void ToggleChunkDebugCMD(string[] args)
    {
        ShowChunkDebug = !ShowChunkDebug;
        Chat.Write($"Toggled chunk border debug: {ShowChunkDebug}", ChatMessageType.Command);
    }
    
    public static void ToggleLightDebugCMD(string[] args)
    {
        ShowLightDebug = !ShowLightDebug;
        Chat.Write($"Toggled light level debug: {ShowLightDebug}", ChatMessageType.Command);
    }
}

public struct DebuBox : IEquatable<DebuBox>
{
    public Vector3 Position;
    public Vector3 Size;
    public Color Color;

    public float Thickness;
    public float FrustumCullRadius;

    public DebuBox(Vector3 position, Vector3 size, Color color, float thickness = 0.005f, float frustumCullRadius = 1f)
    {
        Position = position;
        Size = size;
        Color = color;

        Thickness = thickness;
        FrustumCullRadius = frustumCullRadius;
    }

    public bool Equals(DebuBox other)
    {
        return Position.Equals(other.Position)
               && Size.Equals(other.Size)
               && Color.Equals(other.Color);
    }

    public override bool Equals(object? obj) => obj is DebuBox other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + Position.GetHashCode();
            hash = (hash * 31) + Size.GetHashCode();
            hash = (hash * 31) + Color.GetHashCode();
            
            return hash;
        }
    }

    public static bool operator ==(DebuBox left, DebuBox right) => left.Equals(right);
    public static bool operator !=(DebuBox left, DebuBox right) => !left.Equals(right);
}
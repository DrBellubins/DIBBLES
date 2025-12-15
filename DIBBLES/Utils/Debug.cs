using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using DIBBLES.Systems;

namespace DIBBLES.Utils;

public class Debug
{
    private static Camera3D debugCamera;

    private static List<(string, Color)> textBuffer2d = new();
    
    //private static Dictionary<Vector3, Vector3> debugBoxes = new();
    private static List<DebuBox> debugBoxes = new();
    
    // Cache for text textures: key is a combination of text and position
    private static Dictionary<(string Text, Vector3 Position), Texture2D> textTextureCache = new();

    public static bool ShowDebug { get; private set; } = true;
    public static bool ShowChunkDebug { get; private set; } = false;
    public static bool ShowLightDebug { get; private set; } = true;
    
    public static void Update(Camera3D camera)
    {
        debugCamera = camera;
    }

    public static void Clear2D()
    {
        textBuffer2d.Clear();
    }
    
    public static void ToggleDebug(string[] args)
    {
        ShowDebug = !ShowDebug;
        Chat.Write("Toggled debug information", ChatMessageType.Command);
    }
    
    public static void ToggleDebugExtended(string[] args)
    {
        ShowChunkDebug = !ShowChunkDebug;
        Chat.Write("Toggled extended debug information", ChatMessageType.Command);
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
                    box.Size.X, box.Size.Y, box.Size.Z, box.Color, 0.005f);
            }
        }
        
        debugBoxes.Clear();
    }

    // Draw box Vector3
    public static void DrawBox(Vector3 position, Vector3 size, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position, size, Color.White, frustumCullRadius);
        
        if (!debugBoxes.Contains(debugBox))
            debugBoxes.Add(debugBox);
    }
    
    public static void DrawBox(Vector3 position, Vector3 size, Color color, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position, size, color, frustumCullRadius);
        
        if (!debugBoxes.Contains(debugBox))
            debugBoxes.Add(debugBox);
    }
    
    // Draw box Vector3Int
    public static void DrawBox(Vector3Int position, Vector3Int size, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position.ToVector3(), size.ToVector3(), Color.White, frustumCullRadius);
        
        if (!debugBoxes.Contains(debugBox))
            debugBoxes.Add(debugBox);
    }
    
    public static void DrawBox(Vector3Int position, Vector3Int size, Color color, float frustumCullRadius = 1f)
    {
        var debugBox = new DebuBox(position.ToVector3(), size.ToVector3(), color, frustumCullRadius);
        
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
}

public struct DebuBox : IEquatable<DebuBox>
{
    public Vector3 Position;
    public Vector3 Size;
    public Color Color;

    public float FrustumCullRadius;

    public DebuBox(Vector3 position, Vector3 size, Color color, float frustumCullRadius = 1f)
    {
        Position = position;
        Size = size;
        Color = color;

        FrustumCullRadius = frustumCullRadius;
    }

    public bool Equals(DebuBox other)
    {
        return Position.Equals(other.Position)
               && Size.Equals(other.Size)
               && Color.Equals(other.Color);
    }

    public override bool Equals(object obj) => obj is DebuBox other && Equals(other);

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
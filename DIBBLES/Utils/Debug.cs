using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;
using DIBBLES.Gameplay;
using DIBBLES.Scenes;
using DIBBLES.Systems;

namespace DIBBLES.Utils;

public class Debug
{
    private static Camera3D debugCamera;

    private static Dictionary<string, Color> textBuffer2d = new();
    
    private static Dictionary<Vector3, Vector3> debugBoxes = new();
    
    // Cache for text textures: key is a combination of text and position
    private static Dictionary<(string Text, Vector3 Position), Texture2D> textTextureCache = new();

    public static bool ShowDebug { get; private set; } = true;
    public static bool ShowDebugExtended { get; private set; }
    
    public static void Update(Camera3D camera)
    {
        debugCamera = camera;
        textBuffer2d.Clear();
    }

    public static void ToggleDebug(string[] args)
    {
        ShowDebug = !ShowDebug;
        Chat.Write(ChatMessageType.Command, "Toggled debug information");
    }
    
    public static void ToggleDebugExtended(string[] args)
    {
        ShowDebugExtended = !ShowDebugExtended;
        Chat.Write(ChatMessageType.Command, "Toggled extended debug information");
    }
    
    public static void Draw2D()
    {
        var sprites = Engine.Sprites;
        
        if (ShowDebug)
        {
            int index = 0;
        
            foreach (var text in textBuffer2d)
            {
                sprites.DrawString(Engine.MainFont, text.Key, new Vector2(0f, index), Color.White);
                
                // TODO: Monogame
                //Raylib.DrawTextEx(MonoEngine.MainFont, text.Key, new Vector2(0f, index), 24f, 1f, text.Value);
                index += 24;
            }
        }
    }

    public static void Draw3D()
    {
        if (ShowDebug)
        {
            // TODO: Monogame
            //foreach (var box in debugBoxes)
            //    Raylib.DrawCubeV(box.Key, box.Value, new Color(1f, 0f, 0f, 0.5f));
        
            debugBoxes.Clear();
        }
    }

    public static void DrawBox(Vector3 position, Vector3 size)
    {
        debugBoxes.TryAdd(position, size);
    }
    
    public static void Draw2DText(string text, Color color)
    {
        textBuffer2d.Add(text, color);
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
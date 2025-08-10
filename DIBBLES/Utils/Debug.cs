using System.Numerics;
using System.Text;
using Raylib_cs;

namespace DIBBLES.Utils;

public class Debug
{
    private static Camera3D debugCamera;

    // Cache for text textures: key is a combination of text and position
    private static Dictionary<(string Text, Vector3 Position), Texture2D> textTextureCache = new Dictionary<(string, Vector3), Texture2D>();
    
    public static void Update(Camera3D camera)
    {
        debugCamera = camera;
    }
    
    public static void Draw3DText(string text, Vector3 position, Color color, int fontSize = 24)
    {
        // Create a unique key for the text and position
        var cacheKey = (text, position);

        // Check if texture already exists in cache
        if (!textTextureCache.TryGetValue(cacheKey, out var imgTexture))
        {
            unsafe
            {
                // Measure text to get precise dimensions
                var textSize = Raylib.MeasureTextEx(Raylib.GetFontDefault(), text, fontSize, 1);
                var width = (int)textSize.X + fontSize; // Add padding
                var height = (int)textSize.Y + fontSize;
                
                // Create a blank image with a transparent background
                var textImg = Raylib.GenImageColor(width, height, new Color(0, 0, 0, 0));
                var bytes = Encoding.UTF8.GetBytes(text);
                
                fixed (byte* bytePtr = bytes)
                {
                    var sbytePtr = (sbyte*)bytePtr;
                    
                    // Draw text using the custom font
                    Raylib.ImageDrawTextEx(&textImg, Raylib.GetFontDefault(), sbytePtr, Vector2.Zero, fontSize, 1, color);
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
        Raylib.DrawBillboard(debugCamera, imgTexture, position, 1f, Color.White);
    }
    
    // TODO: Implement on-screen logging.
    public static void Log(string message)
    {
        throw new NotImplementedException();
    }
}
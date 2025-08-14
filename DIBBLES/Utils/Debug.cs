using System.Numerics;
using System.Text;
using Raylib_cs;

namespace DIBBLES.Utils;

public class Debug
{
    private static Camera3D debugCamera;

    private static Dictionary<string, Color> textBuffer2d = new ();
    
    // Cache for text textures: key is a combination of text and position
    private static Dictionary<(string Text, Vector3 Position), Texture2D> textTextureCache = new Dictionary<(string, Vector3), Texture2D>();
    
    public static void Update(Camera3D camera)
    {
        debugCamera = camera;
        textBuffer2d.Clear();
    }

    public static void Draw2D()
    {
        int index = 0;
        
        foreach (var text in textBuffer2d)
        {
            index += 24;
            Raylib.DrawTextEx(Engine.MainFont, text.Key, new Vector2(0f, index), 24f, 1f, text.Value);
        }
    }
    
    public static void Draw2DText(string text, Color color)
    {
        textBuffer2d.Add(text, color);
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
    
    /// <summary>
    /// Draws a cube wireframe at the given position, size, and color, with configurable line thickness.
    /// </summary>
    public static void DrawCubeWiresThick(Vector3 position, float width, float height, float length, Color color, float thickness = 0.02f)
    {
        // Calculate min/max corners
        Vector3 min = position - new Vector3(width, height, length) * 0.5f;
        Vector3 max = position + new Vector3(width, height, length) * 0.5f;

        // 8 corners of the cube
        Vector3[] corners = new Vector3[8];
        corners[0] = new Vector3(min.X, min.Y, min.Z);
        corners[1] = new Vector3(max.X, min.Y, min.Z);
        corners[2] = new Vector3(max.X, max.Y, min.Z);
        corners[3] = new Vector3(min.X, max.Y, min.Z);

        corners[4] = new Vector3(min.X, min.Y, max.Z);
        corners[5] = new Vector3(max.X, min.Y, max.Z);
        corners[6] = new Vector3(max.X, max.Y, max.Z);
        corners[7] = new Vector3(min.X, max.Y, max.Z);

        // 12 edges of the cube (pairs of indices)
        int[,] edges = new int[12, 2]
        {
            {0,1},{1,2},{2,3},{3,0},
            {4,5},{5,6},{6,7},{7,4},
            {0,4},{1,5},{2,6},{3,7}
        };

        // Draw each edge with thickness
        for (int i = 0; i < 12; i++)
        {
            DrawThickLine3D(corners[edges[i,0]], corners[edges[i,1]], color, thickness);
        }
    }

    /// <summary>
    /// Draws a 3D line with thickness as a quad facing the camera direction.
    /// </summary>
    private static void DrawThickLine3D(Vector3 start, Vector3 end, Color color, float thickness)
    {
        Vector3 dir = Vector3.Normalize(end - start);
        Vector3 camUp = new Vector3(0, 1, 0);
        Vector3 side = Vector3.Normalize(Vector3.Cross(dir, camUp));
        
        if (side.Length() < 0.01f)
        {
            side = Vector3.Normalize(Vector3.Cross(dir, new Vector3(1,0,0)));
        }
        
        side *= thickness * 0.5f;

        // 4 quad vertices
        Vector3 v1 = start + side;
        Vector3 v2 = start - side;
        Vector3 v3 = end - side;
        Vector3 v4 = end + side;

        // Draw the line as a quad (triangle pair)
        Raylib.DrawTriangle3D(v1, v2, v3, color);
        Raylib.DrawTriangle3D(v1, v3, v4, color);
    }
}
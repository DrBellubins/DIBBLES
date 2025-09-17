using System.Numerics;
using Raylib_cs;

namespace DIBBLES.Utils;

public class UI
{
    public static Color MainColor = new Color(0, 0, 0, 128);
    public static Color AccentColor = new Color(255, 255, 255, 128);
    public static Color SecondaryAccentColor = new Color(64, 64, 64, 128);
    public static Color FocusColor = new Color(128, 128, 128, 128);
    
    public static Vector2 CenterPivot
    {
        get { return new Vector2((float)Engine.ScreenWidth / 2f, (float)Engine.ScreenHeight / 2f); }
    }

    public static Vector2 TopCenterPivot
    {
        get { return new Vector2((float)Engine.ScreenWidth / 2f, 0f); }
    }
    
    public static Vector2 BottomCenterPivot
    {
        get { return new Vector2((float)Engine.ScreenWidth / 2f, (float)Engine.ScreenHeight); }
    }
    
    public static Vector2 LeftCenterPivot
    {
        get { return new Vector2(0f, (float)Engine.ScreenHeight / 2f); }
    }

    public static Vector2 RightCenterPivot
    {
        get { return new Vector2((float)Engine.ScreenWidth, (float)Engine.ScreenHeight / 2f); }
    }
    
    public static Vector2 TopLeftPivot
    {
        get { return new Vector2(0f, 0f); }
    }
    
    public static Vector2 TopRightPivot
    {
        get { return new Vector2((float)Engine.ScreenWidth, 0f); }
    }
    
    public static Vector2 BottomRightPivot
    {
        get { return new Vector2((float)Engine.ScreenWidth, (float)Engine.ScreenHeight); }
    }
    
    public static Vector2 BottomLeftPivot
    {
        get { return new Vector2(0f, (float)Engine.ScreenHeight); }
    }

    public static void DrawText(string text, Vector2 position)
    {
        Raylib.DrawTextEx(Engine.MainFont, text, position, 28, 0.0f, Color.White);
    }

    public static void DrawText(string text, float size, Vector2 position)
    {
        Raylib.DrawTextEx(Engine.MainFont, text, position, size, 0.0f, Color.White);
    }

    public static void DrawText(string text, Vector2 position, Color color)
    {
        Raylib.DrawTextEx(Engine.MainFont, text, position, 28, 0.0f, color);
    }

    public static void DrawText(string text, float size, Vector2 position, Color color)
    {
        Raylib.DrawTextEx(Engine.MainFont, text, position, size, 0.0f, color);
    }

    public static void DrawText(string text, float x, float y)
    {
        Raylib.DrawTextEx(Engine.MainFont, text, new Vector2(x, y), 28, 0.0f, Color.White);
    }

    public static void DrawText(string text, float size, float x, float y)
    {
        Raylib.DrawTextEx(Engine.MainFont, text, new Vector2(x, y), size, 0.0f, Color.White);
    }

    public static Rectangle CenterRect(Rectangle rect)
    {
        var outRect = new Rectangle(0.0f, 0.0f, rect.Width, rect.Height);

        outRect.X = CenterPivot.X - (rect.Width / 2f);
        outRect.Y = CenterPivot.Y - (rect.Height / 2f);

        return outRect;
    }

    public static Rectangle AlignRect(Rectangle rect, Vector2 alignment)
    {
        var outRect = new Rectangle(0.0f, 0.0f, rect.Width, rect.Height);

        outRect.X = alignment.X - (rect.Width / 2f);
        outRect.Y = alignment.Y - (rect.Height / 2f);

        return outRect;
    }
}
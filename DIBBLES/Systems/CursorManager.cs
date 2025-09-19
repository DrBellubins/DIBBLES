using Microsoft.Xna.Framework;

namespace DIBBLES.Systems;

public static class CursorManager
{
    public static bool IsLocked { get; private set; }
    
    private static Vector2 previousMousePosition = Vector2.Zero;
    
    public static void LockCursor()
    {
        previousMousePosition = InputMono.CursorPosition();

        //Raylib.HideCursor();
        IsLocked = true;;
    }

    public static void ReleaseCursor()
    {
        //Raylib.EnableCursor();
        //Raylib.SetMousePosition((int)previousMousePosition.X, (int)previousMousePosition.Y);
        
        //Raylib.ShowCursor();
        IsLocked = false;
    }

    public static void Update()
    {
        if (IsLocked)
        {
            //Raylib.SetMousePosition(MonoEngine.ScreenWidth / 2, MonoEngine.ScreenHeight / 2);
        }
    }
}
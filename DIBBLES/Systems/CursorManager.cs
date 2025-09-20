using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Systems;

public static class CursorManager
{
    public static bool IsLocked { get; private set; }

    private static Vector2 previousMousePosition = Vector2.Zero;

    public static void LockCursor()
    {
        // Save current mouse position in window coordinates
        var state = Mouse.GetState();
        previousMousePosition = new Vector2(state.X, state.Y);

        // Hide cursor
        Engine.Instance.IsMouseVisible = false; // You must provide a static reference to your MonoEngine/Game instance

        // Warp mouse to center
        Mouse.SetPosition(Engine.ScreenWidth / 2, Engine.ScreenHeight / 2);
        
        // Flush look delta
        Input.FlushLookDelta();
        
        IsLocked = true;
    }

    public static void ReleaseCursor()
    {
        // Show cursor
        Engine.Instance.IsMouseVisible = true;

        // Restore mouse position (optional, can be omitted for FPS-style controls)
        Mouse.SetPosition((int)previousMousePosition.X, (int)previousMousePosition.Y);
        
        IsLocked = false;
    }
}
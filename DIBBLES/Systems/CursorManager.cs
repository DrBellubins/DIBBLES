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
        MonoEngine.Instance.IsMouseVisible = false; // You must provide a static reference to your MonoEngine/Game instance

        IsLocked = true;
    }

    public static void ReleaseCursor()
    {
        // Show cursor
        MonoEngine.Instance.IsMouseVisible = true;

        // Restore mouse position (optional, can be omitted for FPS-style controls)
        Mouse.SetPosition((int)previousMousePosition.X, (int)previousMousePosition.Y);

        IsLocked = false;
    }

    public static void Update()
    {
        if (IsLocked)
        {
            // Recenter mouse every frame to the middle of the screen (typical FPS-style lock)
            Mouse.SetPosition(MonoEngine.ScreenWidth / 2, MonoEngine.ScreenHeight / 2);
        }
    }
}
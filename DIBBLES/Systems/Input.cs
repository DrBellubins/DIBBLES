using System.Numerics;
using Raylib_cs;

namespace DIBBLES.Systems;

public static class Input
{
    public static Vector2 LookDelta()
    {
        return Raylib.GetMouseDelta();
    }

    public static Vector2 CursorPosition()
    {
        return Raylib.GetMousePosition();
    }

    public static float ScrollDelta()
    {
        return Raylib.GetMouseWheelMove();
    }
    
    public static bool MoveForward()
    {
        return Raylib.IsKeyDown(KeyboardKey.W);
    }
    
    public static bool MoveBackward()
    {
        return Raylib.IsKeyDown(KeyboardKey.S);
    }
    
    public static bool MoveLeft()
    {
        return Raylib.IsKeyDown(KeyboardKey.A);
    }
    
    public static bool MoveRight()
    {
        return Raylib.IsKeyDown(KeyboardKey.D);
    }
    
    public static bool Run()
    {
        return Raylib.IsMouseButtonPressed(MouseButton.Extra);
    }

    public static bool Crouch()
    {
        return Raylib.IsKeyDown(KeyboardKey.LeftShift);
    }

    public static bool Jump(bool isCrouching)
    {
        return isCrouching ? Raylib.IsKeyPressed(KeyboardKey.Space) : Raylib.IsKeyDown(KeyboardKey.Space);
    }

    public static bool StartedBreaking => Raylib.IsMouseButtonPressed(MouseButton.Left);
    
    public static bool Break()
    {
        return Raylib.IsMouseButtonDown(MouseButton.Left);
    }
    
    public static bool StartedInteracting => Raylib.IsMouseButtonPressed(MouseButton.Right);
    
    public static bool Interact()
    {
        return Raylib.IsMouseButtonDown(MouseButton.Right);
    }

    public static bool Pause()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Escape);
    }
    
    public static bool OpenChat()
    {
        return Raylib.IsKeyPressed(KeyboardKey.T);
    }
    
    public static bool OpenChatCmd()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Slash);
    }
    
    public static bool SendChat()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Enter);
    }

    public static bool PreviousMessage()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Up);
    }
    
    public static bool NewerMessage()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Down);
    }
}
using System.Numerics;
using Raylib_cs;

namespace DIBBLES.Systems;

public static class Input
{
    public static Vector2 LookDelta()
    {
        return Raylib.GetMouseDelta();
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
        return Raylib.IsKeyDown(KeyboardKey.LeftShift);
    }

    public static bool Crouch()
    {
        return Raylib.IsKeyDown(KeyboardKey.C);
    }

    public static bool Jump(bool isCrouching)
    {
        return isCrouching ? Raylib.IsKeyPressed(KeyboardKey.Space) : Raylib.IsKeyDown(KeyboardKey.Space);
    }

    public static bool Break()
    {
        return Raylib.IsMouseButtonPressed(MouseButton.Left);
    }
    
    public static bool PlaceInteract()
    {
        return Raylib.IsMouseButtonPressed(MouseButton.Right);
    }
}
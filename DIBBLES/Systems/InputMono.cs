using System.Diagnostics;
using DIBBLES;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public static class InputMono
{
    private static KeyboardState _currentKey, _prevKey;
    private static MouseState _currentMouse, _prevMouse;
    private static Point _windowCenter = new Point(MonoEngine.ScreenWidth / 2, MonoEngine.ScreenHeight / 2);
    private static int _prevScrollWheel;
    
    public static Vector2 MouseDelta { get; private set; }
    
    public static void Update()
    {
        _prevKey = _currentKey;
        _currentKey = Keyboard.GetState();

        _prevMouse = _currentMouse;
        _currentMouse = Mouse.GetState();

        MouseDelta = new Vector2(_currentMouse.X - _prevMouse.X, _currentMouse.Y - _prevMouse.Y);
        _prevScrollWheel = _currentMouse.ScrollWheelValue;
    }

    // Movement keys
    public static bool MoveForward() => _currentKey.IsKeyDown(Keys.W);
    public static bool MoveBackward() => _currentKey.IsKeyDown(Keys.S);
    public static bool MoveLeft() => _currentKey.IsKeyDown(Keys.A);
    public static bool MoveRight() => _currentKey.IsKeyDown(Keys.D);

    public static bool Run() => IsMouseButtonPressed(ButtonType.XButton2);

    public static bool Crouch() => _currentKey.IsKeyDown(Keys.LeftShift);

    public static bool Jump(bool isCrouching)
    {
        return isCrouching ? IsKeyPressed(Keys.Space) : _currentKey.IsKeyDown(Keys.Space);
    }

    // Mouse input
    public static Vector2 LookDelta() => MouseDelta;
    public static Vector2 CursorPosition() => new Vector2(_currentMouse.X, _currentMouse.Y);
    
    public static float ScrollDelta()
    {
        return (_currentMouse.ScrollWheelValue - _prevMouse.ScrollWheelValue) / 120f; // 120 per notch
    }

    // Mouse buttons
    public static bool StartedBreaking => IsMouseButtonPressed(ButtonType.Left);
    public static bool Break() => _currentMouse.LeftButton == ButtonState.Pressed;
    public static bool StartedInteracting => IsMouseButtonPressed(ButtonType.Right);
    public static bool Interact() => _currentMouse.RightButton == ButtonState.Pressed;

    // Special keys
    public static bool Pause() => IsKeyPressed(Keys.Escape);

    public static bool OpenChat() => IsKeyPressed(Keys.T);
    public static bool OpenChatCmd() => IsKeyPressed(Keys.OemQuestion); // '/' on most layouts
    public static bool SendChat() => IsKeyPressed(Keys.Enter);

    public static bool Quit() => IsKeyPressed(Keys.Q);
    
    // Numeric keys for hotbar selection (useful in Hotbar.cs)
    public static int GetNumberKeyPressed()
    {
        for (int i = 0; i < 9; i++)
        {
            if (IsKeyPressed(Keys.D1 + i))
                return i;
        }
        
        return -1;
    }

    // Utility for single-frame key presses
    private static bool IsKeyPressed(Keys key) => _currentKey.IsKeyDown(key) && _prevKey.IsKeyUp(key);

    // Utility for single-frame mouse button presses
    private enum ButtonType { Left, Right, Middle, XButton1, XButton2 }
    
    private static bool IsMouseButtonPressed(ButtonType btn)
    {
        switch (btn)
        {
            case ButtonType.Left: return _currentMouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
            case ButtonType.Right: return _currentMouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;
            case ButtonType.Middle: return _currentMouse.MiddleButton == ButtonState.Pressed && _prevMouse.MiddleButton == ButtonState.Released;
            case ButtonType.XButton1: return _currentMouse.XButton1 == ButtonState.Pressed && _prevMouse.XButton1 == ButtonState.Released;
            case ButtonType.XButton2: return _currentMouse.XButton2 == ButtonState.Pressed && _prevMouse.XButton2 == ButtonState.Released;
            default: return false;
        }
    }
}
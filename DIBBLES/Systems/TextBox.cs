/*using Microsoft.Xna.Framework;
using DIBBLES.Systems;

namespace DIBBLES.Utils;

// Simple single-line text field with focus/click logic
public class TextBox
{
    public string Text { get; set; } = "";
    public bool IsFocused { get; set; } = false;
    public int MaxLength { get; set; } = 32;

    public Rectangle Bounds;

    public TextBox(Rectangle rect, int maxLength = 32)
    {
        Bounds = rect;
        MaxLength = maxLength;
    }

    public void Update()
    {
        Vector2 mousePos = Input.CursorPosition();
        bool mouseInBox = Raylib.CheckCollisionPointRec(mousePos, Bounds);

        // Click to focus
        if (Input.StartedBreaking)
            IsFocused = mouseInBox;

        if (IsFocused)
        {
            int c = Raylib.GetCharPressed();
            
            while (c > 0)
            {
                // Accept printable characters (ASCII 32..126), ignore others except backspace
                if (c >= 32 && c <= 126 && Text.Length < MaxLength)
                    Text += (char)c;

                c = Raylib.GetCharPressed();
            }
            
            // Handle backspace
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && Text.Length > 0)
                Text = Text[..^1];
        }
    }

    public void Draw()
    {
        // Draw box (different color if focused)
        Color boxColor = IsFocused ? UI.FocusColor : UI.SecondaryAccentColor;

        Raylib.DrawRectangleRec(Bounds, boxColor);

        // Draw text
        var padding = 8f;
        Vector2 textPos = new Vector2(Bounds.X + padding, Bounds.Y + padding);

        UI.DrawText(Text, 24, textPos, Color.White);

        // Draw blinking caret if focused
        if (IsFocused)
        {
            float time = Time.time;
            
            if ((int)(time * 2) % 2 == 0) // Blinks every 0.5s
            {
                // Get text width
                var textWidth = Raylib.MeasureTextEx(MonoEngine.MainFont, Text, 24, 0).X;
                
                float caretX = textPos.X + textWidth + 2f;
                float caretY = textPos.Y;
                float caretH = 24f;

                Raylib.DrawRectangle((int)caretX, (int)caretY, 2, (int)caretH, Color.White);
            }
        }
    }
}*/
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Text;
using DIBBLES.Systems;
using DIBBLES.Systems.Rendering;

namespace DIBBLES.Utils;

// Simple single-line text field with focus/click logic for MonoGame
public class TextBox
{
    public string Text { get; set; } = "";
    public bool IsFocused { get; set; } = false;
    public int MaxLength { get; set; } = 32;

    public RectangleF Bounds;
    
    private double caretBlinkTime = 0d;
    private bool showCaret = true;
    private int caretPos => Text.Length;

    public TextBox(RectangleF rect, int maxLength = 32)
    {
        Bounds = rect;
        MaxLength = maxLength;
        
        Engine.Instance.Window.TextInput += (s, e) =>
        {
            if (IsFocused)
                OnTextInput(e);
        };
    }

    public void Update()
    {
        MouseState mouse = Mouse.GetState();
        Point mousePos = mouse.Position;

        bool mouseInBox = Bounds.Contains(mousePos);
        
        // Click to focus
        if (mouse.LeftButton == ButtonState.Pressed && mouseInBox)
            IsFocused = true;
        else if (mouse.LeftButton == ButtonState.Pressed && !mouseInBox)
            IsFocused = false;

        // Blink caret
        caretBlinkTime += Time.DeltaTime;
        
        if (caretBlinkTime >= 0.5f)
        {
            showCaret = !showCaret;
            caretBlinkTime = 0;
        }
    }

    // Draw using MonoEngine.Sprites and MonoEngine.MainFont
    public void Draw()
    {
        // Draw box (different color if focused)
        Color boxColor = IsFocused ? UI.FocusColor : UI.AccentColor;
        
        UIBatch.DrawRect(Bounds, boxColor);

        // Draw text
        var padding = 8f;
        Vector2 textPos = new Vector2(Bounds.X + padding, Bounds.Y + padding);

        UIBatch.DrawString(Engine.MainFont, Text, textPos, Color.White);

        // Draw caret if focused
        if (IsFocused && showCaret)
        {
            // TODO: This offset is a hack, for some reason MeasureString is inaccurate.
            Vector2 textSize = Engine.MainFont.MeasureString(Text) * 0.93f; 
            
            float caretX = textPos.X + textSize.X;
            float caretY = textPos.Y;
            float caretH = Engine.MainFont.LineSpacing;

            UIBatch.DrawRect(new RectangleF(caretX, caretY, 2f, caretH), Color.White);
        }
    }

    public void OnTextInput(TextInputEventArgs e)
    {
        // Handle Backspace
        if (e.Character == '\b')
        {
            if (Text.Length > 0)
                Text = Text[..^1];
            
            return;
        }

        if (!char.IsControl(e.Character) && Text.Length < MaxLength)
            Text += e.Character.ToString();
    }

    public void Clear()
    {
        Text = string.Empty;
    }
}
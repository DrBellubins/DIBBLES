using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Text;
using DIBBLES.Systems;

namespace DIBBLES.Utils;

// Simple single-line text field with focus/click logic for MonoGame
public class TextBoxMono
{
    public string Text { get; set; } = "";
    public bool IsFocused { get; set; } = false;
    public int MaxLength { get; set; } = 32;

    public Rectangle Bounds;
    private StringBuilder textBuilder = new StringBuilder();
    
    private float caretBlinkTime = 0;
    private bool showCaret = true;
    private int caretPos => Text.Length;

    public TextBoxMono(Rectangle rect, int maxLength = 32)
    {
        Bounds = rect;
        MaxLength = maxLength;
        
        MonoEngine.Instance.Window.TextInput += (s, e) =>
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
        Color boxColor = IsFocused ? Color.Gray : Color.DarkGray;
        MonoEngine.Sprites.Draw(TextureUtils.GetWhitePixel(), Bounds, boxColor);

        // Draw text
        var padding = 8f;
        Vector2 textPos = new Vector2(Bounds.X + padding, Bounds.Y + padding);

        MonoEngine.Sprites.DrawString(MonoEngine.MainFont, Text, textPos, Color.White);

        // Draw caret if focused
        if (IsFocused && showCaret)
        {
            Vector2 textSize = MonoEngine.MainFont.MeasureString(Text);
            
            float caretX = textPos.X + textSize.X + 2f;
            float caretY = textPos.Y;
            float caretH = MonoEngine.MainFont.LineSpacing;

            MonoEngine.Sprites.Draw(TextureUtils.GetWhitePixel(),
                new Rectangle((int)caretX, (int)caretY, 2, (int)caretH), Color.White);
        }
    }

    public void OnTextInput(TextInputEventArgs e)
    {
        if (char.IsControl(e.Character) && !IsFocused) return; // Ignore control chars
        
        if (Text.Length < MaxLength)
            Text += e.Character;
    }
    
    // Utility: very basic key to char (for demo; for robust, see TextInput event)
    private static char? KeyToChar(Keys key, bool shift)
    {
        if (key >= Keys.A && key <= Keys.Z)
        {
            char c = (char)('a' + (key - Keys.A));
            
            return shift ? char.ToUpper(c) : c;
        }
        
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            char c = (char)('0' + (key - Keys.D0));
            
            return c;
        }
        
        if (key == Keys.Space)
            return ' ';
        
        return null;
    }
}
using System.Diagnostics;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Inventory;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Systems.DebugMenu;

// The (class type) button side menu.
// Options buttons are registered per class
public class DebugMenu
{
    // Group buttons by the Type that registered them
    private static readonly Dictionary<Type, Button> buttonsByOwner = new();
    
    public bool Open = true;
    
    private const float width = 400.0f;
    private RectangleF sideMenuRect;

    // Uniform button height
    private const float ButtonHeight = 150.0f;
    
    private const float ButtonPaddingW = 16.0f; // Width
    private const float ButtonPaddingH = 8.0f; // Height
    
    public void Start()
    {
        sideMenuRect = new RectangleF(UI.TopRightPivot.X - width, UI.TopRightPivot.Y, width, Engine.ScreenHeight);
    }

    public void Update()
    {
        if (Input.IsKeyPressed(Keys.U))
            Open = !Open;
        
        if (Open)
            Interactions.CloseMenusAndFreeze();
        else
            Interactions.Unfreeze();
    }

    public void Draw()
    {
        if (!Open)
            return;

        // Side menu background
        UIBatch.DrawRect(sideMenuRect, UI.MainColor);

        float sideMenuX = sideMenuRect.X;
        float sideMenuY = sideMenuRect.Y + ButtonPaddingH;
        float sideMenuWidth = sideMenuRect.Width;

        float buttonWidth = sideMenuWidth - ButtonPaddingW;
        
        // Draw buttons grouped by owner type
        foreach (var kv in buttonsByOwner)
        {
            var button = kv.Value;
            
            var widthPadding = ButtonPaddingW * 0.5f;
                
            button.Rect = new RectangleF(sideMenuX + widthPadding,
                sideMenuY, buttonWidth, ButtonHeight);
                
            UIBatch.DrawRect(button.Rect, UI.MainColor.Darken(0.2f)); // Use a different color if you have one for buttons

            var textPos = new Vector2(button.Rect.X + (buttonWidth * 0.5f), button.Rect.Y + (ButtonHeight * 0.5f));

            UIBatch.DrawCircle(textPos, 5f, Color.Blue);
                
            // If you have a text API in UIBatch, draw the label here using rect.X/Y for positioning.
            // Example (pseudo): UIBatch.DrawString(MainFont, label, new Vector2(rect.X + 8, rect.Y + 8), Color.White);
            UIBatch.DrawStringCentered(Engine.MainFont, button.Label, textPos, Color.White);

            sideMenuY += ButtonHeight + ButtonPaddingH;

            // Extra spacing between groups
            sideMenuY += ButtonPaddingH;
        }
    }
    
    // Register a button and auto-categorize by the calling class (no generics, no explicit type parameter)
    public static void CreateButton(string buttonName)
    {
        var callerType = new StackFrame(1, false).GetMethod()?.DeclaringType ?? typeof(DebugMenu);

        if (!buttonsByOwner.TryGetValue(callerType, out var button))
        {
            button = new Button(buttonName, new RectangleF());
            buttonsByOwner[callerType] = button;
        }
    }
}
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
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
    private static readonly Dictionary<string, MenuItem> menuItems = new();
    
    //private static readonly Dictionary<Type, Button> buttonsByOwner = new();
    //private static readonly Dictionary<Type, List<IDebugParam>> paramsByOwner = new();
    
    private static readonly Dictionary<Texture2D, IntPtr> imguiTextureIds = new();
    
    private static Func<Texture2D, IntPtr> _bindTextureFunc;
    
    public bool Open = true;
    
    private const float width = 400.0f;
    private RectangleF sideMenuRect;

    // Uniform button height
    private const float ButtonHeight = 150.0f;
    
    private const float ButtonPaddingW = 16.0f; // Width
    private const float ButtonPaddingH = 8.0f; // Height
    
    private string? activeOwner;
    
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
        else if (Interactions.Frozen)
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
        foreach (var item in menuItems.Values)
        {
            var button = item.Button;
            
            if (isButtonClicked(button.Rect))
            {
                activeOwner = item.Name;
                Open = true;
            }
            
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
    
    // Draw IMGUI window for the active owner:
    public void DrawIMGUI()
    {
        if (!Open || activeOwner == null)
            return;

        string title = $"{activeOwner} Debug";
        
        ImGui.Begin(title, ref Open, ImGuiWindowFlags.AlwaysAutoResize);

        if (menuItems.TryGetValue(activeOwner, out var item))
        {
            foreach (var param in item.Params)
                param.Draw();
        }
        else
            ImGui.TextDisabled("No registered parameters for this type.");

        ImGui.End();
    }
    
    // Setter to provide the bind function from ImGui renderer
    public static void SetBindTextureFunc(Func<Texture2D, IntPtr> bindFunc)
    {
        _bindTextureFunc = bindFunc;
    }

    public static IntPtr BindImGuiTexture(Texture2D tex)
    {
        if (tex == null || _bindTextureFunc == null)
            return IntPtr.Zero;

        if (!imguiTextureIds.TryGetValue(tex, out var id))
        {
            id = _bindTextureFunc(tex);
            imguiTextureIds[tex] = id;
        }

        return id;
    }
    
    public static void RegisterMenuItem(string name,  params IDebugParam[] _params)
    {
        var button = new Button(name, new RectangleF());
        var menuItem = new MenuItem(name, button, _params.ToList());
        
        menuItems.Add(name, menuItem);
    }
    
    // Optional: helper to wrap bind function for TextureDisplayParam creation
    public static Func<Texture2D, IntPtr> GetBindTextureFunc()
    {
        return tex =>
        {
            if (tex == null || _bindTextureFunc == null)
                return IntPtr.Zero;

            if (!imguiTextureIds.TryGetValue(tex, out var id))
            {
                id = _bindTextureFunc(tex);
                imguiTextureIds[tex] = id;
            }
            
            return id;
        };
    }
    
    // Simple click helper (UIBatch rectangles)
    private bool isButtonClicked(RectangleF rect)
    {
        var ms = Mouse.GetState();
        var mp = new Vector2(ms.X, ms.Y);

        bool inside =
            mp.X >= rect.X &&
            mp.X <= rect.X + rect.Width &&
            mp.Y >= rect.Y &&
            mp.Y <= rect.Y + rect.Height;

        return inside && ms.LeftButton == ButtonState.Pressed;
    }
}
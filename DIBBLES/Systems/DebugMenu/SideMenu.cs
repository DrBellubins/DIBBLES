using DIBBLES.Utils;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Systems.DebugMenu;

// The (class type) button side menu.
// Options buttons are registered per class
public class SideMenu
{
    private static List<string> buttons = new();
    //public Dictionary<string, >
    
    public bool Opened = false;
    
    private const float width = 400.0f;
    private RectangleF sideMenuRect;
    
    public void Start()
    {
        sideMenuRect = new RectangleF(UI.TopRightPivot.X - width, UI.TopRightPivot.Y, width, Engine.ScreenHeight);
    }

    public void Update()
    {
        if (Input.IsKeyPressed(Keys.U))
            Opened = !Opened;
    }

    public void Draw()
    {
        if (Opened)
            UIBatch.DrawRect(sideMenuRect, UI.MainColor);
    }
    
    public static void CreateButton(string buttonName)
    {
        buttons.Add(buttonName);
    }
}
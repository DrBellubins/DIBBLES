namespace DIBBLES.Systems.DebugMenu;

// The master class for all things relating to the debug
// menu system. Holds: SideMenu, Windows, etc.
public class DebugMenu
{
    private static SideMenu sideMenu = new();
    
    public void Start()
    {
        sideMenu.Start();
    }
    
    public void Update()
    {
        sideMenu.Update();
    }

    public void Draw()
    {
        sideMenu.Draw();
    }

    public static void Register(string buttonName)
    {
        SideMenu.CreateButton(buttonName);
    }
}
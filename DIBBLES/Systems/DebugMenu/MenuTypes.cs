using DIBBLES.Utils;

namespace DIBBLES.Systems.DebugMenu;

public class Button
{
    public string Label;
    public RectangleF Rect;
    
    public Button(string label, RectangleF rect)
    {
        Label = label;
        Rect = rect;
    }
}
using DIBBLES.Scenes;
using DIBBLES.Utils;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.ImGuiNet;

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

public class MenuItem
{
    public string Name;
    public Button Button;
    public List<IDebugParam> Params;
    
    public MenuItem(string name, Button button, List<IDebugParam> _params)
    {
        Name = name;
        Button = button;
        Params = _params;
    }
}

// IMGUI param interface
public interface IDebugParam
{
    void Draw();
}

public sealed class SeparatorParam : IDebugParam
{
    private readonly string _label;

    public SeparatorParam(string label)
    {
        _label = label;
    }
    
    public void Draw()
    {
        ImGui.SeparatorText(_label);
    }
}

public sealed class DropdownParam : IDebugParam
{
    private readonly string _label;
    private readonly int _selectionIndex;
    private readonly string[] _items;
    private readonly Func<int> _get;
    private readonly Action<int> _set;

    public DropdownParam(string label, string[] itmes, int selectionIndex, Func<int> getter, Action<int> setter)
    {
        _label = label;
        _selectionIndex = selectionIndex;
        _items = itmes;
        _get = getter;
        _set = setter;
    }

    public void Draw()
    {
        //ImGui.
        int v = _get();
        
        //ImGui
        
        if (ImGui.Combo(_label, ref v, _items, _items.Length))
            _set(v);
    }
}

// Slider (float)
public sealed class SliderParam : IDebugParam
{
    private readonly string _label;
    private readonly float _min;
    private readonly float _max;
    private readonly Func<float> _get;
    private readonly Action<float> _set;

    public SliderParam(string label, float min, float max, Func<float> getter, Action<float> setter)
    {
        _label = label;
        _min = min;
        _max = max;
        _get = getter;
        _set = setter;
    }

    public void Draw()
    {
        //ImGui.
        float v = _get();
        
        if (ImGui.SliderFloat(_label, ref v, _min, _max))
            _set(v);
    }
}

// Checkbox (bool)
public sealed class CheckBoxParam : IDebugParam
{
    private readonly string _label;
    private readonly Func<bool> _get;
    private readonly Action<bool> _set;

    public CheckBoxParam(string label, Func<bool> getter, Action<bool> setter)
    {
        _label = label;
        _get = getter;
        _set = setter;
    }

    public void Draw()
    {
        bool v = _get();
        
        if (ImGui.Checkbox(_label, ref v))
            _set(v);
    }
}

// Texture display (uniform size)
public sealed class TextureDisplayParam : IDebugParam
{
    private readonly string _label;
    private readonly Texture2D _texture;
    private readonly float _width;
    private readonly float _height;

    public TextureDisplayParam(string label, Texture2D texture,
        float width = 256f, float height = 256f)
    {
        _label = label;
        _texture = texture;
        _width = width;
        _height = height;
    }

    public void Draw()
    {
        ImGui.Text(_label);

        if (_texture == null)
        {
            ImGui.TextDisabled("null texture");
            return;
        }

        var id = DebugMenu.BindImGuiTexture(_texture);

        if (id == IntPtr.Zero)
        {
            ImGui.TextDisabled("texture not bound (DebugMenu.SetBindTextureFunc was not called)");
            return;
        }

        ImGui.Image(id, new System.Numerics.Vector2(_width, _height));
    }
}
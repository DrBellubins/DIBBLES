using DIBBLES.Utils;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;

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

// IMGUI param interface
public interface IDebugParam
{
    void Draw();
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
        float v = _get();
        if (ImGui.SliderFloat(_label, ref v, _min, _max))
        {
            _set(v);
        }
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
        {
            _set(v);
        }
    }
}

// Texture display (uniform size)
public sealed class TextureDisplayParam : IDebugParam
{
    private readonly string _label;
    private readonly Texture2D _texture;
    private readonly System.Func<Texture2D, IntPtr> _bindTexture;
    private readonly float _uniformSize;

    public TextureDisplayParam(string label, Texture2D texture, Func<Texture2D, IntPtr> bindTexture, float uniformSize = 256f)
    {
        _label = label;
        _texture = texture;
        _bindTexture = bindTexture;
        _uniformSize = uniformSize;
    }

    public void Draw()
    {
        ImGui.Text(_label);

        if (_texture == null)
        {
            ImGui.TextDisabled("null texture");
            return;
        }

        var id = _bindTexture(_texture);
        ImGui.Image(id, new System.Numerics.Vector2(_uniformSize, _uniformSize));
    }
}
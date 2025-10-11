using Microsoft.Xna.Framework;

namespace DIBBLES.Utils;

public struct RectangleF
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public RectangleF(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    
    public RectangleF(Vector2 pos, float width, float height)
    {
        X = pos.X;
        Y = pos.Y;
        Width = width;
        Height = height;
    }
    
    public RectangleF(Vector2 pos, Vector2 size)
    {
        X = pos.X;
        Y = pos.Y;
        Width = size.X;
        Height = size.Y;
    }
    
    /// <summary>
    /// Returns true if the specified point is contained within this rectangle.
    /// Equivalent to MonoGame's Rectangle.Contains.
    /// </summary>
    public bool Contains(float x, float y)
    {
        return x >= X && x < (X + Width) && y >= Y && y < (Y + Height);
    }

    /// <summary>
    /// Returns true if the specified point is contained within this rectangle.
    /// </summary>
    public bool Contains(Vector2 point)
    {
        return Contains(point.X, point.Y);
    }
    
    /// <summary>
    /// Returns true if the specified point is contained within this rectangle.
    /// </summary>
    public bool Contains(Point point)
    {
        return Contains(point.X, point.Y);
    }
}
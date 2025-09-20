using DIBBLES;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public static class Primatives2D
{
    private static Texture2D? _pixel;

    // Ensures the pixel texture exists for drawing rectangles/lines
    private static void EnsurePixel()
    {
        var gd = Engine.Graphics;
        
        if (_pixel == null || _pixel.IsDisposed)
        {
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
    }

    /// <summary>
    /// Fills a rectangle with the given color. Equivalent to Raylib.DrawRectangleRec.
    /// </summary>
    public static void DrawRectangleRec(Rectangle rect, Color color)
    {
        var sprites = Engine.Sprites;
        
        EnsurePixel();
        
        sprites.Draw(_pixel!, rect, color);
    }

    /// <summary>
    /// Fills a rectangle given as (x, y, width, height).
    /// </summary>
    public static void DrawRectangleRec(int x, int y, int width, int height, Color color)
    {
        DrawRectangleRec(new Rectangle(x, y, width, height), color);
    }
    
    /// <summary>
    /// Draws a rectangle with rounded corners, similar to Raylib.DrawRectangleRounded.
    /// </summary>
    /// <param name="rect">Rectangle area.</param>
    /// <param name="roundness">Corner roundness (0.0 to 1.0, where 1.0 is maximum circle).</param>
    /// <param name="segments">Number of segments for smooth corners (suggest 8-16).</param>
    /// <param name="color">Fill color.</param>
    public static void DrawRectangleRounded(Rectangle rect, float roundness, int segments, Color color)
    {
        roundness = Math.Clamp(roundness, 0f, 1f);
        segments = Math.Max(segments, 2);
        EnsurePixel();
        var sprites = Engine.Sprites;
    
        float radius = roundness * (Math.Min(rect.Width, rect.Height) / 2f);
        if (radius < 1f)
        {
            // Not rounded, just draw a rectangle
            sprites.Draw(_pixel!, rect, color);
            return;
        }
    
        // 1. Draw 4 quarter-circle corners
        DrawQuarterCircleFilled(sprites, rect.X + radius, rect.Y + radius, radius, 180, 270, segments, color); // Top-left
        DrawQuarterCircleFilled(sprites, rect.X + rect.Width - radius, rect.Y + radius, radius, 270, 360, segments, color); // Top-right
        DrawQuarterCircleFilled(sprites, rect.X + radius, rect.Y + rect.Height - radius, radius, 90, 180, segments, color); // Bottom-left
        DrawQuarterCircleFilled(sprites, rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, 0, 90, segments, color); // Bottom-right
    
        // 2. Draw 4 rectangles for sides (between corners)
        // Top: between top-left and top-right corners
        sprites.Draw(_pixel!, new Rectangle(
            (int)(rect.X + radius), rect.Y,
            (int)(rect.Width - 2 * radius), (int)radius), color);
    
        // Bottom: between bottom-left and bottom-right
        sprites.Draw(_pixel!, new Rectangle(
            (int)(rect.X + radius), (int)(rect.Y + rect.Height - radius),
            (int)(rect.Width - 2 * radius), (int)radius), color);
    
        // Left: between top-left and bottom-left
        sprites.Draw(_pixel!, new Rectangle(
            rect.X, (int)(rect.Y + radius),
            (int)radius, (int)(rect.Height - 2 * radius)), color);
    
        // Right: between top-right and bottom-right
        sprites.Draw(_pixel!, new Rectangle(
            (int)(rect.X + rect.Width - radius), (int)(rect.Y + radius),
            (int)radius, (int)(rect.Height - 2 * radius)), color);
    
        // 3. Draw center rectangle (touches all sides)
        sprites.Draw(_pixel!, new Rectangle(
            (int)(rect.X + radius), (int)(rect.Y + radius),
            (int)(rect.Width - 2 * radius), (int)(rect.Height - 2 * radius)), color);
    }
    
    // Draw a filled quarter circle as a triangle fan using SpriteBatch
    private static void DrawQuarterCircleFilled(SpriteBatch sprites, float cx, float cy, float radius, float startAngle, float endAngle, int segments, Color color)
    {
        double angleStep = (endAngle - startAngle) / segments;
        var points = new List<Vector2> { new Vector2(cx, cy) }; // Center
    
        for (int i = 0; i <= segments; i++)
        {
            double angle = MathHelper.ToRadians((float)(startAngle + i * angleStep));
            float x = cx + (float)Math.Cos(angle) * radius;
            float y = cy + (float)Math.Sin(angle) * radius;
            points.Add(new Vector2(x, y));
        }
    
        // Draw as triangles
        for (int i = 1; i < points.Count - 1; i++)
        {
            DrawTriangle(sprites, points[0], points[i], points[i + 1], color);
        }
    }
    
    // Draw a filled triangle with SpriteBatch using a pixel texture
    private static void DrawTriangle(SpriteBatch sprites, Vector2 v0, Vector2 v1, Vector2 v2, Color color)
    {
        // Sort points by Y
        if (v1.Y < v0.Y) (v0, v1) = (v1, v0);
        if (v2.Y < v0.Y) (v0, v2) = (v2, v0);
        if (v2.Y < v1.Y) (v1, v2) = (v2, v1);
    
        // Compute edge slopes
        float dx1 = (v1.Y - v0.Y) > 0 ? (v1.X - v0.X) / (v1.Y - v0.Y) : 0;
        float dx2 = (v2.Y - v0.Y) > 0 ? (v2.X - v0.X) / (v2.Y - v0.Y) : 0;
        float dx3 = (v2.Y - v1.Y) > 0 ? (v2.X - v1.X) / (v2.Y - v1.Y) : 0;
    
        float sx = v0.X;
        float ex = v0.X;
    
        // Top half
        for (float y = v0.Y; y < v1.Y; y++)
        {
            float xStart = sx;
            float xEnd = ex;
            if (xStart > xEnd) (xStart, xEnd) = (xEnd, xStart);
            sprites.Draw(_pixel!, new Rectangle((int)xStart, (int)y, (int)(xEnd - xStart + 1), 1), color);
            sx += dx1;
            ex += dx2;
        }
        // Bottom half
        sx = v1.X;
        for (float y = v1.Y; y < v2.Y; y++)
        {
            float xStart = sx;
            float xEnd = ex;
            if (xStart > xEnd) (xStart, xEnd) = (xEnd, xStart);
            sprites.Draw(_pixel!, new Rectangle((int)xStart, (int)y, (int)(xEnd - xStart + 1), 1), color);
            sx += dx3;
            ex += dx2;
        }
    }
}
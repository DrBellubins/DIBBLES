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

        // Calculate corner radius
        float radius = (rect.Width > rect.Height) ? (rect.Height * roundness) / 2f : (rect.Width * roundness) / 2f;
        if (radius <= 0.5f)
        {
            sprites.Draw(_pixel!, rect, color);
            return;
        }

        // Calculate the 12 points as in Raylib
        float x = rect.X;
        float y = rect.Y;
        float w = rect.Width;
        float h = rect.Height;

        Vector2[] points = new Vector2[12];
        points[0] = new Vector2(x + radius, y);
        points[1] = new Vector2(x + w - radius, y);
        points[2] = new Vector2(x + w, y + radius);
        points[3] = new Vector2(x + w, y + h - radius);
        points[4] = new Vector2(x + w - radius, y + h);
        points[5] = new Vector2(x + radius, y + h);
        points[6] = new Vector2(x, y + h - radius);
        points[7] = new Vector2(x, y + radius);
        points[8] = new Vector2(x + radius, y + radius);
        points[9] = new Vector2(x + w - radius, y + radius);
        points[10] = new Vector2(x + w - radius, y + h - radius);
        points[11] = new Vector2(x + radius, y + h - radius);

        // Draw 4 corners as filled triangle fans
        DrawQuarterCircleFan(sprites, points[8], radius, 180, 270, segments, color); // Top-left
        DrawQuarterCircleFan(sprites, points[9], radius, 270, 360, segments, color); // Top-right
        DrawQuarterCircleFan(sprites, points[10], radius, 0, 90, segments, color); // Bottom-right
        DrawQuarterCircleFan(sprites, points[11], radius, 90, 180, segments, color); // Bottom-left

        // Draw the 4 edge rectangles (as polygons)
        DrawQuad(sprites, points[0], points[8], points[9], points[1], color); // Top
        DrawQuad(sprites, points[2], points[9], points[10], points[3], color); // Right
        DrawQuad(sprites, points[5], points[11], points[10], points[4], color); // Bottom
        DrawQuad(sprites, points[7], points[8], points[11], points[6], color); // Left

        // Draw center rectangle (as a quad)
        DrawQuad(sprites, points[8], points[9], points[10], points[11], color);
    }

    private static void DrawQuad(SpriteBatch sprites, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color)
    {
        DrawTriangle(sprites, p0, p1, p2, color);
        DrawTriangle(sprites, p0, p2, p3, color);
    }

    // Triangle fan for quarter circle (no overlap with side/center rectangles, matches Raylib)
    private static void DrawQuarterCircleFan(SpriteBatch sprites, Vector2 center, float radius, float angleStart, float angleEnd, int segments, Color color)
    {
        double angleStep = (angleEnd - angleStart) / segments;
        List<Vector2> arc = new List<Vector2>();
        for (int i = 0; i <= segments; i++)
        {
            double angle = MathHelper.ToRadians((float)(angleStart + i * angleStep));
            arc.Add(center + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius));
        }
        for (int i = 0; i < segments; i++)
        {
            DrawTriangle(sprites, center, arc[i], arc[i + 1], color);
        }
    }

    private static void DrawTriangle(SpriteBatch sprites, Vector2 p0, Vector2 p1, Vector2 p2, Color color)
    {
        // Sort by Y (for scanline rasterization)
        if (p1.Y < p0.Y) (p0, p1) = (p1, p0);
        if (p2.Y < p0.Y) (p0, p2) = (p2, p0);
        if (p2.Y < p1.Y) (p1, p2) = (p2, p1);

        float dx1 = (p1.Y - p0.Y) > 0 ? (p1.X - p0.X) / (p1.Y - p0.Y) : 0;
        float dx2 = (p2.Y - p0.Y) > 0 ? (p2.X - p0.X) / (p2.Y - p0.Y) : 0;
        float dx3 = (p2.Y - p1.Y) > 0 ? (p2.X - p1.X) / (p2.Y - p1.Y) : 0;

        float sx = p0.X, ex = p0.X;

        for (float y = p0.Y; y < p1.Y; y++)
        {
            float xs = sx, xe = ex;
            if (xs > xe) (xs, xe) = (xe, xs);
            sprites.Draw(_pixel!, new Rectangle((int)xs, (int)y, (int)(xe - xs + 1), 1), color);
            sx += dx1;
            ex += dx2;
        }
        sx = p1.X;
        for (float y = p1.Y; y < p2.Y; y++)
        {
            float xs = sx, xe = ex;
            if (xs > xe) (xs, xe) = (xe, xs);
            sprites.Draw(_pixel!, new Rectangle((int)xs, (int)y, (int)(xe - xs + 1), 1), color);
            sx += dx3;
            ex += dx2;
        }
    }
}
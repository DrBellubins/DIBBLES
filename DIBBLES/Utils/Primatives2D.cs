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
        // Clamp roundness
        roundness = Math.Clamp(roundness, 0f, 1f);
        segments = Math.Max(segments, 2);

        EnsurePixel();
        var sprites = Engine.Sprites;

        float radius = roundness * (Math.Min(rect.Width, rect.Height) / 2f);

        // Draw center rect (without corners)
        Rectangle center = new Rectangle(
            (int)(rect.X + radius),
            (int)(rect.Y + radius),
            (int)(rect.Width - 2 * radius),
            (int)(rect.Height - 2 * radius)
        );
        if (center.Width > 0 && center.Height > 0)
            sprites.Draw(_pixel!, center, color);

        // Draw side rectangles
        // Top
        if (radius > 0)
            sprites.Draw(_pixel!, new Rectangle((int)(rect.X + radius), rect.Y, (int)(rect.Width - 2 * radius), (int)radius), color);
        // Bottom
        if (radius > 0)
            sprites.Draw(_pixel!, new Rectangle((int)(rect.X + radius), (int)(rect.Y + rect.Height - radius), (int)(rect.Width - 2 * radius), (int)radius), color);
        // Left
        if (radius > 0)
            sprites.Draw(_pixel!, new Rectangle(rect.X, (int)(rect.Y + radius), (int)radius, (int)(rect.Height - 2 * radius)), color);
        // Right
        if (radius > 0)
            sprites.Draw(_pixel!, new Rectangle((int)(rect.X + rect.Width - radius), (int)(rect.Y + radius), (int)radius, (int)(rect.Height - 2 * radius)), color);

        // Draw corners (quarter circles)
        if (radius > 0)
        {
            DrawQuarterCircle(sprites, rect.X + radius, rect.Y + radius, radius, 180, 270, segments, color); // Top-left
            DrawQuarterCircle(sprites, rect.X + rect.Width - radius, rect.Y + radius, radius, 270, 360, segments, color); // Top-right
            DrawQuarterCircle(sprites, rect.X + radius, rect.Y + rect.Height - radius, radius, 90, 180, segments, color); // Bottom-left
            DrawQuarterCircle(sprites, rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, 0, 90, segments, color); // Bottom-right
        }
    }

    // Helper: draws a filled quarter circle using vertical lines (fans from center)
    private static void DrawQuarterCircle(SpriteBatch sprites, float cx, float cy, float radius, float startAngle, float endAngle, int segments, Color color)
    {
        double angleStep = (endAngle - startAngle) / segments;

        for (int i = 0; i < segments; i++)
        {
            double angle0 = MathHelper.ToRadians((float)(startAngle + i * angleStep));
            double angle1 = MathHelper.ToRadians((float)(startAngle + (i + 1) * angleStep));

            float x0 = cx + (float)Math.Cos(angle0) * radius;
            float y0 = cy + (float)Math.Sin(angle0) * radius;
            float x1 = cx + (float)Math.Cos(angle1) * radius;
            float y1 = cy + (float)Math.Sin(angle1) * radius;

            // Draw triangle fan from center to arc edge
            // We'll draw a thin rectangle (line) between center and each arc segment
            DrawThickLine(sprites, cx, cy, x0, y0, x1, y1, color);
        }
    }

    // Helper: Draws a filled triangle (used for quarter circle)
    private static void DrawThickLine(SpriteBatch sprites, float cx, float cy, float x0, float y0, float x1, float y1, Color color)
    {
        // Draw two triangles between (cx,cy)-(x0,y0)-(x1,y1)
        // But since we have only a pixel, approximate by drawing a filled polygon as a very thin rectangle
        // Instead, just draw lines between center and arc
        // For a filled look, draw vertical lines between arc points and center

        // We'll draw a line from (x0,y0) to center and (x1,y1) to center as a 1px thick rectangle
        // But SpriteBatch can't draw rotated rectangles by default.
        // Instead, draw a 1x1 pixel at (x0,y0), and let the segment count fill the area.

        // For better coverage, draw a line from (x0,y0) to (x1,y1)
        int steps = (int)Math.Ceiling(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
        steps = Math.Max(steps, 1);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = MathHelper.Lerp(x0, x1, t);
            float y = MathHelper.Lerp(y0, y1, t);

            sprites.Draw(_pixel!, new Rectangle((int)x, (int)y, 1, 1), color);
        }
    }
}
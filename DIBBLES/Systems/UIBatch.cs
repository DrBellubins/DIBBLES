using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using DIBBLES.Utils;

namespace DIBBLES.Systems;

/// <summary>
/// Immediate-mode 2D/2.5D polygon batch for drawing UI elements with floating-point precision.
/// </summary>
public static class UIBatch
{
    struct UIVertex
    {
        public Vector3 Position;
        public Color Color;
        public Vector2 TexCoord;
    }

    private static GraphicsDevice _graphics;
    private static List<UIVertex> _vertices = new();
    private static List<short> _indices = new();
    private static Texture2D? _currentTexture;
    private static bool _inBatch = false;

    // Simple effect for 2D UI
    private static BasicEffect _effect;

    // You may want to cache a default white pixel texture for colored rects
    private static Texture2D _whitePixel;

    public static void Initialize()
    {
        _graphics = Engine.Graphics;

        _effect = new BasicEffect(_graphics)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
            Projection = Matrix.CreateOrthographicOffCenter(0, Engine.ScreenWidth, Engine.ScreenHeight, 0, 0, 1),
            View = Matrix.Identity,
            World = Matrix.Identity
        };

        // White pixel for solid color rectangles
        _whitePixel = new Texture2D(_graphics, 1, 1, false, SurfaceFormat.Color);
        _whitePixel.SetData(new[] { Color.White });
    }

    public static void Begin()
    {
        if (_inBatch)
            throw new InvalidOperationException("Already in batch!");
        
        _inBatch = true;
        _vertices.Clear();
        _indices.Clear();
        _currentTexture = null;
    }

    /// <summary>
    /// Draw a solid rectangle (float precision).
    /// </summary>
    public static void DrawRect(RectangleF rect, Color color)
    {
        if (_whitePixel == null) Console.WriteLine("UIBatch: _whitePixel is null!");
        Draw(_whitePixel, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), color);
    }
    
    /// <summary>
    /// Draws a rectangle with rounded corners, using UIBatch's immediate-mode vertex system.
    /// </summary>
    public static void DrawRectRounded(RectangleF rect, float roundness, int segments, Color color)
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() before DrawRectangleRounded()");

        roundness = Math.Clamp(roundness, 0f, 1f);
        segments = Math.Max(segments, 2);

        // Calculate corner radius
        float radius = (rect.Width > rect.Height) ? (rect.Height * roundness) / 2f : (rect.Width * roundness) / 2f;
        if (radius <= 0.5f)
        {
            DrawRect(rect, color);
            return;
        }

        float x = rect.X;
        float y = rect.Y;
        float w = rect.Width;
        float h = rect.Height;

        // Calculate the 12 points (from Primatives2D)
        Vector2[] points = new Vector2[12];
        points[0] = new Vector2(x + radius, y);
        points[1] = new Vector2(x + w - radius, y);
        points[2] = new Vector2(x + w, y + radius);
        points[3] = new Vector2(x + w, y + h - radius);
        points[4] = new Vector2(x + w - radius, y + h);
        points[5] = new Vector2(x + radius, y + h);
        points[6] = new Vector2(x, y + h - radius);
        points[7] = new Vector2(x, y + radius);

        points[8] = new Vector2(x + radius, y + radius);               // Top-left arc center
        points[9] = new Vector2(x + w - radius, y + radius);           // Top-right arc center
        points[10] = new Vector2(x + w - radius, y + h - radius);      // Bottom-right arc center
        points[11] = new Vector2(x + radius, y + h - radius);          // Bottom-left arc center

        // Draw the 4 corner arcs as triangle fans
        DrawQuarterCircleFan(points[8], radius, 180, 270, segments, color); // Top-left
        DrawQuarterCircleFan(points[9], radius, 270, 360, segments, color); // Top-right
        DrawQuarterCircleFan(points[10], radius, 0, 90, segments, color);   // Bottom-right
        DrawQuarterCircleFan(points[11], radius, 90, 180, segments, color); // Bottom-left

        // Draw the 4 edge rectangles as quads
        DrawQuad(points[0], points[8], points[9], points[1], color); // Top edge
        DrawQuad(points[2], points[9], points[10], points[3], color); // Right edge
        DrawQuad(points[5], points[11], points[10], points[4], color); // Bottom edge
        DrawQuad(points[7], points[8], points[11], points[6], color); // Left edge

        // Draw the center rectangle as a quad
        DrawQuad(points[8], points[9], points[10], points[11], color);
    }

    // Draws a quad (as two triangles) using UIBatch
    private static void DrawQuad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color)
    {
        short baseIndex = (short)_vertices.Count;
        Vector2 uv = Vector2.Zero; // White pixel

        _vertices.Add(new UIVertex { Position = new Vector3(p0, 0.5f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p1, 0.5f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p2, 0.5f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p3, 0.5f), Color = color, TexCoord = uv });

        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));

        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
        _currentTexture = _whitePixel;
    }

    // Draws a quarter-circle as a triangle fan using UIBatch
    private static void DrawQuarterCircleFan(Vector2 center, float radius, float angleStart, float angleEnd, int segments, Color color)
    {
        // Precompute points along the arc
        double angleStep = (angleEnd - angleStart) / segments;
        List<Vector2> arc = new List<Vector2>();
        
        for (int i = 0; i <= segments; i++)
        {
            double angle = MathHelper.ToRadians((float)(angleStart + i * angleStep));
            arc.Add(center + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius));
        }

        // Add vertices (center, then arc points)
        short baseIndex = (short)_vertices.Count;
        Vector2 uv = Vector2.Zero; // White pixel

        _vertices.Add(new UIVertex { Position = new Vector3(center, 0.5f), Color = color, TexCoord = uv });
        
        for (int i = 0; i < arc.Count; i++)
            _vertices.Add(new UIVertex { Position = new Vector3(arc[i], 0.5f), Color = color, TexCoord = uv });

        // Build triangle fan indices
        for (short i = 1; i < arc.Count; i++)
        {
            _indices.Add(baseIndex);
            _indices.Add((short)(baseIndex + i));
            _indices.Add((short)(baseIndex + i + 1));
        }
        _currentTexture = _whitePixel;
    }

    /// <summary>
    /// Draw a textured rectangle (float precision).
    /// </summary>
    public static void Draw(Texture2D texture, Vector2 position, Vector2 size, Color color, RectangleF? srcRect = null)
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() before Draw()");

        // If texture switches, flush (optional: you can batch multiple textures if you want, but SpriteBatch does not)
        if (_currentTexture != null && _currentTexture != texture)
            Flush();

        _currentTexture = texture;

        // Vertex order: TL, TR, BR, BL
        Vector2 pos = position;
        Vector2 sz = size;

        // Texture coordinates
        Vector2 uvTL = Vector2.Zero;
        Vector2 uvBR = Vector2.One;

        if (srcRect.HasValue)
        {
            var r = srcRect.Value;
            uvTL = new Vector2(r.X / texture.Width, r.Y / texture.Height);
            uvBR = new Vector2((r.X + r.Width) / texture.Width, (r.Y + r.Height) / texture.Height);
        }

        short baseIndex = (short)_vertices.Count;

        _vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y, 0.5f), Color = color, TexCoord = new Vector2(uvTL.X, uvTL.Y) }); // TL
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X + sz.X, pos.Y, 0.5f), Color = color, TexCoord = new Vector2(uvBR.X, uvTL.Y) }); // TR
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X + sz.X, pos.Y + sz.Y, 0.5f), Color = color, TexCoord = new Vector2(uvBR.X, uvBR.Y) }); // BR
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y + sz.Y, 0.5f), Color = color, TexCoord = new Vector2(uvTL.X, uvBR.Y) }); // BL

        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));

        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
    }
    
    public static void End()
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() first!");
        if (_vertices.Count > 0)
            Flush();
        _inBatch = false;
    }
    
    /// <summary>
    /// Flushes all draws (called automatically on End, or on texture switch).
    /// </summary>
    public static void Flush()
    {
        //Console.WriteLine($"Vertices: {_vertices.Count}, Indices: {_indices.Count}, Texture: {_currentTexture} ");
        
        if (_vertices.Count == 0 || _currentTexture == null) return;

        var vertexArray = new VertexPositionColorTexture[_vertices.Count];
        for (int i = 0; i < _vertices.Count; i++)
        {
            var v = _vertices[i];
            vertexArray[i] = new VertexPositionColorTexture(v.Position, v.Color, v.TexCoord);
        }

        _graphics.BlendState = BlendState.Opaque;
        _graphics.RasterizerState = RasterizerState.CullNone;
        _graphics.DepthStencilState = DepthStencilState.None;
        _graphics.SamplerStates[0] = SamplerState.PointClamp;

        _effect.Texture = _currentTexture;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0, Engine.ScreenWidth, Engine.ScreenHeight, 0, 0, 1);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphics.DrawUserIndexedPrimitives<VertexPositionColorTexture>(
                PrimitiveType.TriangleList,
                vertexArray,
                0,
                vertexArray.Length,
                _indices.ToArray(),
                0,
                _indices.Count / 3
            );
        }

        _vertices.Clear();
        _indices.Clear();
        _currentTexture = null;
    }
}
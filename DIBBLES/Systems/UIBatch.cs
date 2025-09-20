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
        Draw(_whitePixel, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), color);
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

        _vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y, 0), Color = color, TexCoord = new Vector2(uvTL.X, uvTL.Y) }); // TL
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X + sz.X, pos.Y, 0), Color = color, TexCoord = new Vector2(uvBR.X, uvTL.Y) }); // TR
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X + sz.X, pos.Y + sz.Y, 0), Color = color, TexCoord = new Vector2(uvBR.X, uvBR.Y) }); // BR
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y + sz.Y, 0), Color = color, TexCoord = new Vector2(uvTL.X, uvBR.Y) }); // BL

        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));

        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
    }

    /// <summary>
    /// Flushes all draws (called automatically on End, or on texture switch).
    /// </summary>
    public static void Flush()
    {
        if (_vertices.Count == 0 || _currentTexture == null) return;

        var vertexArray = new VertexPositionColorTexture[_vertices.Count];
        for (int i = 0; i < _vertices.Count; i++)
        {
            var v = _vertices[i];
            vertexArray[i] = new VertexPositionColorTexture(v.Position, v.Color, v.TexCoord);
        }

        _graphics.BlendState = BlendState.AlphaBlend;
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

    public static void End()
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() first!");
        if (_vertices.Count > 0)
            Flush();
        _inBatch = false;
    }
}
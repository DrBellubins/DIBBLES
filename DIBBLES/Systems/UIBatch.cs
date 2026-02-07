using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using DIBBLES.Terrain;
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
    
    private static BlendState _blendOverride = BlendState.AlphaBlend;

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
    /// Set the blend state for the UIBatch.
    /// </summary>
    /// <param name="blendState"></param>
    public static void SetBlendState(BlendState blendState)
    {
        _blendOverride = blendState ?? BlendState.AlphaBlend;
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
        drawQuarterCircleFan(points[8], radius, 180, 270, segments, color); // Top-left
        drawQuarterCircleFan(points[9], radius, 270, 360, segments, color); // Top-right
        drawQuarterCircleFan(points[10], radius, 0, 90, segments, color);   // Bottom-right
        drawQuarterCircleFan(points[11], radius, 90, 180, segments, color); // Bottom-left

        // Draw the 4 edge rectangles as quads
        DrawQuad(points[0], points[8], points[9], points[1], color); // Top edge
        DrawQuad(points[2], points[9], points[10], points[3], color); // Right edge
        DrawQuad(points[5], points[11], points[10], points[4], color); // Bottom edge
        DrawQuad(points[7], points[8], points[11], points[6], color); // Left edge

        // Draw the center rectangle as a quad
        DrawQuad(points[8], points[9], points[10], points[11], color);
    }
    
    /// <summary>
    /// Draws text to screen
    /// </summary>
    public static void DrawString(SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() before DrawString()");
        if (string.IsNullOrEmpty(text)) return;
        
        Texture2D fontTex = font.Texture;

        // Flush if texture changes
        if (_currentTexture != null && _currentTexture != fontTex)
            Flush();
        
        _currentTexture = fontTex;

        Vector2 currentPos = position;
        
        // Per-glyph data
        var glyphs = font.GetGlyphs();
        char defaultChar = font.DefaultCharacter ?? '?';
    
        foreach (char c in text)
        {
            SpriteFont.Glyph glyph;
            
            if (!glyphs.TryGetValue(c, out glyph))
            {
                if (!glyphs.TryGetValue(defaultChar, out glyph))
                    continue; // Skip missing glyphs
            }
    
            Rectangle srcRect = glyph.BoundsInTexture;
            Rectangle cropping = glyph.Cropping;
            Vector3 vPos = new Vector3(currentPos.X + cropping.X * scale, currentPos.Y + cropping.Y * scale, 0f);
    
            Vector2 size = new Vector2(srcRect.Width * scale, srcRect.Height * scale);
    
            // Texture UVs (normalized)
            Vector2 texTL = new Vector2((float)srcRect.X / fontTex.Width, (float)srcRect.Y / fontTex.Height);
            Vector2 texBR = new Vector2((float)(srcRect.X + srcRect.Width) / fontTex.Width, (float)(srcRect.Y + srcRect.Height) / fontTex.Height);
    
            short baseIndex = (short)_vertices.Count;
    
            _vertices.Add(new UIVertex { Position = vPos, Color = color, TexCoord = texTL }); // Top-left
            _vertices.Add(new UIVertex { Position = vPos + new Vector3(size.X, 0, 0), Color = color, TexCoord = new Vector2(texBR.X, texTL.Y) }); // Top-right
            _vertices.Add(new UIVertex { Position = vPos + new Vector3(size.X, size.Y, 0), Color = color, TexCoord = texBR }); // Bottom-right
            _vertices.Add(new UIVertex { Position = vPos + new Vector3(0, size.Y, 0), Color = color, TexCoord = new Vector2(texTL.X, texBR.Y) }); // Bottom-left
    
            _indices.Add((short)(baseIndex + 0));
            _indices.Add((short)(baseIndex + 1));
            _indices.Add((short)(baseIndex + 2));
            _indices.Add((short)(baseIndex + 0));
            _indices.Add((short)(baseIndex + 2));
            _indices.Add((short)(baseIndex + 3));
    
            // Advance to next character
            currentPos.X += glyph.Width * scale;
        }
    }

    public static Texture2D PremultiplyAlpha(Texture2D source)
    {
        // Step 1: Extract pixel data
        int width = source.Width;
        int height = source.Height;
        Color[] pixels = new Color[width * height];
        source.GetData(pixels);

        // Step 2: Premultiply
        for (int i = 0; i < pixels.Length; i++)
        {
            float a = pixels[i].A / 255f;
            pixels[i].R = (byte)(pixels[i].R * a);
            pixels[i].G = (byte)(pixels[i].G * a);
            pixels[i].B = (byte)(pixels[i].B * a);
        }

        // Step 3: Create new texture and set data
        Texture2D result = new Texture2D(source.GraphicsDevice, width, height, false, SurfaceFormat.Color);
        result.SetData(pixels);

        return result;
    }
    
    public static void DrawString(string text, Vector2 position, Color color)
    {
        DrawString(Engine.MainFont, text, position, color);
    }
    
    // Draws a quad (as two triangles) using UIBatch
    private static void DrawQuad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color)
    {
        if (_currentTexture != null && _currentTexture != _whitePixel)
            Flush();
        
        _currentTexture = _whitePixel;
        
        short baseIndex = (short)_vertices.Count;
        Vector2 uv = Vector2.Zero; // White pixel

        _vertices.Add(new UIVertex { Position = new Vector3(p0, 0f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p1, 0f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p2, 0f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p3, 0f), Color = color, TexCoord = uv });

        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));

        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
    }

    // Draws a quarter-circle as a triangle fan using UIBatch
    private static void drawQuarterCircleFan(Vector2 center, float radius, float angleStart, float angleEnd, int segments, Color color)
    {
        if (_currentTexture != null && _currentTexture != _whitePixel)
            Flush();
        
        _currentTexture = _whitePixel;
        
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

        _vertices.Add(new UIVertex { Position = new Vector3(center, 0f), Color = color, TexCoord = uv });
        
        for (int i = 0; i < arc.Count; i++)
            _vertices.Add(new UIVertex { Position = new Vector3(arc[i], 0f), Color = color, TexCoord = uv });

        // Build triangle fan indices
        for (short i = 1; i < arc.Count; i++)
        {
            _indices.Add(baseIndex);
            _indices.Add((short)(baseIndex + i));
            _indices.Add((short)(baseIndex + i + 1));
        }
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

        _vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y, 0f), Color = color, TexCoord = new Vector2(uvTL.X, uvTL.Y) }); // TL
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X + sz.X, pos.Y, 0f), Color = color, TexCoord = new Vector2(uvBR.X, uvTL.Y) }); // TR
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X + sz.X, pos.Y + sz.Y, 0f), Color = color, TexCoord = new Vector2(uvBR.X, uvBR.Y) }); // BR
        _vertices.Add(new UIVertex { Position = new Vector3(pos.X, pos.Y + sz.Y, 0f), Color = color, TexCoord = new Vector2(uvTL.X, uvBR.Y) }); // BL

        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));

        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
    }
    
    /// <summary>
    /// Draw a texture with rectangle
    /// </summary>
    public static void DrawTextureRect(Texture2D texture, RectangleF destinationRectangle, Color color)
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() before Draw()");

        // If texture switches, flush the batch
        if (_currentTexture != null && _currentTexture != texture)
            Flush();

        _currentTexture = texture;

        // Use the full texture (uv = 0,0 to 1,1)
        Vector2 uvTL = Vector2.Zero;
        Vector2 uvBR = Vector2.One;

        short baseIndex = (short)_vertices.Count;

        float x = destinationRectangle.X;
        float y = destinationRectangle.Y;
        float w = destinationRectangle.Width;
        float h = destinationRectangle.Height;

        _vertices.Add(new UIVertex { Position = new Vector3(x, y, 0f), Color = color, TexCoord = new Vector2(uvTL.X, uvTL.Y) });             // TL
        _vertices.Add(new UIVertex { Position = new Vector3(x + w, y, 0f), Color = color, TexCoord = new Vector2(uvBR.X, uvTL.Y) });         // TR
        _vertices.Add(new UIVertex { Position = new Vector3(x + w, y + h, 0f), Color = color, TexCoord = new Vector2(uvBR.X, uvBR.Y) });     // BR
        _vertices.Add(new UIVertex { Position = new Vector3(x, y + h, 0f), Color = color, TexCoord = new Vector2(uvTL.X, uvBR.Y) });         // BL

        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
    }
    
    /// <summary>
    /// Draws a texture with source and destination rectangles, origin, and rotation.
    /// Equivalent to Raylib.DrawTexturePro.
    /// </summary>
    public static void DrawTexturePro(
        Texture2D texture,
        RectangleF sourceRec,
        RectangleF destRec,
        Vector2 origin,
        float rotation,
        Color color)
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() before DrawTexturePro()");
    
        // Flush if texture switches
        if (_currentTexture != null && _currentTexture != texture)
            Flush();
        
        _currentTexture = texture;
    
        // Compute normalized UVs
        Vector2 uvTL = new Vector2(sourceRec.X / texture.Width, (sourceRec.Y / texture.Height));
        Vector2 uvBR = new Vector2((sourceRec.X + sourceRec.Width) / texture.Width,
                                   (sourceRec.Y + sourceRec.Height) / texture.Height);
    
        // Define destination quad in local space (-origin), then rotate, then translate
        Vector2[] corners = new Vector2[4];
        corners[0] = new Vector2(0, 0) - origin;                        // Top-left
        corners[1] = new Vector2(destRec.Width, 0) - origin;            // Top-right
        corners[2] = new Vector2(destRec.Width, destRec.Height) - origin; // Bottom-right
        corners[3] = new Vector2(0, destRec.Height) - origin;           // Bottom-left
    
        // Apply rotation
        if (rotation != 0f)
        {
            float rad = MathHelper.ToRadians(rotation);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            
            for (int i = 0; i < 4; i++)
            {
                var v = corners[i];
                
                corners[i] = new Vector2(
                    v.X * cos - v.Y * sin,
                    v.X * sin + v.Y * cos
                );
            }
        }
    
        // Translate to final position
        Vector2 destPos = new Vector2(destRec.X, destRec.Y);
        
        for (int i = 0; i < 4; i++)
            corners[i] += destPos;
    
        // Vertex order: TL, TR, BR, BL
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(uvTL.X, uvTL.Y),               // TL
            new Vector2(uvBR.X, uvTL.Y),               // TR
            new Vector2(uvBR.X, uvBR.Y),               // BR
            new Vector2(uvTL.X, uvBR.Y),               // BL
        };
    
        short baseIndex = (short)_vertices.Count;
        
        for (int i = 0; i < 4; i++)
            _vertices.Add(new UIVertex { Position = new Vector3(corners[i], 0f), Color = color, TexCoord = uvs[i] });
    
        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
    }
    
    /// <summary>
    /// Draws a thick line between two points. Equivalent to Raylib.DrawLineEx.
    /// </summary>
    public static void DrawLine(Vector2 start, Vector2 end, float thickness, Color color)
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() before DrawLineEx()");

        // If texture switches, flush
        if (_currentTexture != null && _currentTexture != _whitePixel)
            Flush();
        
        _currentTexture = _whitePixel;

        Vector2 direction = end - start;
        if (direction.LengthSquared() < float.Epsilon)
            return; // Points are the same; nothing to draw

        Vector2 normal = Vector2.Normalize(new Vector2(-direction.Y, direction.X));
        Vector2 offset = normal * (thickness / 2f);

        Vector2 p0 = start + offset;
        Vector2 p1 = start - offset;
        Vector2 p2 = end - offset;
        Vector2 p3 = end + offset;

        short baseIndex = (short)_vertices.Count;
        Vector2 uv = Vector2.Zero; // White pixel

        _vertices.Add(new UIVertex { Position = new Vector3(p0, 0f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p1, 0f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p2, 0f), Color = color, TexCoord = uv });
        _vertices.Add(new UIVertex { Position = new Vector3(p3, 0f), Color = color, TexCoord = uv });

        // Two triangles: 0,1,2 and 0,2,3
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 1));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 0));
        _indices.Add((short)(baseIndex + 2));
        _indices.Add((short)(baseIndex + 3));
    }
    
    /// <summary>
    /// Draws a filled circle. Equivalent to Raylib.DrawCircle.
    /// </summary>
    public static void DrawCircle(Vector2 center, float radius, Color color, int segments = 32)
    {
        if (!_inBatch) throw new InvalidOperationException("Call Begin() before DrawCircle()");

        // Flush if texture switches (should always be white pixel here)
        if (_currentTexture != null && _currentTexture != _whitePixel)
            Flush();
        _currentTexture = _whitePixel;

        // Calculate vertices for the triangle fan
        short baseIndex = (short)_vertices.Count;
        Vector2 uv = Vector2.Zero; // White pixel

        // Center point
        _vertices.Add(new UIVertex { Position = new Vector3(center, 0f), Color = color, TexCoord = uv });

        // Perimeter points
        for (int i = 0; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float x = center.X + MathF.Cos(angle) * radius;
            float y = center.Y + MathF.Sin(angle) * radius;
            _vertices.Add(new UIVertex { Position = new Vector3(x, y, 0f), Color = color, TexCoord = uv });
        }

        // Indices for triangle fan
        for (short i = 1; i <= segments; i++)
        {
            _indices.Add(baseIndex);         // center
            _indices.Add((short)(baseIndex + i));
            _indices.Add((short)(baseIndex + i + 1));
        }
    }

    public static void DrawCircle(float x, float y, float radius, Color color, int segments = 32)
    {
        DrawCircle(new  Vector2(x, y), radius, color, segments);
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
        if (_vertices.Count == 0 || _currentTexture == null) return;

        var vertexArray = new VertexPositionColorTexture[_vertices.Count];
        for (int i = 0; i < _vertices.Count; i++)
        {
            var v = _vertices[i];
            vertexArray[i] = new VertexPositionColorTexture(v.Position, v.Color, v.TexCoord);
        }

        _graphics.BlendState = _blendOverride;
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
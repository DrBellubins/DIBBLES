using DIBBLES.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Utils;

// Class representing a simple textured cube mesh for MonoGame
public class MonoCubeMesh
{
    public VertexBuffer VertexBuffer { get; private set; }
    public IndexBuffer IndexBuffer { get; private set; }
    public Texture2D Texture { get; set; }
    public BasicEffect Effect { get; private set; }
    public GraphicsDevice GraphicsDevice { get; private set; }

    // Generates a 0.5 unit size textured cube mesh
    public MonoCubeMesh(GraphicsDevice graphicsDevice, Texture2D texture)
    {
        GraphicsDevice = graphicsDevice;
        Texture = texture;

        var vertices = new VertexPositionNormalTexture[24];
        var indices = new short[36];
        FillCubeMeshData(vertices, indices, 0.25f); // Cube size (half extents)

        VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
        VertexBuffer.SetData(vertices);

        IndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indices.Length, BufferUsage.WriteOnly);
        IndexBuffer.SetData(indices);

        Effect = new BasicEffect(graphicsDevice)
        {
            TextureEnabled = true,
            Texture = Texture,
            LightingEnabled = false,
            VertexColorEnabled = false
        };
    }

    // Fills the arrays with cube data (positions, normals, UVs, indices)
    private void FillCubeMeshData(VertexPositionNormalTexture[] vertices, short[] indices, float s)
    {
        // Cube faces: +X, -X, +Y, -Y, +Z, -Z
        Vector3[] positions =
        {
            // +X
            new Vector3(+s, -s, -s), new Vector3(+s, +s, -s), new Vector3(+s, +s, +s), new Vector3(+s, -s, +s),
            // -X
            new Vector3(-s, -s, +s), new Vector3(-s, +s, +s), new Vector3(-s, +s, -s), new Vector3(-s, -s, -s),
            // +Y
            new Vector3(-s, +s, -s), new Vector3(-s, +s, +s), new Vector3(+s, +s, +s), new Vector3(+s, +s, -s),
            // -Y
            new Vector3(-s, -s, +s), new Vector3(-s, -s, -s), new Vector3(+s, -s, -s), new Vector3(+s, -s, +s),
            // +Z
            new Vector3(-s, -s, +s), new Vector3(+s, -s, +s), new Vector3(+s, +s, +s), new Vector3(-s, +s, +s),
            // -Z
            new Vector3(+s, -s, -s), new Vector3(-s, -s, -s), new Vector3(-s, +s, -s), new Vector3(+s, +s, -s)
        };

        Vector3[] normals =
        {
            // +X
            Vector3.Right, Vector3.Right, Vector3.Right, Vector3.Right,
            // -X
            Vector3.Left, Vector3.Left, Vector3.Left, Vector3.Left,
            // +Y
            Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up,
            // -Y
            Vector3.Down, Vector3.Down, Vector3.Down, Vector3.Down,
            // +Z
            Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward,
            // -Z
            Vector3.Backward, Vector3.Backward, Vector3.Backward, Vector3.Backward
        };

        Vector2[] uvs =
        {
            // +X
            new Vector2(0,1), new Vector2(0,0), new Vector2(1,0), new Vector2(1,1),
            // -X
            new Vector2(0,1), new Vector2(0,0), new Vector2(1,0), new Vector2(1,1),
            // +Y
            new Vector2(0,1), new Vector2(0,0), new Vector2(1,0), new Vector2(1,1),
            // -Y
            new Vector2(0,1), new Vector2(0,0), new Vector2(1,0), new Vector2(1,1),
            // +Z
            new Vector2(0,1), new Vector2(0,0), new Vector2(1,0), new Vector2(1,1),
            // -Z
            new Vector2(0,1), new Vector2(0,0), new Vector2(1,0), new Vector2(1,1),
        };

        for (int i = 0; i < 24; i++)
            vertices[i] = new VertexPositionNormalTexture(positions[i], normals[i], uvs[i]);

        // Indices for each face (2 triangles per face)
        for (int f = 0; f < 6; f++)
        {
            int vi = f * 4;
            int ii = f * 6;
            
            // First triangle (CCW): 0,2,1
            indices[ii + 0] = (short)(vi + 0);
            indices[ii + 1] = (short)(vi + 2);
            indices[ii + 2] = (short)(vi + 1);
            
            // Second triangle (CCW): 0,3,2
            indices[ii + 3] = (short)(vi + 0);
            indices[ii + 4] = (short)(vi + 3);
            indices[ii + 5] = (short)(vi + 2);
        }
    }

    public void SetTexture(Texture2D texture)
    {
        Texture = texture;
        Effect.Texture = texture;
    }

    // Draws the cube mesh at a given world matrix, using the given view/projection
    public void Draw(Matrix world, Matrix view, Matrix projection)
    {
        Effect.World = world;
        Effect.View = view;
        Effect.Projection = projection;

        foreach (var pass in Effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(VertexBuffer);
            GraphicsDevice.Indices = IndexBuffer;
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
        }
    }
}

// Utility: Returns a single white pixel texture (for box/caret drawing)
public static class TextureUtils
{
    private static Texture2D whitePixel;
        
    public static Texture2D GetWhitePixel()
    {
        if (whitePixel == null)
        {
            whitePixel = new Texture2D(MonoEngine.Graphics, 1, 1);
            whitePixel.SetData(new[] { Color.White });
        }
            
        return whitePixel;
    }

    /// <summary>
    /// Alter a character in this spritefont.
    /// requires ... using System.Collections.Generic;
    /// </summary>
    public static SpriteFont AlterSpriteFont(SpriteFont sf, char chartoalter, float width_amount_to_add)
    {
        Dictionary<char, SpriteFont.Glyph> dgyphs;
        SpriteFont.Glyph defaultglyph;
        char defaultchar = ' ';
        
        // Alter one of my methods a bit here for this purpose.
        // just drop all the alterd values into a new spritefont
        dgyphs = sf.GetGlyphs();
        defaultglyph = new SpriteFont.Glyph();
        
        if (sf.DefaultCharacter.HasValue)
        {
            defaultchar = (char)(sf.DefaultCharacter.Value);
            defaultglyph = dgyphs[defaultchar];
        }
        else
        {
            // we could create a default value from like a pixel in the sprite font and add the glyph.
        }
        
        var altered = dgyphs[chartoalter];
        altered.Width = altered.Width + width_amount_to_add;  // ect 
        dgyphs.Remove(chartoalter);
        dgyphs.Add(chartoalter, altered);

        //sf.Glyphs = _glyphs;  // cant do it as its readonly private that sucks hard we would of been done

        List<Rectangle> glyphBounds = new List<Rectangle>();
        List<Rectangle> cropping = new List<Rectangle>();
        List<char> characters = new List<char>();
        List<Vector3> kerning = new List<Vector3>();
        
        foreach (var item in dgyphs)
        {
            glyphBounds.Add(item.Value.BoundsInTexture);
            cropping.Add(item.Value.Cropping);
            characters.Add(item.Value.Character);
            kerning.Add(new Vector3(item.Value.LeftSideBearing, item.Value.Width, item.Value.RightSideBearing));
        }
        
        List<Rectangle> b = new List<Rectangle>();
        sf = new SpriteFont(sf.Texture, glyphBounds, cropping, characters, sf.LineSpacing, sf.Spacing, kerning, defaultchar);
        return sf;
    }
}

public static class Primatives
{
    private static Texture2D? _pixel;

    // Ensures the pixel texture exists for drawing rectangles/lines
    private static void EnsurePixel()
    {
        var gd = MonoEngine.Graphics;
        
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
        var sprites = MonoEngine.Sprites;
        
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
}

// Static utility class to provide cube mesh creation (matches Raylib MeshUtils)
public static class MeshUtilsMonoGame
{
    // Returns a simple cube mesh with the given texture
    public static MonoCubeMesh GenTexturedCube(GraphicsDevice graphicsDevice, Texture2D texture)
        => new MonoCubeMesh(graphicsDevice, texture);

    // Sets the texture on an existing cube mesh
    public static void SetCubeMeshTexture(MonoCubeMesh mesh, Texture2D texture)
        => mesh.SetTexture(texture);
    
    public static RuntimeModel GenTexturedCubeIcon(Texture2D texture)
    {
        var gd = MonoEngine.Graphics;
        
        // Define face colors for the icon
        var faceColors = new Color[]
        {
            new Color(180,180,180,255), // Right (-Z)
            new Color(0,0,0,0),         // Unused (+Z)
            new Color(150,150,150,255), // Left (-X)
            new Color(0,0,0,0),         // Unused (+X)
            new Color(255,255,255,255), // Top (-Y)
            new Color(0,0,0,0),         // Unused (+Y)
        };

        return GenMeshCubeWithColors(1f, 1f, 1f, faceColors, texture);
    }
    
    /// <summary>
    /// Generates a cube mesh with per-face colors and a texture. Only visible faces are colored.
    /// </summary>
    public static RuntimeModel GenMeshCubeWithColors(float width, float height, float length, Color[] faceColors, Texture2D texture)
    {
        var gd = MonoEngine.Graphics;
        
        // 6 faces, 4 vertices per face (no sharing for unique face colors)
        int faceCount = 6;
        int vertsPerFace = 4;
        int indicesPerFace = 6;

        var vertices = new VertexPositionNormalTexture[faceCount * vertsPerFace];
        var indices = new short[faceCount * indicesPerFace];

        float x = width * 0.5f;
        float y = height * 0.5f;
        float z = length * 0.5f;

        // Cube face data (positions, normals, uvs)
        Vector3[] faceNormals = new[]
        {
            new Vector3(0,0,-1), // Front (-Z)
            new Vector3(0,0,1),  // Back (+Z)
            new Vector3(-1,0,0), // Left (-X)
            new Vector3(1,0,0),  // Right (+X)
            new Vector3(0,-1,0), // Bottom (-Y)
            new Vector3(0,1,0),  // Top (+Y)
        };

        // Each face: 4 corners (counter-clockwise)
        Vector3[][] faceVerts = new Vector3[][]
        {
            // Front (-Z)
            new[] { new Vector3(-x,-y,-z), new Vector3(-x, y,-z), new Vector3( x, y,-z), new Vector3( x,-y,-z) },
            // Back (+Z)
            new[] { new Vector3( x,-y, z), new Vector3( x, y, z), new Vector3(-x, y, z), new Vector3(-x,-y, z) },
            // Left (-X)
            new[] { new Vector3(-x,-y, z), new Vector3(-x, y, z), new Vector3(-x, y,-z), new Vector3(-x,-y,-z) },
            // Right (+X)
            new[] { new Vector3( x,-y,-z), new Vector3( x, y,-z), new Vector3( x, y, z), new Vector3( x,-y, z) },
            // Bottom (-Y)
            new[] { new Vector3(-x,-y, z), new Vector3(-x,-y,-z), new Vector3( x,-y,-z), new Vector3( x,-y, z) },
            // Top (+Y)
            new[] { new Vector3(-x, y,-z), new Vector3(-x, y, z), new Vector3( x, y, z), new Vector3( x, y,-z) },
        };

        Vector2[] uvs = new[]
        {
            new Vector2(0,1), new Vector2(0,0), new Vector2(1,0), new Vector2(1,1)
        };

        // Build vertex and index arrays
        int v = 0, i = 0;
        for (int f = 0; f < faceCount; f++)
        {
            var normal = faceNormals[f];

            // Only color selected faces for icon
            Color color = faceColors[f];
            if (color.A == 0) continue; // Skip unused faces

            // 4 vertices per face
            for (int j = 0; j < 4; j++)
            {
                // NOTE: VertexPositionNormalTexture does not support per-vertex color. 
                // For per-face color, you need a custom vertex type and a custom shader.
                // Here we use BasicEffect with texture only, so color must be baked into the texture for a true effect.
                vertices[v + j] = new VertexPositionNormalTexture(
                    faceVerts[f][j],
                    normal,
                    uvs[j]
                );
            }

            // 2 triangles per face (0,1,2) (0,2,3)
            indices[i++] = (short)(v + 0);
            indices[i++] = (short)(v + 1);
            indices[i++] = (short)(v + 2);
            indices[i++] = (short)(v + 0);
            indices[i++] = (short)(v + 2);
            indices[i++] = (short)(v + 3);

            v += 4;
        }

        // Remove unused vertices (those with color.A == 0)
        int usedVerts = v;
        int usedIndices = i;
        var finalVertices = new VertexPositionNormalTexture[usedVerts];
        var finalIndices = new short[usedIndices];
        Array.Copy(vertices, finalVertices, usedVerts);
        Array.Copy(indices, finalIndices, usedIndices);

        // Create buffers
        var vb = new VertexBuffer(gd, typeof(VertexPositionNormalTexture), usedVerts, BufferUsage.WriteOnly);
        vb.SetData(finalVertices);

        var ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, usedIndices, BufferUsage.WriteOnly);
        ib.SetData(finalIndices);

        // BasicEffect
        var effect = new BasicEffect(gd)
        {
            TextureEnabled = true,
            Texture = texture,
            LightingEnabled = false,
            VertexColorEnabled = false // If you want per-vertex color, use a custom shader and vertex struct
        };

        var model = new RuntimeModel
        {
            VertexBuffer = vb,
            IndexBuffer = ib,
            TriangleCount = usedIndices / 3,
            Effect = effect,
            Texture = texture
        };

        return model;
    }
}
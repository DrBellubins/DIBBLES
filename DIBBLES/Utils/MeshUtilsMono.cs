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
            indices[ii + 0] = (short)(vi + 0);
            indices[ii + 1] = (short)(vi + 1);
            indices[ii + 2] = (short)(vi + 2);
            indices[ii + 3] = (short)(vi + 0);
            indices[ii + 4] = (short)(vi + 2);
            indices[ii + 5] = (short)(vi + 3);
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
}

// Utility class to provide cube mesh creation (matches Raylib MeshUtils)
public static class MeshUtilsMonoGame
{
    // Returns a simple cube mesh with the given texture
    public static MonoCubeMesh GenTexturedCube(GraphicsDevice graphicsDevice, Texture2D texture)
        => new MonoCubeMesh(graphicsDevice, texture);

    // Sets the texture on an existing cube mesh
    public static void SetCubeMeshTexture(MonoCubeMesh mesh, Texture2D texture)
        => mesh.SetTexture(texture);
}
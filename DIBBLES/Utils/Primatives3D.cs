using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using DIBBLES.Scenes;

namespace DIBBLES.Utils;

/// <summary>
/// Immediate-mode 3D primitives for MonoGame. 
/// Provides thick line, cube wire, and plane drawing using Engine.Graphics.
/// </summary>
public static class Primatives3D
{
    // Cached quad vertex/index buffers for thick line and plane rendering
    private static BasicEffect _effect;

    static Primatives3D()
    {
        // Effect is created in Initialize()
    }

    public static void Initialize()
    {
        _effect = new BasicEffect(Engine.Graphics)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false
        };
    }

    /// <summary>
    /// Draws a cube wireframe with customizable line thickness.
    /// </summary>
    public static void DrawCubeWiresThick(
        Vector3 position, float width, float height, float length, Color color, float thickness = 0.02f,
        Matrix? view = null, Matrix? projection = null)
    {
        var padding = 0.01f; // To prevent z fighting

        // Calculate min/max corners
        Vector3 half = new Vector3(width + padding, height + padding, length + padding) * 0.5f;
        Vector3 min = position - half;
        Vector3 max = position + half;

        // 8 corners of the cube
        Vector3[] corners = new Vector3[8];
        corners[0] = new Vector3(min.X, min.Y, min.Z);
        corners[1] = new Vector3(max.X, min.Y, min.Z);
        corners[2] = new Vector3(max.X, max.Y, min.Z);
        corners[3] = new Vector3(min.X, max.Y, min.Z);

        corners[4] = new Vector3(min.X, min.Y, max.Z);
        corners[5] = new Vector3(max.X, min.Y, max.Z);
        corners[6] = new Vector3(max.X, max.Y, max.Z);
        corners[7] = new Vector3(min.X, max.Y, max.Z);

        // 12 edges of the cube (pairs of indices)
        int[,] edges = new int[12, 2]
        {
            {0,1},{1,2},{2,3},{3,0},
            {4,5},{5,6},{6,7},{7,4},
            {0,4},{1,5},{2,6},{3,7}
        };

        Matrix v = view ?? GameScene.PlayerCharacter.Camera.View;
        Matrix p = projection ?? GameScene.PlayerCharacter.Camera.Projection;

        _effect.World = Matrix.Identity;
        _effect.View = v;
        _effect.Projection = p;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            // Get camera up and forward (for thick line facing)
            Vector3 camPos = v.Translation;
            Vector3 camUp = v.Up;
            Vector3 camForward = -v.Forward;

            for (int i = 0; i < 12; i++)
            {
                Vector3 start = corners[edges[i, 0]];
                Vector3 end = corners[edges[i, 1]];
                DrawThickLine3D(start, end, color, thickness, camPos, camUp, camForward);
            }
        }
    }

    /// <summary>
    /// Draws a 3D line with thickness as a quad facing the camera.
    /// </summary>
    public static void DrawThickLine3D(
        Vector3 start, Vector3 end, Color color, float thickness,
        Vector3 camPos, Vector3 camUp, Vector3 camForward)
    {
        GraphicsDevice gd = Engine.Graphics;

        Vector3 dir = Vector3.Normalize(end - start);
        Vector3 side = Vector3.Cross(dir, camForward);
        if (side.Length() < 0.001f)
            side = Vector3.Cross(dir, camUp);

        side = Vector3.Normalize(side) * (thickness * 0.5f);

        Vector3 v1 = start + side;
        Vector3 v2 = start - side;
        Vector3 v3 = end - side;
        Vector3 v4 = end + side;

        VertexPositionColor[] quadVerts = new[]
        {
            new VertexPositionColor(v1, color),
            new VertexPositionColor(v2, color),
            new VertexPositionColor(v3, color),
            new VertexPositionColor(v4, color)
        };

        short[] quadIdx = { 0, 1, 2, 0, 2, 3 };

        gd.DrawUserIndexedPrimitives(
            PrimitiveType.TriangleList,
            quadVerts, 0, 4,
            quadIdx, 0, 2
        );
    }

    /// <summary>
    /// Draws a plane at the given position, size, and up direction.
    /// </summary>
    /// <param name="centerPos">Center position of the plane.</param>
    /// <param name="size">Size of the plane (X=width, Y=length).</param>
    /// <param name="color">Color of the plane.</param>
    /// <param name="up">Up direction of the plane (default: Vector3.UnitY).</param>
    public static void DrawPlane(
        Vector3 centerPos, Vector2 size, Color color, Vector3? up = null,
        Matrix? view = null, Matrix? projection = null)
    {
        Matrix v = view ?? GameScene.PlayerCharacter.Camera.View;
        Matrix p = projection ?? GameScene.PlayerCharacter.Camera.Projection;

        _effect.World = Matrix.Identity;
        _effect.View = v;
        _effect.Projection = p;
        _effect.CurrentTechnique.Passes[0].Apply();

        Vector3 upDir = up ?? Vector3.UnitY;
        upDir = Vector3.Normalize(upDir);

        Vector3 arbitrary = Math.Abs(Vector3.Dot(upDir, Vector3.UnitX)) < 0.99f ? Vector3.UnitX : Vector3.UnitZ;
        Vector3 right = Vector3.Normalize(Vector3.Cross(upDir, arbitrary));
        Vector3 forward = Vector3.Normalize(Vector3.Cross(right, upDir));

        float halfWidth = size.X * 0.5f;
        float halfLength = size.Y * 0.5f;

        Vector3 p0 = centerPos + (-right * halfWidth) + (-forward * halfLength);
        Vector3 p1 = centerPos + (-right * halfWidth) + ( forward * halfLength);
        Vector3 p2 = centerPos + ( right * halfWidth) + ( forward * halfLength);
        Vector3 p3 = centerPos + ( right * halfWidth) + (-forward * halfLength);

        VertexPositionColor[] verts = new[]
        {
            new VertexPositionColor(p0, color),
            new VertexPositionColor(p1, color),
            new VertexPositionColor(p2, color),
            new VertexPositionColor(p3, color)
        };

        short[] idx = { 0, 1, 2, 0, 2, 3 };

        Engine.Graphics.DrawUserIndexedPrimitives(
            PrimitiveType.TriangleList,
            verts, 0, 4,
            idx, 0, 2
        );
    }
}
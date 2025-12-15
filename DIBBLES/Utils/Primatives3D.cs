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
    public static void DrawCubeWiresThick(Vector3 position, float width, float height,
        float length, Color color, float thickness = 0.02f)
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

        Matrix v = GameScene.PlayerCharacter.Camera.View;
        Matrix p = GameScene.PlayerCharacter.Camera.Projection;

        _effect.World = Matrix.Identity;
        _effect.View = v;
        _effect.Projection = p;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            for (int i = 0; i < 12; i++)
            {
                Vector3 start = corners[edges[i, 0]];
                Vector3 end = corners[edges[i, 1]];
                DrawThickLine3D(start, end, color, thickness);
            }
        }
    }

    /// <summary>
    /// Draws a 3D line with thickness as a quad facing the camera.
    /// </summary>
    public static void DrawThickLine3D(Vector3 start, Vector3 end, Color color, float thickness)
    {
        // Use camera forward/up to build a world-space billboard around the line
        var camView = GameScene.PlayerCharacter.Camera.View;
        var camProj = GameScene.PlayerCharacter.Camera.Projection;
    
        // Extract camera forward from View (third column of inverse view gives forward)
        // Alternatively, use PlayerCharacter.CameraForward which is already maintained.
        Vector3 camForward = Vector3.Normalize(GameScene.PlayerCharacter.Camera.Target - GameScene.PlayerCharacter.Camera.Position.ToVector3());
        Vector3 camUp = GameScene.PlayerCharacter.Camera.Up;
    
        // Line direction
        Vector3 dir = end - start;
        float dirLenSq = dir.LengthSquared();
    
        if (dirLenSq < 1e-6f)
            return;
    
        dir /= MathF.Sqrt(dirLenSq);
    
        // Perpendicular in world space using camera forward (fallback to camUp when nearly parallel)
        Vector3 side = Vector3.Cross(dir, camForward);
    
        if (side.LengthSquared() < 1e-6f)
            side = Vector3.Cross(dir, camUp);
    
        side = Vector3.Normalize(side);
    
        // Optional: scale thickness by distance for visual consistency
        float dist = Vector3.Distance(GameScene.PlayerCharacter.Camera.Position.ToVector3(), (start + end) * 0.5f);
        float pixelScale = 0.0015f; // tune to taste
        float halfWidth = thickness * 0.5f * MathF.Max(1f, dist) * pixelScale;
    
        Vector3 offset = side * halfWidth;
    
        // Build the quad in world space
        Vector3 v0 = start + offset;
        Vector3 v1 = start - offset;
        Vector3 v2 = end - offset;
        Vector3 v3 = end + offset;
    
        // Submit as a single quad (two triangles)
        VertexPositionColor[] quadVerts = new[]
        {
            new VertexPositionColor(v0, color),
            new VertexPositionColor(v1, color),
            new VertexPositionColor(v2, color),
            new VertexPositionColor(v3, color),
        };
    
        short[] quadIdx = { 0, 1, 2, 0, 2, 3 };
    
        // Effect is already applied in DrawCubeWiresThick before calling this
        Engine.Graphics.DrawUserIndexedPrimitives(
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
        Vector3 centerPos, Vector2 size, Color color, Vector3? up = null)
    {
        Matrix v = GameScene.PlayerCharacter.Camera.View;
        Matrix p = GameScene.PlayerCharacter.Camera.Projection;

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
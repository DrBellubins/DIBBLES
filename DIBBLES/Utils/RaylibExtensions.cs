using System.Numerics;
using DIBBLES.Scenes;
using Raylib_cs;

namespace DIBBLES.Utils;

/// <summary>
/// A collection of Raylib extensions
/// </summary>
public static class RayEx
{
    /// <summary>
    /// Draws a cube wireframe with customizable line thickness.
    /// </summary>
    public static void DrawCubeWiresThick(Vector3 position, float width, float height, float length, Color color, float thickness = 0.02f)
    {
        var padding = 0.01f; // To prevent z fighting
        
        // Calculate min/max corners
        Vector3 min = position - new Vector3(width + padding, height + padding, length + padding) * 0.5f;
        Vector3 max = position + new Vector3(width + padding, height + padding, length + padding) * 0.5f;

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

        // Get camera up and forward
        Camera3D cam = VoxelTerrainScene.Player.Camera;
        Vector3 camPos = cam.Position;
        Vector3 camUp = cam.Up;
        Vector3 camForward = Vector3.Normalize(cam.Target - cam.Position);

        // Draw each edge with thickness
        for (int i = 0; i < 12; i++)
        {
            DrawThickLine3D(corners[edges[i,0]], corners[edges[i,1]], color, thickness, camPos, camUp, camForward);
        }
    }

    /// <summary>
    /// Draws a 3D line with thickness as a quad facing the camera.
    /// </summary>
    private static void DrawThickLine3D(Vector3 start, Vector3 end, Color color, float thickness, Vector3 camPos, Vector3 camUp, Vector3 camForward)
    {
        Vector3 dir = Vector3.Normalize(end - start);

        // Find a vector perpendicular to the line direction and the camera forward
        Vector3 side = Vector3.Cross(dir, camForward);
        
        if (side.Length() < 0.001f)
        {
            // If the line is parallel to the camera forward, cross with Up
            side = Vector3.Cross(dir, camUp);
        }
        
        side = Vector3.Normalize(side) * (thickness * 0.5f);

        // 4 quad vertices
        Vector3 v1 = start + side;
        Vector3 v2 = start - side;
        Vector3 v3 = end - side;
        Vector3 v4 = end + side;

        // Draw the line as a quad (two triangles)
        Raylib.DrawTriangle3D(v1, v2, v3, color);
        Raylib.DrawTriangle3D(v1, v3, v4, color);
    }
    
    /// <summary>
    /// Draws a plane at the given position, size, and up direction.
    /// </summary>
    /// <param name="centerPos">Center position of the plane.</param>
    /// <param name="size">Size of the plane (X=width, Y=length).</param>
    /// <param name="color">Color of the plane.</param>
    /// <param name="up">Up direction of the plane (default: Vector3.UnitY).</param>
    public static void DrawPlane(Vector3 centerPos, Vector2 size, Color color, Vector3? up = null)
    {
        // Default up is +Y
        Vector3 upDir = up ?? Vector3.UnitY;
        upDir = Vector3.Normalize(upDir);

        // Choose an arbitrary right vector
        Vector3 arbitrary = Math.Abs(Vector3.Dot(upDir, Vector3.UnitX)) < 0.99f ? Vector3.UnitX : Vector3.UnitZ;
        Vector3 right = Vector3.Normalize(Vector3.Cross(upDir, arbitrary));
        Vector3 forward = Vector3.Normalize(Vector3.Cross(right, upDir));

        float halfWidth = size.X * 0.5f;
        float halfLength = size.Y * 0.5f;

        // Plane corners (counter-clockwise winding for visible front face)
        Vector3 p0 = centerPos + (-right * halfWidth) + (-forward * halfLength); // Bottom-left
        Vector3 p1 = centerPos + (-right * halfWidth) + ( forward * halfLength); // Top-left
        Vector3 p2 = centerPos + ( right * halfWidth) + ( forward * halfLength); // Top-right
        Vector3 p3 = centerPos + ( right * halfWidth) + (-forward * halfLength); // Bottom-right

        // Draw as two triangles (counter-clockwise)
        Raylib.DrawTriangle3D(p0, p1, p2, color);
        Raylib.DrawTriangle3D(p0, p2, p3, color);
    }
}
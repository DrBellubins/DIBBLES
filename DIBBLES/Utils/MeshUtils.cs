/*using Raylib_cs;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using DIBBLES.Terrain;

namespace DIBBLES.Utils;

public static class MeshUtils
{
    public static Model GenTexturedCube(Texture2D texture)
    {
        Mesh cubeMesh = Raylib.GenMeshCube(1f, 1f, 1f);
        Model cubeModel = Raylib.LoadModelFromMesh(cubeMesh);
        
        // Use the default material or create your own
        Material material = Raylib.LoadMaterialDefault();
        Raylib.SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);

        // Set the model's material
        unsafe { cubeModel.Materials[0] = material; }
        
        return cubeModel;
    }
    
    public static Model GenTexturedCubeIcon(Texture2D texture)
    {
        // Add per-face vertex color data
        Color[] faceColors = new Color[]
        {
            new Color(180,180,180,255), // Right (-Z)
            new Color(0,0,0,0),         // Unused (+Z)
            new Color(150,150,150,255), // Left (-X)
            new Color(0,0,0,0),         // Unused (+X)
            new Color(255,255,255,255), // Top (-Y)
            new Color(0,0,0,0),         // Unused (+Y)
        };
        
        Mesh cubeMesh = GenMeshCubeWithColors(1f, 1f, 1f, faceColors);
        Model cubeModel = Raylib.LoadModelFromMesh(cubeMesh);

        Material material = Raylib.LoadMaterialDefault();
        Raylib.SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);
        
        unsafe { cubeModel.Materials[0] = material; }
        
        return cubeModel;
    }

    public static void SetModelTexture(Model model, Texture2D texture)
    {
        unsafe{ Raylib.SetMaterialTexture(ref model.Materials[0], MaterialMapIndex.Albedo, texture); }
    }
    
    public static Mesh GenMeshCubeWithColors(float width, float height, float length, Color[] faceColors)
    {
        // 6 faces * 4 vertices per face = 24 vertices (no sharing, so each face can be colored uniquely)
        int vertexCount = 24;
        int triangleCount = 12; // 6 faces * 2 triangles per face
    
        float x = width * 0.5f;
        float y = height * 0.5f;
        float z = length * 0.5f;
    
        // Vertices (CCW for each face as seen from outside)
        float[] vertices = new float[]
        {
            // Front (-Z)
            -x, -y, -z,   -x,  y, -z,    x,  y, -z,    x, -y, -z,
            // Back (+Z)
            x, -y,  z,    x,  y,  z,   -x,  y,  z,   -x, -y,  z,
            // Left (-X)
            -x, -y,  z,   -x,  y,  z,   -x,  y, -z,   -x, -y, -z,
            // Right (+X)
            x, -y, -z,    x,  y, -z,    x,  y,  z,    x, -y,  z,
            // Bottom (-Y)
            -x, -y,  z,   -x, -y, -z,    x, -y, -z,    x, -y,  z,
            // Top (+Y)
            -x,  y, -z,   -x,  y,  z,    x,  y,  z,    x,  y, -z
        };

        // Normals (one per face)
        float[] normals = new float[]
        {
            // Front
            0, 0, -1,   0, 0, -1,   0, 0, -1,   0, 0, -1,
            // Back
            0, 0, 1,    0, 0, 1,    0, 0, 1,    0, 0, 1,
            // Left
            -1, 0, 0,   -1, 0, 0,   -1, 0, 0,   -1, 0, 0,
            // Right
            1, 0, 0,    1, 0, 0,    1, 0, 0,    1, 0, 0,
            // Bottom
            0, -1, 0,   0, -1, 0,   0, -1, 0,   0, -1, 0,
            // Top
            0, 1, 0,    0, 1, 0,    0, 1, 0,    0, 1, 0
        };

        // UVs (standard 0-1 quad per face)
        float[] texcoords = new float[]
        {
            0, 1,   0, 0,   1, 0,   1, 1, // Front
            0, 1,   0, 0,   1, 0,   1, 1, // Back
            0, 1,   0, 0,   1, 0,   1, 1, // Left
            0, 1,   0, 0,   1, 0,   1, 1, // Right
            0, 1,   0, 0,   1, 0,   1, 1, // Bottom
            0, 1,   0, 0,   1, 0,   1, 1  // Top
        };

        ushort[] indices = new ushort[]
        {
            // Each face: 0,2,1  0,3,2
            0,2,1, 0,3,2,
            4,6,5, 4,7,6,
            8,10,9, 8,11,10,
            12,14,13, 12,15,14,
            16,18,17, 16,19,18,
            20,22,21, 20,23,22
        };
    
        // Per-vertex color (4 bytes per vertex, RGBA)
        byte[] colors = new byte[vertexCount * 4];
        
        for (int face = 0; face < 6; face++)
        {
            Color c = faceColors[face];
            for (int v = 0; v < 4; v++)
            {
                int idx = (face * 4 + v) * 4;
                colors[idx + 0] = c.R;
                colors[idx + 1] = c.G;
                colors[idx + 2] = c.B;
                colors[idx + 3] = c.A;
            }
        }
    
        // Allocate unmanaged memory and copy arrays
        unsafe
        {
            Mesh mesh = new Mesh();
            mesh.VertexCount = vertexCount;
            mesh.TriangleCount = triangleCount;
    
            mesh.Vertices = (float*)Marshal.AllocHGlobal(sizeof(float) * vertices.Length);
            mesh.Normals = (float*)Marshal.AllocHGlobal(sizeof(float) * normals.Length);
            mesh.TexCoords = (float*)Marshal.AllocHGlobal(sizeof(float) * texcoords.Length);
            mesh.Indices = (ushort*)Marshal.AllocHGlobal(sizeof(ushort) * indices.Length);
            mesh.Colors = (byte*)Marshal.AllocHGlobal(sizeof(byte) * colors.Length);
    
            Marshal.Copy(vertices, 0, (IntPtr)mesh.Vertices, vertices.Length);
            Marshal.Copy(normals, 0, (IntPtr)mesh.Normals, normals.Length);
            Marshal.Copy(texcoords, 0, (IntPtr)mesh.TexCoords, texcoords.Length);
            Marshal.Copy(colors, 0, (IntPtr)mesh.Colors, colors.Length);
            
            for (int i = 0; i < indices.Length; i++)
                mesh.Indices[i] = indices[i];
    
            Raylib.UploadMesh(ref mesh, false);
    
            return mesh;
        }
    }
}*/
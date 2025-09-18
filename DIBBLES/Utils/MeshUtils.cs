using Raylib_cs;
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
            new Color(100,100,100,255), // Front (-Z)
            new Color(140,140,140,255), // Back (+Z)
            new Color(180,180,180,255), // Left (-X)
            new Color(140,140,140,255), // Right (+X)
            new Color(120,120,120,255), // Bottom (-Y)
            new Color(255,255,255,255), // Top (+Y)
        };
        
        Mesh cubeMesh = GenMeshCubeWithColors(1f, 1f, 1f, faceColors);

        // Each cube face is 4 vertices, 6 faces = 24 vertices (if no vertex sharing)
        // If the mesh only has 8 shared vertices, you'll need to "explode" it first.
        // Raylib's GenMeshCube uses 24 vertices, so it's safe!

        unsafe
        {
            byte* colors = (byte*)Raylib.MemAlloc(sizeof(byte) * 24 * 4);
            
            for (int face = 0; face < 6; face++)
            {
                Color c = faceColors[face];
                
                for (int i = 0; i < 4; i++)
                {
                    int idx = face * 4 + i;
                    colors[idx * 4 + 0] = c.R;
                    colors[idx * 4 + 1] = c.G;
                    colors[idx * 4 + 2] = c.B;
                    colors[idx * 4 + 3] = c.A;
                }
            }
            
            cubeMesh.Colors = colors;
        }

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
            // Each face: 0,1,2  0,2,3
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            8,9,10, 8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23
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
    
    // Generates a plane mesh with configurable UV tiling
    public static unsafe Mesh GenMeshPlaneTiled(float width, float length, int resX, int resZ, float tileU, float tileV)
    {
        resX = Math.Max(resX, 1);
        resZ = Math.Max(resZ, 1);

        int vertCount = (resX + 1) * (resZ + 1);
        int triCount = resX * resZ * 2;

        float[] vertices = new float[vertCount * 3];
        float[] normals = new float[vertCount * 3];
        float[] texcoords = new float[vertCount * 2];
        ushort[] indices = new ushort[triCount * 3];

        int v = 0, t = 0;
        for (int z = 0; z <= resZ; z++)
        {
            float pz = ((float)z / resZ - 0.5f) * length;
            float tv = ((float)z / resZ) * tileV;
            for (int x = 0; x <= resX; x++)
            {
                float px = ((float)x / resX - 0.5f) * width;
                float tu = ((float)x / resX) * tileU;

                vertices[v * 3 + 0] = px;
                vertices[v * 3 + 1] = 0.0f;
                vertices[v * 3 + 2] = pz;

                normals[v * 3 + 0] = 0.0f;
                normals[v * 3 + 1] = 1.0f;
                normals[v * 3 + 2] = 0.0f;

                texcoords[t * 2 + 0] = tu;
                texcoords[t * 2 + 1] = tv;

                v++;
                t++;
            }
        }

        int i = 0;
        for (int z = 0; z < resZ; z++)
        {
            for (int x = 0; x < resX; x++)
            {
                int start = z * (resX + 1) + x;

                // First triangle
                indices[i++] = (ushort)(start);
                indices[i++] = (ushort)(start + resX + 1);
                indices[i++] = (ushort)(start + 1);

                // Second triangle
                indices[i++] = (ushort)(start + 1);
                indices[i++] = (ushort)(start + resX + 1);
                indices[i++] = (ushort)(start + resX + 2);
            }
        }

        // Allocate unmanaged memory and copy data
        float* vertPtr = (float*)Marshal.AllocHGlobal(sizeof(float) * vertices.Length);
        float* normPtr = (float*)Marshal.AllocHGlobal(sizeof(float) * normals.Length);
        float* texPtr  = (float*)Marshal.AllocHGlobal(sizeof(float) * texcoords.Length);
        ushort* indPtr = (ushort*)Marshal.AllocHGlobal(sizeof(ushort) * indices.Length);

        Marshal.Copy(vertices, 0, (IntPtr)vertPtr, vertices.Length);
        Marshal.Copy(normals, 0, (IntPtr)normPtr, normals.Length);
        Marshal.Copy(texcoords, 0, (IntPtr)texPtr, texcoords.Length);
        
        for (int idx = 0; idx < indices.Length; idx++)
            indPtr[idx] = indices[idx];

        Mesh mesh = new Mesh();
        mesh.VertexCount = vertCount;
        mesh.TriangleCount = triCount;
        mesh.Vertices = vertPtr;
        mesh.Normals = normPtr;
        mesh.TexCoords = texPtr;
        mesh.Indices = indPtr;
        
        Raylib.UploadMesh(ref mesh, false);
        
        return mesh;
    }
}
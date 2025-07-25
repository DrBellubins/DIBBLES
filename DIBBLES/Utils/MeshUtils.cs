using Raylib_cs;
using System;
using System.Runtime.InteropServices;

namespace DIBBLES.Utils;

public static class MeshUtils
{
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
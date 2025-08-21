using System;

namespace DIBBLES.Utils;

public class MeshData
{
    public float[] Vertices;
    public float[] Normals;
    public float[] TexCoords;
    public byte[] Colors;
    public ushort[] Indices;

    public int VertexCount;
    public int TriangleCount;

    public MeshData(int vertCount, int triCount)
    {
        Vertices = new float[vertCount * 3];
        Normals = new float[vertCount * 3];
        TexCoords = new float[vertCount * 2];
        Colors = new byte[vertCount * 4];
        Indices = new ushort[triCount * 3];
        VertexCount = vertCount;
        TriangleCount = triCount;
    }
}
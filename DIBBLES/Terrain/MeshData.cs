using System;

namespace DIBBLES.Terrain;

public struct MeshData
{
    public float[] Vertices;
    public float[] Normals;
    public float[] TexCoords;
    public byte[] Colors;
    public ushort[] Indices;

    // Per-vertex atlas sub-rect (x,y,w,h) and orientation basis (uX,uY,vX,vY)
    public float[] UVRects;
    public float[] UVBasis;
    
    public float[] Emissives;
    
    public int VertexCount;
    public int TriangleCount;

    public MeshData(int vertCount, int triCount)
    {
        Vertices = new float[vertCount * 3];
        Normals = new float[vertCount * 3];
        TexCoords = new float[vertCount * 2];
        Colors = new byte[vertCount * 4];
        Indices = new ushort[triCount * 3];
        
        UVRects = new float[vertCount * 4];
        UVBasis = new float[vertCount * 4];
        
        Emissives = new float[vertCount];
        
        VertexCount = vertCount;
        TriangleCount = triCount;
    }
}
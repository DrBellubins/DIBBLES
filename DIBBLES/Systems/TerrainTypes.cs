using System.Numerics;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Systems;

public enum BlockType
{
    Dirt,
    Grass,
    Stone
}

public class Block
{
    public BlockType Type;
    public Vector3 Position;
}

public class Chunk
{
    public int ID;
    public Vector3 Position;
    public byte[,,] VoxelData;
    public Model Model;

    public Chunk(Vector3 pos)
    {
        ID = GMath.NextInt(-int.MaxValue, int.MaxValue);
        Position = pos;
        VoxelData = new byte[TerrainGeneration.ChunkSize, TerrainGeneration.ChunkHeight, TerrainGeneration.ChunkSize];
    }
}
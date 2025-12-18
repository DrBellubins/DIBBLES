using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class NeighborCache
{
    public Chunk PosX;
    public Chunk NegX;
    public Chunk PosY;
    public Chunk NegY;
    public Chunk PosZ;
    public Chunk NegZ;
    
    public static NeighborCache Build(Chunk chunk)
    {
        var pos = chunk.Position;

        ChunkBuffer.TryGetValue(pos + new Vector3Int(ChunkSize, 0, 0), out var px);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(-ChunkSize, 0, 0), out var nx);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, ChunkSize, 0), out var py);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, -ChunkSize, 0), out var ny);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, 0, ChunkSize), out var pz);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, 0, -ChunkSize), out var nz);

        return new NeighborCache
        {
            PosX = px,
            NegX = nx,
            PosY = py,
            NegY = ny,
            PosZ = pz,
            NegZ = nz
        };
    }
}
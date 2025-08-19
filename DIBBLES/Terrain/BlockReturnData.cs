using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public struct BlockReturnData()
{
    public Block CurrentBlock = new(Vector3Int.Zero, Block.Prefabs[BlockType.Air]);
    public bool FoundSurface;   // Default false
    public Vector3Int WorldPos;
    public int IslandDepth;     // Default 0
    public int SurfaceY;
}
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public struct BlockReturnData()
{
    public BlockData CurrentBlock;
    public bool FoundSurface;   // Default false
    public Vector3Int WorldPos;
    public int IslandDepth;     // Default 0
    public SeededRandom RNG;
    public FastNoiseLite Noise;
}
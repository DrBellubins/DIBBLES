using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public struct BlockReturnData()
{
    public Vector3Int WorldPos;
    public bool FoundSurface;   // Default false
    public int IslandDepth;     // Default 0
    public SeededRandom RNG;
    public FastNoiseLite Noise;
}
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public struct BlockData
{
    // Only run-time modified data (NO BlockInfo stuff, that is for block prefabs!)
    public Vector3Int Position;
    public TerrainBiome Biome;
    public byte LightLevel;
    public bool GeneratedInsideIsland;
}

public class ChunkComponent
{
    public Vector3Int Position;
    public BlockData[] Blocks; // Flat array for locality.

    public ChunkComponent(Vector3Int pos)
    {
        Position = pos;
        Blocks = new BlockData[TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize];
    }

    // Helper for flat indexing
    public BlockData GetBlock(int x, int y, int z)
    {
        int index = x + TerrainGeneration.ChunkSize * (y + TerrainGeneration.ChunkSize * z);
        return Blocks[index];
    }

    public void SetBlock(int x, int y, int z, BlockData data)
    {
        int index = x + TerrainGeneration.ChunkSize * (y + TerrainGeneration.ChunkSize * z);
        Blocks[index] = data;
    }
}

public class TerrainTypesECS
{
    
}
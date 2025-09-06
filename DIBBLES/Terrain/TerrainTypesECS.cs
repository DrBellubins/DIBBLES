using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public struct BlockData
{
    // Only run-time modified data (NO BlockInfo stuff, that is for block prefabs!)
    public BlockType Type;
    public Vector3Int Position;
    public TerrainBiome Biome;
    public BlockInfo Info;
    public byte LightLevel;
    public bool GeneratedInsideIsland;

    public BlockData(Vector3Int position, BlockType type)
    {
        var info = Block.Prefabs[type];
        
        Type = type;
        Position = position;
        Biome = TerrainBiome.Plains;
        Info = info;
        LightLevel = info.LightEmission;
        
        GeneratedInsideIsland = false;
    }
}

public class ChunkComponent
{
    public Vector3Int Position;
    public ChunkInfo Info;
    public BlockData[] Blocks; // Flat array for locality.

    public ChunkGenerationState GenerationState = ChunkGenerationState.Uninitialized;
    
    public ChunkComponent(Vector3Int pos)
    {
        Position = pos;
        Info = new ChunkInfo();
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
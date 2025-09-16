using DIBBLES.Utils;

namespace DIBBLES.Terrain;

// Only run-time modified data (NO BlockInfo stuff, that is for block prefabs!)
public struct Block
{
    public BlockType Type;
    public Vector3Int Position;
    public TerrainBiome Biome;
    public BlockInfo Info;
    public byte LightLevel;
    public bool GeneratedInsideIsland;

    public bool SkyExposed = false;
    
    public bool IsValid => Type != BlockType.Air;
    
    public Block(Vector3Int position, BlockType type)
    {
        var info = BlockData.Prefabs[type];
        
        Type = type;
        Position = position;
        Biome = TerrainBiome.Plains;
        Info = info;
        LightLevel = info.LightEmission;
        
        GeneratedInsideIsland = false;
    }
}

public class Chunk
{
    public Vector3Int Position;
    public Block[] Blocks; // Flat array for locality.

    public ChunkGenerationState GenerationState = ChunkGenerationState.Uninitialized;
    
    public Chunk(Vector3Int pos)
    {
        Position = pos;
        Blocks = new Block[TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize];
    }

    // Helper for flat indexing
    public Block GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= TerrainGeneration.ChunkSize ||
            y < 0 || y >= TerrainGeneration.ChunkSize ||
            z < 0 || z >= TerrainGeneration.ChunkSize)
        {
            // Return Air block if out of bounds
            return new Block(new Vector3Int(x, y, z), BlockType.Air);
        }

        int index = x + TerrainGeneration.ChunkSize * (y + TerrainGeneration.ChunkSize * z);
        return Blocks[index];
    }

    public void SetBlock(int x, int y, int z, Block data)
    {
        int index = x + TerrainGeneration.ChunkSize * (y + TerrainGeneration.ChunkSize * z);
        Blocks[index] = data;
    }
}
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

    public bool IsAir => Type == BlockType.Air;
    
    public Block(Vector3Int position, BlockType type)
    {
        var info = BlockData.Prefabs[type];
        
        Type = type;
        Position = position;
        Biome = TerrainBiome.Plains;
        Info = info;
        LightLevel = info.LightEmission;
    }
}

public enum ChunkGenerationStage
{
    Uninitialized,
    ChunkData,
    Decorations,
    Lighting,
    Meshing
}

public class Chunk
{
    public Vector3Int Position;
    public Block[] Blocks; // Flat array for locality.

    public bool IsModified = false;
    
    public ChunkGenerationStage GenerationStage = ChunkGenerationStage.Uninitialized;
    
    public Chunk(Vector3Int pos)
    {
        Position = pos;
        Blocks = new Block[TerrainGenerationNew.ChunkSize * TerrainGenerationNew.ChunkSize * TerrainGenerationNew.ChunkSize];
    }

    // Helper for flat indexing
    public Block GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= TerrainGenerationNew.ChunkSize ||
            y < 0 || y >= TerrainGenerationNew.ChunkSize ||
            z < 0 || z >= TerrainGenerationNew.ChunkSize)
        {
            // Return Air block if out of bounds
            return new Block(new Vector3Int(x, y, z), BlockType.Air);
        }

        int index = x + TerrainGenerationNew.ChunkSize * (y + TerrainGenerationNew.ChunkSize * z);
        return Blocks[index];
    }

    public void SetBlock(int x, int y, int z, Block data)
    {
        int index = x + TerrainGenerationNew.ChunkSize * (y + TerrainGenerationNew.ChunkSize * z);
        Blocks[index] = data;
    }
}
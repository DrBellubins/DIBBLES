using DIBBLES.Utils;

namespace DIBBLES.Terrain;

// Only run-time data (NO BlockInfo stuff, that is for block prefabs!)
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

    public byte[] BlockTypes;
    public byte[] LightLevels;
    public byte[] Biomes;
    
    public bool IsModified = false;
    
    public ChunkGenerationStage GenerationStage = ChunkGenerationStage.Uninitialized;
    
    public Chunk(Vector3Int pos)
    {
        Position = pos;
        
        BlockTypes =  new byte[TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize];
        LightLevels =  new byte[TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize];
        Biomes =  new byte[TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize * TerrainGeneration.ChunkSize];
    }

    // Helper for flat indexing
    private int ToIndex(int x, int y, int z)
    {
        return x + TerrainGeneration.ChunkSize * (y + TerrainGeneration.ChunkSize * z);
    }
    
    // Get block at position
    public Block GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= TerrainGeneration.ChunkSize ||
            y < 0 || y >= TerrainGeneration.ChunkSize ||
            z < 0 || z >= TerrainGeneration.ChunkSize)
        {
            // Return Air block if out of bounds
            return new Block(new Vector3Int(Position.X + x, Position.Y + y, Position.Z + z), BlockType.Air);
        }

        int index = ToIndex(x, y, z);
        
        var position = new Vector3Int(Position.X + x, Position.Y + y, Position.Z + z);
        var type = (BlockType)BlockTypes[index];
        var biome = (TerrainBiome)Biomes[index];
        var light = LightLevels[index];
        
        var info = BlockData.Prefabs[type];

        // Construct Block with all info
        return new Block(position, type)
        {
            Biome = biome,
            LightLevel = light,
            Info = info
        };
    }

    // Write Block's values back into arrays
    public void SetBlock(int x, int y, int z, Block block)
    {
        if (x < 0 || x >= TerrainGeneration.ChunkSize ||
            y < 0 || y >= TerrainGeneration.ChunkSize ||
            z < 0 || z >= TerrainGeneration.ChunkSize)
        {
            // Out of bounds, do nothing or throw exception
            return;
        }

        int index = ToIndex(x, y, z);
        BlockTypes[index] = (byte)block.Type;
        Biomes[index] = (byte)block.Biome;
        LightLevels[index] = block.LightLevel;
        // Note: Info is not stored per-block, it's static in BlockData.Prefabs
    }
}
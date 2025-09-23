using DIBBLES.Utils;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

// Only run-time data (NO BlockInfo stuff, that is for block prefabs!)
public struct Block
{
    public BlockType Type;
    public Vector3Int Position;
    public TerrainBiome Biome;
    public BlockInfo Info;
    public byte LightLevel;
    
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
        
        BlockTypes =  new byte[ChunkSize * ChunkSize * ChunkSize];
        LightLevels =  new byte[ChunkSize * ChunkSize * ChunkSize];
        Biomes =  new byte[ChunkSize * ChunkSize * ChunkSize];
    }

    // Helper for flat indexing
    private int ToIndex(int x, int y, int z)
    {
        return x + ChunkSize * (y + ChunkSize * z);
    }
    
    /// <summary>
    /// Helper function for TerrainGameplay only!!
    /// </summary>
    public Block GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
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

    /// <summary>
    /// Helper function for TerrainGameplay only!!
    /// </summary>
    public void SetBlock(int x, int y, int z, Block block)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
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
    
    public BlockType GetTypeAt(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
            return BlockType.Air;
        
        return (BlockType)BlockTypes[ToIndex(x, y, z)];
    }

    public byte GetLightLevelAt(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
            return 0;
        
        return LightLevels[ToIndex(x, y, z)];
    }

    public TerrainBiome GetBiomeAt(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
            return TerrainBiome.Plains;
        
        return (TerrainBiome)Biomes[ToIndex(x, y, z)];
    }
    
    public BlockInfo GetInfoAt(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
            return BlockData.Prefabs[BlockType.Air];
        
        var index = ToIndex(x, y, z);
        
        return BlockData.Prefabs[(BlockType)BlockTypes[index]];
    }
    
    public void SetTypeAt(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
            return;
        
        var  index = ToIndex(x, y, z);
        BlockTypes[index] = (byte)type;
    }

    public void SetLightLevelAt(int x, int y, int z, byte lightLevel)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
            return;
        
        var index = ToIndex(x, y, z);
        LightLevels[index] = lightLevel;
    }

    public void SetBiomeAt(int x, int y, int z, TerrainBiome biome)
    {
        if (x < 0 || x >= ChunkSize ||
            y < 0 || y >= ChunkSize ||
            z < 0 || z >= ChunkSize)
            return;
        
        var index = ToIndex(x, y, z);
        Biomes[index] = (byte)biome;
    }
    
    // Globals
    public static BlockType GetBlockTypeGlobal(Vector3Int worldPos)
    {
        int chunkX = (int)Math.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            return BlockType.Air;

        int localX = worldPos.X - chunkX;
        int localY = worldPos.Y - chunkY;
        int localZ = worldPos.Z - chunkZ;

        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkSize ||
            localZ < 0 || localZ >= ChunkSize)
            return BlockType.Air;

        return chunk.GetTypeAt(localX, localY, localZ);
    }
    
    public static BlockInfo GetBlockInfoGlobal(Vector3Int worldPos)
    {
        int chunkX = (int)Math.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            return BlockData.Prefabs[BlockType.Air];

        int localX = worldPos.X - chunkX;
        int localY = worldPos.Y - chunkY;
        int localZ = worldPos.Z - chunkZ;

        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkSize ||
            localZ < 0 || localZ >= ChunkSize)
            return BlockData.Prefabs[BlockType.Air];

        return chunk.GetInfoAt(localX, localY, localZ);
    }
    
    public static byte GetLightLevelGlobal(Vector3Int worldPos)
    {
        int chunkX = (int)Math.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            return 0;

        int localX = worldPos.X - chunkX;
        int localY = worldPos.Y - chunkY;
        int localZ = worldPos.Z - chunkZ;

        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkSize ||
            localZ < 0 || localZ >= ChunkSize)
            return 0;

        return chunk.GetLightLevelAt(localX, localY, localZ);
    }
    
    public static void SetBlockTypeGlobal(Vector3Int worldPos, BlockType type)
    {
        int chunkX = (int)Math.Floor((float)worldPos.X / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        int chunkY = (int)Math.Floor((float)worldPos.Y / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        int chunkZ = (int)Math.Floor((float)worldPos.Z / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!TerrainGeneration.ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            return;

        int localX = worldPos.X - chunkX;
        int localY = worldPos.Y - chunkY;
        int localZ = worldPos.Z - chunkZ;

        if (localX < 0 || localX >= TerrainGeneration.ChunkSize ||
            localY < 0 || localY >= TerrainGeneration.ChunkSize ||
            localZ < 0 || localZ >= TerrainGeneration.ChunkSize)
            return;

        chunk.SetTypeAt(localX, localY, localZ, type);
    }
    
    public static void SetLightLevelGlobal(Vector3Int worldPos, byte lightLevel)
    {
        int chunkX = (int)Math.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!TerrainGeneration.ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            return;

        int localX = worldPos.X - chunkX;
        int localY = worldPos.Y - chunkY;
        int localZ = worldPos.Z - chunkZ;

        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkSize ||
            localZ < 0 || localZ >= ChunkSize)
            return;

        chunk.SetLightLevelAt(localX, localY, localZ, lightLevel);
    }
}
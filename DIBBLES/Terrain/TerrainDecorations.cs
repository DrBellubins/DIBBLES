using DIBBLES.Utils;

namespace DIBBLES.Terrain;

/// <summary>
/// Things generated in the world after the terrain.
/// Trees, grass blades, buildings, etc.
/// </summary>
public class TerrainDecorations
{
    public void GenerateTrees(Vector3Int currentPosition)
    {
        var middleTrunkPosition = currentPosition + new Vector3Int(0, 2, 0);
        var isSpace = CheckSpace(middleTrunkPosition, new Vector3Int(3, 4, 3));

        if (isSpace)
        {
            
        }
    }

    public static bool CheckSpace(Vector3Int startPos, Vector3Int size)
    {
        // For each block in the region defined by startPos and size, check if it is BlockType.Air
        for (int dx = 0; dx < size.X; dx++)
        for (int dy = 0; dy < size.Y; dy++)
        for (int dz = 0; dz < size.Z; dz++)
        {
            Vector3Int checkPos = new Vector3Int(
                startPos.X + dx,
                startPos.Y + dy,
                startPos.Z + dz
            );

            // Find the chunk containing this block
            int chunkX = (int)Math.Floor((float)checkPos.X / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            int chunkY = (int)Math.Floor((float)checkPos.Y / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            int chunkZ = (int)Math.Floor((float)checkPos.Z / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;

            var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

            if (!TerrainGeneration.Chunks.TryGetValue(chunkCoord, out var chunk))
                return false; // Out of loaded world bounds or chunk missing

            int localX = checkPos.X - chunkX;
            int localY = checkPos.Y - chunkY;
            int localZ = checkPos.Z - chunkZ;

            // Bounds check (should always be safe due to chunk math, but just in case)
            if (localX < 0 || localX >= TerrainGeneration.ChunkSize ||
                localY < 0 || localY >= TerrainGeneration.ChunkSize ||
                localZ < 0 || localZ >= TerrainGeneration.ChunkSize)
                return false; // Out of chunk bounds

            var block = chunk.Blocks[localX, localY, localZ];
            
            if (block.Info.Type != BlockType.Air)
                return false; // Space is not empty
        }
        
        return true; // All positions are air
    }
}
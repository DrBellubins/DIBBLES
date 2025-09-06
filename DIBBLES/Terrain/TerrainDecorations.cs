using DIBBLES.Utils;

namespace DIBBLES.Terrain;

/// <summary>
/// Things generated in the world after the terrain.
/// Trees, grass blades, buildings, etc.
/// </summary>
public class TerrainDecorations
{
    public void GenerateTrees(Vector3Int surfacePos)
    {
        // Trunk: 1x4x1, Leaves: 3x3x3 centered at trunk top
        int trunkHeight = 4;
        Vector3Int trunkSize = new Vector3Int(1, trunkHeight, 1);
        Vector3Int leavesSize = new Vector3Int(3, 3, 3);

        Vector3Int trunkStart = surfacePos + new Vector3Int(0, 1, 0); // start at one above surface
        Vector3Int leavesStart = surfacePos + new Vector3Int(-1, trunkHeight, -1); // center leaves on trunk top

        // Check space for trunk and leaves
        bool spaceForTrunk = CheckSpace(trunkStart, trunkSize);
        bool spaceForLeaves = CheckSpace(leavesStart, leavesSize);

        if (!spaceForTrunk || !spaceForLeaves)
            return;

        // Place trunk
        for (int dy = 0; dy < trunkHeight; dy++)
        {
            Vector3Int pos = surfacePos + new Vector3Int(0, 1 + dy, 0);

            int chunkX = (int)Math.Floor((float)pos.X / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            int chunkY = (int)Math.Floor((float)pos.Y / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            int chunkZ = (int)Math.Floor((float)pos.Z / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

            if (!TerrainGeneration.ECSChunks.TryGetValue(chunkCoord, out var chunk))
                continue;

            int localX = pos.X - chunkX;
            int localY = pos.Y - chunkY;
            int localZ = pos.Z - chunkZ;

            if (localX < 0 || localX >= TerrainGeneration.ChunkSize ||
                localY < 0 || localY >= TerrainGeneration.ChunkSize ||
                localZ < 0 || localZ >= TerrainGeneration.ChunkSize)
                continue;

            chunk.SetBlock(localX, localY, localZ, new Block(pos, BlockType.WoodLog));
        }

        // Place leaves as a 3x3x3 cube centered at trunk top
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = 0; dy <= 2; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Vector3Int pos = surfacePos + new Vector3Int(dx, trunkHeight + dy, dz);

            int chunkX = (int)Math.Floor((float)pos.X / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            int chunkY = (int)Math.Floor((float)pos.Y / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            int chunkZ = (int)Math.Floor((float)pos.Z / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
            var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

            if (!TerrainGeneration.ECSChunks.TryGetValue(chunkCoord, out var chunk))
                continue;

            int localX = pos.X - chunkX;
            int localY = pos.Y - chunkY;
            int localZ = pos.Z - chunkZ;

            if (localX < 0 || localX >= TerrainGeneration.ChunkSize ||
                localY < 0 || localY >= TerrainGeneration.ChunkSize ||
                localZ < 0 || localZ >= TerrainGeneration.ChunkSize)
                continue;

            // Only place leaves if position is Air (don't overwrite trunk)
            if (chunk.GetBlock(localX, localY, localZ).Type == BlockType.Air)
                chunk.SetBlock(localX, localY, localZ, new Block(pos, BlockType.Leaves));
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

            if (!TerrainGeneration.ECSChunks.TryGetValue(chunkCoord, out var chunk))
                return false; // Out of loaded world bounds or chunk missing

            int localX = checkPos.X - chunkX;
            int localY = checkPos.Y - chunkY;
            int localZ = checkPos.Z - chunkZ;

            // Bounds check (should always be safe due to chunk math, but just in case)
            if (localX < 0 || localX >= TerrainGeneration.ChunkSize ||
                localY < 0 || localY >= TerrainGeneration.ChunkSize ||
                localZ < 0 || localZ >= TerrainGeneration.ChunkSize)
                return false; // Out of chunk bounds

            var block = chunk.GetBlock(localX, localY, localZ);
            
            if (block.Type != BlockType.Air)
                return false; // Space is not empty
        }
        
        return true; // All positions are air
    }
}
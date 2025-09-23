using DIBBLES.Utils;

namespace DIBBLES.Terrain;

/// <summary>
/// Things generated in the world after the terrain.
/// Trees, grass blades, buildings, etc.
/// </summary>
public class TerrainDecorations
{
    public void GenerateTrees(Vector3Int localSurfacePos, Chunk chunk)
    {
        // Convert local chunk pos to world pos
        Vector3Int worldSurfacePos = chunk.Position + localSurfacePos;

        int trunkHeight = 4;
        Vector3Int trunkSize = new Vector3Int(1, trunkHeight, 1);
        Vector3Int leavesSize = new Vector3Int(3, 3, 3);

        Vector3Int trunkStart = worldSurfacePos + new Vector3Int(0, 1, 0); // start at one above surface
        Vector3Int leavesStart = worldSurfacePos + new Vector3Int(-1, trunkHeight, -1); // center leaves on trunk top

        bool spaceForTrunk = CheckSpace(trunkStart, trunkSize);
        bool spaceForLeaves = CheckSpace(leavesStart, leavesSize);

        if (!spaceForTrunk || !spaceForLeaves)
            return;

        // Place trunk
        for (int dy = 0; dy < trunkHeight; dy++)
        {
            Vector3Int pos = worldSurfacePos + new Vector3Int(0, 1 + dy, 0);
            Chunk.SetBlockTypeGlobal(pos, BlockType.WoodLog);
        }

        // Place leaves
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = 0; dy <= 2; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Vector3Int pos = worldSurfacePos + new Vector3Int(dx, trunkHeight + dy, dz);
            // Only place leaves if position is Air (don't overwrite trunk)
            if (Chunk.GetBlockTypeGlobal(pos) == BlockType.Air)
                Chunk.SetBlockTypeGlobal(pos, BlockType.Leaves);
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

            if (!TerrainGeneration.ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
                return false; // Out of loaded world bounds or chunk missing

            int localX = checkPos.X - chunkX;
            int localY = checkPos.Y - chunkY;
            int localZ = checkPos.Z - chunkZ;

            // Bounds check (should always be safe due to chunk math, but just in case)
            if (localX < 0 || localX >= TerrainGeneration.ChunkSize ||
                localY < 0 || localY >= TerrainGeneration.ChunkSize ||
                localZ < 0 || localZ >= TerrainGeneration.ChunkSize)
                return false; // Out of chunk bounds

            var block = chunk.GetTypeAt(localX, localY, localZ);
            
            if (block != BlockType.Air)
                return false; // Space is not empty
        }
        
        return true; // All positions are air
    }
}
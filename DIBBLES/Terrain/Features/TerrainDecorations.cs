using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Features;

/// <summary>
/// Things generated in the world after the terrain.
/// Trees, grass blades, buildings, etc.
/// </summary>
public class TerrainDecorations
{
    public void Generate(Chunk chunk)
    {
        long chunkSeed = Seed 
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
        
        var rng = new SeededRandom(chunkSeed);
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        var decorations = new TerrainDecorations();
        
        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
        {
            for (int y = ChunkSize - 1; y >= 0; y--)
            {
                var currentBlockType =  chunk.GetTypeAt(x, y, z);
                var pos = new Vector3Int(x, y, z);

                if (currentBlockType == BlockType.Grass)
                {
                    // Grass blades/flowers
                    if (rng.NextChance(35f))
                    {
                        var worldAbove = chunk.Position + pos + new Vector3Int(0, 1, 0);
                        var aboveType = Chunk.GetBlockTypeGlobal(worldAbove);

                        if (aboveType.Item1 == BlockType.Air)
                            Chunk.SetBlockTypeGlobal(worldAbove, BlockType.GrassBlades);
                    }
                    
                    // Trees
                    if (rng.NextChance(0.5f))
                        decorations.GenerateTrees(pos, chunk);
                }
            }
        }
    }
    
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

            var gType = Chunk.GetBlockTypeGlobal(pos);
            
            // Only place leaves if position is Air (don't overwrite trunk)
            if (gType.Item1 == BlockType.Air)
                Chunk.SetBlockTypeGlobal(pos, BlockType.Leaves);
        }
    }

    public bool CheckSpace(Vector3Int startPos, Vector3Int size)
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
            int chunkX = (int)Math.Floor((float)checkPos.X / ChunkSize) * ChunkSize;
            int chunkY = (int)Math.Floor((float)checkPos.Y / ChunkSize) * ChunkSize;
            int chunkZ = (int)Math.Floor((float)checkPos.Z / ChunkSize) * ChunkSize;

            var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

            if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
                return false; // Out of loaded world bounds or chunk missing

            int localX = checkPos.X - chunkX;
            int localY = checkPos.Y - chunkY;
            int localZ = checkPos.Z - chunkZ;

            // Bounds check (should always be safe due to chunk math, but just in case)
            if (localX < 0 || localX >= ChunkSize ||
                localY < 0 || localY >= ChunkSize ||
                localZ < 0 || localZ >= ChunkSize)
                return false; // Out of chunk bounds

            var block = chunk.GetTypeAt(localX, localY, localZ);
            
            if (block != BlockType.Air)
                return false; // Space is not empty
        }
        
        return true; // All positions are air
    }
}
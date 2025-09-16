using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainLighting
{
    public void Generate(Chunk chunk)
    {
        // Step 1: Initialize block light from emissive blocks
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var block = chunk.GetBlock(x, y, z);

            if (block.Type == BlockType.Air)
                block.LightLevel = block.SkyExposed ? (byte)15 : (byte)0;
            else
                block.LightLevel = block.Info.LightEmission;
            
            chunk.SetBlock(x, y, z, block);
        }

        // Step 2: Propagate block light using BFS
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();

        // Enqueue all blocks in this chunk with block light > 0
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var block = chunk.GetBlock(x, y, z);
            
            if (block.LightLevel > 0)
                queue.Enqueue((chunk, new Vector3Int(x, y, z)));
        }

        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();
            var block = curChunk.GetBlock(pos.X, pos.Y, pos.Z);
            var lightLevel = block.LightLevel;
    
            // Skip if no light to propagate
            if (lightLevel <= 1) continue;
    
            // Define the six possible directions (±X, ±Y, ±Z)
            Vector3Int[] directions = {
                new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 1, 0),
                new Vector3Int(0, -1, 0),
                new Vector3Int(0, 0, 1),
                new Vector3Int(0, 0, -1)
            };
    
            // Check all six neighbors
            foreach (var dir in directions)
            {
                Vector3Int newPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);
        
                // Skip if out of bounds
                if (newPos.X < 0 || newPos.X >= ChunkSize || 
                    newPos.Y < 0 || newPos.Y >= ChunkSize || 
                    newPos.Z < 0 || newPos.Z >= ChunkSize)
                    continue;
        
                var neighborBlock = curChunk.GetBlock(newPos.X, newPos.Y, newPos.Z);
                
                // Only propagate to transparent (except leaves for thicker look) or air blocks
                if (neighborBlock.Type == BlockType.Air ||
                    (neighborBlock.Type != BlockType.Leaves && neighborBlock.Info.IsTransparent))
                {
                    byte newLight = (byte)(lightLevel - 1);
            
                    // Only update if the new light is brighter
                    if (newLight > neighborBlock.LightLevel)
                    {
                        neighborBlock.LightLevel = newLight;
                        chunk.SetBlock(newPos.X, newPos.Y, newPos.Z, neighborBlock);
                        
                        queue.Enqueue((curChunk, newPos)); // Add to queue for further propagation
                    }
                }
            }
        }
    }
    
    public void FloodFillSkyLight()
{
    // Step 0: Reset all air blocks to not sky-exposed
    foreach (var chunk in ECSChunks.Values)
    {
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var block = chunk.GetBlock(x, y, z);
            if (block.Type == BlockType.Air)
            {
                block.SkyExposed = false;
                chunk.SetBlock(x, y, z, block);
            }
        }
    }

    Queue<Vector3Int> queue = new();
    HashSet<Vector3Int> visited = new();

    int seeds = 0;
    // Enqueue only air blocks on chunk faces with no neighbor (the void)
    foreach (var chunk in ECSChunks.Values)
    {
        foreach (var (facePos, faceDir) in ChunkFaceAirBlocks(chunk))
        {
            var worldPos = chunk.Position + facePos;
            if (!HasNeighbor(chunk.Position, faceDir))
            {
                queue.Enqueue(worldPos);
                visited.Add(worldPos);
                seeds++;
            }
        }
    }

    Console.WriteLine($"[SkyFlood] Seed air blocks for BFS: {seeds}");

    // BFS flood fill
    int exposed = 0;
    while (queue.Count > 0)
    {
        var pos = queue.Dequeue();
        var block = TerrainUtils.GetBlockAtWorldPos(pos);

        if (!block.IsValid || block.Type != BlockType.Air)
            continue;

        if (block.SkyExposed)
            continue;

        block.SkyExposed = true;
        TerrainUtils.SetBlockAtWorldPos(pos, block);
        exposed++;

        // Flood to 6 neighbors
        foreach (var dir in FaceUtils.VoxelFaceInfos())
        {
            var npos = pos + dir.neighborOffset;
            if (visited.Contains(npos))
                continue;

            var nblock = TerrainUtils.GetBlockAtWorldPos(npos);
            if (nblock.Type == BlockType.Air && !nblock.SkyExposed)
            {
                queue.Enqueue(npos);
                visited.Add(npos);
            }
        }
    }

    Console.WriteLine($"[SkyFlood] Air blocks marked as sky-exposed: {exposed}");
}
    
    // Helper to yield all air block positions on the 6 faces of a chunk
    private IEnumerable<(Vector3Int localPos, Vector3Int faceDir)> ChunkFaceAirBlocks(Chunk chunk)
    {
        int size = ChunkSize;
        // -X face
        for (int y = 0; y < size; y++)
        for (int z = 0; z < size; z++)
            if (chunk.GetBlock(0, y, z).Type == BlockType.Air)
                yield return (new Vector3Int(0, y, z), new Vector3Int(-1, 0, 0));
        // +X face
        for (int y = 0; y < size; y++)
        for (int z = 0; z < size; z++)
            if (chunk.GetBlock(size - 1, y, z).Type == BlockType.Air)
                yield return (new Vector3Int(size - 1, y, z), new Vector3Int(1, 0, 0));
        // -Y face
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
            if (chunk.GetBlock(x, 0, z).Type == BlockType.Air)
                yield return (new Vector3Int(x, 0, z), new Vector3Int(0, -1, 0));
        // +Y face
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
            if (chunk.GetBlock(x, size - 1, z).Type == BlockType.Air)
                yield return (new Vector3Int(x, size - 1, z), new Vector3Int(0, 1, 0));
        // -Z face
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            if (chunk.GetBlock(x, y, 0).Type == BlockType.Air)
                yield return (new Vector3Int(x, y, 0), new Vector3Int(0, 0, -1));
        // +Z face
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            if (chunk.GetBlock(x, y, size - 1).Type == BlockType.Air)
                yield return (new Vector3Int(x, y, size - 1), new Vector3Int(0, 0, 1));
    }
    
    // Returns true if there is a neighbor chunk adjacent to chunkPos in the direction of faceRelPos
    private static bool HasNeighbor(Vector3Int chunkPos, Vector3Int faceRelPos)
    {
        // Only allow ±1 in any axis for faceRelPos
        if (faceRelPos == Vector3Int.Zero)
            return false;

        // Only one axis can be nonzero (should be a face direction)
        int countNonZero = 0;
        
        if (faceRelPos.X != 0) countNonZero++;
        if (faceRelPos.Y != 0) countNonZero++;
        if (faceRelPos.Z != 0) countNonZero++;
        
        if (countNonZero != 1)
            throw new ArgumentException("faceRelPos must be a face direction (one axis ±1)");

        var neighborChunkPos = chunkPos + (faceRelPos * ChunkSize);

        return ECSChunks.ContainsKey(neighborChunkPos);
    }
}
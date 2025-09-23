using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainLighting
{
    private const int BatchSize = 16;
    
    public void GenerateNew(Chunk chunk)
    {
        placeLights(chunk);
        floodFill(chunk);
    }

    private void placeLights(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
            chunk.SetLightLevelAt(x, y, z, chunk.GetInfoAt(x, y, z).LightEmission);
    }
    
    public void GenerateAllLighting(Vector3Int centerChunk)
    {
        // 1. Collect chunk positions in render distance
        int halfRD = RenderDistance / 2;
        List<Vector3Int> chunkPositions = new();

        for (int cx = centerChunk.X - halfRD; cx <= centerChunk.X + halfRD; cx++)
        for (int cy = centerChunk.Y - halfRD; cy <= centerChunk.Y + halfRD; cy++)
        for (int cz = centerChunk.Z - halfRD; cz <= centerChunk.Z + halfRD; cz++)
        {
            var chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
            if (ChunkBuffer.ContainsKey(chunkPos))
                chunkPositions.Add(chunkPos);
        }

        // 2. Divide into batches
        int batchCount = (chunkPositions.Count + BatchSize - 1) / BatchSize;
        ManualResetEvent[] batchEvents = new ManualResetEvent[batchCount];

        for (int batchIdx = 0; batchIdx < batchCount; batchIdx++)
        {
            batchEvents[batchIdx] = new ManualResetEvent(false);
            int startIdx = batchIdx * BatchSize;
            int endIdx = Math.Min(startIdx + BatchSize, chunkPositions.Count);

            // Capture batchIdx for closure
            var batchEvent = batchEvents[batchIdx];

            ThreadPool.QueueUserWorkItem(_ =>
            {
                // Each batch processes its chunk group
                Queue<(Chunk chunk, Vector3Int pos)> queue = new();

                for (int i = startIdx; i < endIdx; i++)
                {
                    var chunkPos = chunkPositions[i];
                    var chunk = ChunkBuffer[chunkPos];

                    placeLights(chunk);

                    for (int x = 0; x < ChunkSize; x++)
                    for (int y = 0; y < ChunkSize; y++)
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        byte blockLight = chunk.GetLightLevelAt(x, y, z);
                        if (blockLight > 0)
                            queue.Enqueue((chunk, new Vector3Int(x, y, z)));
                    }
                }

                // Flood-fill lighting for this batch
                while (queue.Count > 0)
                {
                    var (curChunk, pos) = queue.Dequeue();
                    var lightLevel = curChunk.GetLightLevelAt(pos.X, pos.Y, pos.Z);

                    if (lightLevel <= 1) continue;

                    Vector3Int[] directions = {
                        new Vector3Int(1, 0, 0),
                        new Vector3Int(-1, 0, 0),
                        new Vector3Int(0, 1, 0),
                        new Vector3Int(0, -1, 0),
                        new Vector3Int(0, 0, 1),
                        new Vector3Int(0, 0, -1)
                    };

                    foreach (var dir in directions)
                    {
                        Vector3Int neighborPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);

                        Chunk neighborChunk = curChunk;
                        Vector3Int localPos = neighborPos;

                        // Cross chunk border if needed
                        if (localPos.X < 0 || localPos.X >= ChunkSize ||
                            localPos.Y < 0 || localPos.Y >= ChunkSize ||
                            localPos.Z < 0 || localPos.Z >= ChunkSize)
                        {
                            Vector3Int worldPos = curChunk.Position + neighborPos;
                            int chunkX = (int)Math.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
                            int chunkY = (int)Math.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
                            int chunkZ = (int)Math.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
                            var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

                            if (!ChunkBuffer.TryGetValue(chunkCoord, out neighborChunk))
                                continue; // Only propagate to loaded chunks

                            localPos = new Vector3Int(worldPos.X - chunkX, worldPos.Y - chunkY, worldPos.Z - chunkZ);
                        }

                        var neighborBlockType = neighborChunk.GetTypeAt(localPos.X, localPos.Y, localPos.Z);
                        var neighborBlockInfo = neighborChunk.GetInfoAt(localPos.X, localPos.Y, localPos.Z);
                        var neighborBlockLightLevel = neighborChunk.GetLightLevelAt(localPos.X, localPos.Y, localPos.Z);

                        if (neighborBlockType == BlockType.Air ||
                            (neighborBlockType != BlockType.Leaves && neighborBlockInfo.IsTransparent))
                        {
                            byte newLight = (byte)(lightLevel - 1);
                            if (newLight > neighborBlockLightLevel)
                            {
                                neighborChunk.SetLightLevelAt(localPos.X, localPos.Y, localPos.Z, newLight);
                                queue.Enqueue((neighborChunk, localPos));
                            }
                        }
                    }
                }

                // Signal batch complete
                batchEvent.Set();
            });
        }

        // Wait for all batches to complete
        WaitHandle.WaitAll(batchEvents);
    }

    // TODO: Needs to be cross-chunk based on SetLightLevelGlobal
    private void floodFill(Chunk chunk)
    {
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();

        // Enqueue all blocks in this chunk with block light > 0
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var blockLightLevel = chunk.GetLightLevelAt(x, y, z);
            
            if (blockLightLevel > 0)
                queue.Enqueue((chunk, new Vector3Int(x, y, z)));
        }

        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();
            var lightLevel = curChunk.GetLightLevelAt(pos.X, pos.Y, pos.Z);
    
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
    
            foreach (var dir in directions)
            {
                Vector3Int neighborPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);

                Chunk neighborChunk = curChunk;
                Vector3Int localPos = neighborPos;

                // Check if neighbor is out of bounds
                if (localPos.X < 0 || localPos.X >= ChunkSize ||
                    localPos.Y < 0 || localPos.Y >= ChunkSize ||
                    localPos.Z < 0 || localPos.Z >= ChunkSize)
                {
                    // Convert to world position
                    Vector3Int worldPos = curChunk.Position + neighborPos;
                    // Find neighbor chunk
                    int chunkX = (int)Math.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
                    int chunkY = (int)Math.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
                    int chunkZ = (int)Math.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
                    var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

                    if (!ChunkBuffer.TryGetValue(chunkCoord, out neighborChunk))
                        continue; // If chunk isn't loaded, skip

                    // Local position in neighbor chunk
                    localPos = new Vector3Int(worldPos.X - chunkX, worldPos.Y - chunkY, worldPos.Z - chunkZ);
                }

                // Now propagate light to neighborChunk at localPos
                // (Same code as before)
                var neighborBlockType = neighborChunk.GetTypeAt(localPos.X, localPos.Y, localPos.Z);
                var neighborBlockInfo = neighborChunk.GetInfoAt(localPos.X, localPos.Y, localPos.Z);
                var neighborBlockLightLevel = neighborChunk.GetLightLevelAt(localPos.X, localPos.Y, localPos.Z);

                if (neighborBlockType == BlockType.Air ||
                    (neighborBlockType != BlockType.Leaves && neighborBlockInfo.IsTransparent))
                {
                    byte newLight = (byte)(lightLevel - 1);
                    if (newLight > neighborBlockLightLevel)
                    {
                        neighborChunk.SetLightLevelAt(localPos.X, localPos.Y, localPos.Z, newLight);
                        queue.Enqueue((neighborChunk, localPos));
                    }
                }
            }
        }
    }
}
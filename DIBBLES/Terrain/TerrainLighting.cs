using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainLighting
{
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
        // 1. Enqueue all light sources from all loaded chunks in render distance
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();
        HashSet<(Chunk, Vector3Int)> visited = new(); // Optional: avoid redundant BFS
    
        int halfRD = RenderDistance / 2;
        for (int cx = centerChunk.X - halfRD; cx <= centerChunk.X + halfRD; cx++)
        for (int cy = centerChunk.Y - halfRD; cy <= centerChunk.Y + halfRD; cy++)
        for (int cz = centerChunk.Z - halfRD; cz <= centerChunk.Z + halfRD; cz++)
        {
            var chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
            
            if (!ChunkBuffer.TryGetValue(chunkPos, out var chunk))
                continue;
            
            // Reset light levels (optional, if needed)
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
    
        // 2. Flood-fill BFS over all loaded chunks
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
    
                // Propagate
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
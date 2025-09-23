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
        // Step 1: Set block light for emissives
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
            chunk.SetLightLevelAt(x, y, z, chunk.GetInfoAt(x, y, z).LightEmission);
        
        // Step 2: Place skylights
        
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
    
            // Check all six neighbors
            foreach (var dir in directions)
            {
                Vector3Int newPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);
        
                // Skip if out of bounds
                if (newPos.X < 0 || newPos.X >= ChunkSize || 
                    newPos.Y < 0 || newPos.Y >= ChunkSize || 
                    newPos.Z < 0 || newPos.Z >= ChunkSize)
                    continue;
        
                
                var neighborBlockType = curChunk.GetTypeAt(newPos.X, newPos.Y, newPos.Z);
                var neighborBlockInfo = curChunk.GetInfoAt(newPos.X, newPos.Y, newPos.Z);
                var neighborBlockLightLevel = curChunk.GetLightLevelAt(newPos.X, newPos.Y, newPos.Z);
                
                // Only propagate to transparent (except leaves for thicker look) or air blocks
                if (neighborBlockType == BlockType.Air ||
                    (neighborBlockType != BlockType.Leaves && neighborBlockInfo.IsTransparent))
                {
                    byte newLight = (byte)(lightLevel - 1);
            
                    // Only update if the new light is brighter
                    if (newLight > neighborBlockLightLevel)
                    {
                        curChunk.SetLightLevelAt(newPos.X, newPos.Y, newPos.Z, newLight);
                        queue.Enqueue((curChunk, newPos)); // Add to queue for further propagation
                    }
                }
            }
        }
    }
    
    public void Generate(Chunk chunk)
    {
        // Step 1: Initialize block light from emissive blocks
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var blockType = chunk.GetTypeAt(x, y, z);
            var blockLightLevel = chunk.GetLightLevelAt(x, y, z);

            if (blockType == BlockType.Air)
                chunk.SetLightLevelAt(x, y, z, 15); // TEMP
            else
                chunk.SetLightLevelAt(x, y, z, chunk.GetInfoAt(x, y, z).LightEmission);
        }

        // Step 2: Propagate block light using BFS
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
    
            // Check all six neighbors
            foreach (var dir in directions)
            {
                Vector3Int newPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);
        
                // Skip if out of bounds
                if (newPos.X < 0 || newPos.X >= ChunkSize || 
                    newPos.Y < 0 || newPos.Y >= ChunkSize || 
                    newPos.Z < 0 || newPos.Z >= ChunkSize)
                    continue;
        
                
                var neighborBlockType = curChunk.GetTypeAt(newPos.X, newPos.Y, newPos.Z);
                var neighborBlockInfo = curChunk.GetInfoAt(newPos.X, newPos.Y, newPos.Z);
                var neighborBlockLightLevel = curChunk.GetLightLevelAt(newPos.X, newPos.Y, newPos.Z);
                
                // Only propagate to transparent (except leaves for thicker look) or air blocks
                if (neighborBlockType == BlockType.Air ||
                    (neighborBlockType != BlockType.Leaves && neighborBlockInfo.IsTransparent))
                {
                    byte newLight = (byte)(lightLevel - 1);
            
                    // Only update if the new light is brighter
                    if (newLight > neighborBlockLightLevel)
                    {
                        curChunk.SetLightLevelAt(newPos.X, newPos.Y, newPos.Z, newLight);
                        queue.Enqueue((curChunk, newPos)); // Add to queue for further propagation
                    }
                }
            }
        }
    }
}
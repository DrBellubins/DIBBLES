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
            var block = chunk.Blocks[x, y, z];

            if (!block.GeneratedInsideIsland)
            {
                if (block.Info.Type == BlockType.Air)
                    block.LightLevel = 15; // TEMP
                else
                    block.LightLevel = block.Info.LightEmission;
            }
            else
                block.LightLevel = block.Info.LightEmission;
        }

        // Step 2: Propagate block light using BFS
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();

        // Enqueue all blocks in this chunk with block light > 0
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var block = chunk.Blocks[x, y, z];
            
            if (block.LightLevel > 0 && !block.GeneratedInsideIsland)
                queue.Enqueue((chunk, new Vector3Int(x, y, z)));
        }

        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();
            var block = curChunk.Blocks[pos.X, pos.Y, pos.Z];
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
        
                var neighborBlock = curChunk.Blocks[newPos.X, newPos.Y, newPos.Z];
        
                // Only propagate to transparent or air blocks
                if (neighborBlock.Info.Type == BlockType.Air || neighborBlock.Info.IsTransparent)
                {
                    byte newLight = (byte)(lightLevel - 1);
            
                    // Only update if the new light is brighter
                    if (newLight > neighborBlock.LightLevel)
                    {
                        neighborBlock.LightLevel = newLight;
                        queue.Enqueue((curChunk, newPos)); // Add to queue for further propagation
                    }
                }
            }
        }
    }
}
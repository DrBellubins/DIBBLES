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

            if (!block.InsideIsland)
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
            
            if (block.LightLevel > 0)
                queue.Enqueue((chunk, new Vector3Int(x, y, z)));
        }

        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();
            var block = curChunk.Blocks[pos.X, pos.Y, pos.Z];
            var lightLevel = block.LightLevel;

            // Only propagate if neighbor is transparent or air
            if (block.Info.Type == BlockType.Air || block.Info.IsTransparent)
            {
                var newLight = (byte)(lightLevel - 1);
                    
                if (newLight > block.LightLevel)
                    block.LightLevel = newLight;
            }
        }
    }
}
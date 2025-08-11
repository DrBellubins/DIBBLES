using DIBBLES.Utils;

using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

public class TerrainLighting
{
    public void Generate(Chunk chunk)
    {
        // Initialize sky lighting - propagate from top down
        initializeSkyLighting(chunk);
        
        // Initialize block lighting from emissive blocks
        initializeBlockLighting(chunk);
        
        // Propagate lighting using BFS
        propagateLighting(chunk);
    }
    
    private void initializeSkyLighting(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                byte skyLight = 15;
                bool surfaceLit = false;
                for (int y = ChunkHeight - 1; y >= 0; y--)
                {
                    var block = chunk.Blocks[x, y, z];
                    if (block.Info.Type == BlockType.Air || block.Info.IsTransparent)
                    {
                        block.SkyLight = skyLight;
                    }
                    else
                    {
                        if (!surfaceLit)
                        {
                            // Light the first solid (surface) block with the current skylight
                            block.SkyLight = skyLight;
                            surfaceLit = true;
                        }
                        else
                        {
                            block.SkyLight = 0;
                        }
                        skyLight = 0;
                    }
                }
            }
        }
    }
    
    private void initializeBlockLighting(Chunk chunk)
    {
        // Initialize block light from emissive blocks
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    var block = chunk.Blocks[x, y, z];
                    block.BlockLight = block.Info.LightEmission;
                }
            }
        }
    }
    
    private void propagateLighting(Chunk chunk)
    {
        // Use BFS to propagate both sky and block light
        Queue<Vector3Int> lightQueue = new Queue<Vector3Int>();
        
        // Add all blocks with light to the queue for propagation
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    var block = chunk.Blocks[x, y, z];
                    if (block.SkyLight > 0 || block.BlockLight > 0)
                    {
                        lightQueue.Enqueue(new Vector3Int(x, y, z));
                    }
                }
            }
        }
        
        // Process light propagation
        while (lightQueue.Count > 0)
        {
            var pos = lightQueue.Dequeue();
            var block = chunk.Blocks[pos.X, pos.Y, pos.Z];
            
            // Check all 6 neighboring positions
            Vector3Int[] neighbors = {
                new Vector3Int(pos.X - 1, pos.Y, pos.Z),
                new Vector3Int(pos.X + 1, pos.Y, pos.Z),
                new Vector3Int(pos.X, pos.Y - 1, pos.Z),
                new Vector3Int(pos.X, pos.Y + 1, pos.Z),
                new Vector3Int(pos.X, pos.Y, pos.Z - 1),
                new Vector3Int(pos.X, pos.Y, pos.Z + 1)
            };
            
            foreach (var neighborPos in neighbors)
            {
                // Skip if outside chunk bounds (we'll handle cross-chunk later)
                if (neighborPos.X < 0 || neighborPos.X >= ChunkSize ||
                    neighborPos.Y < 0 || neighborPos.Y >= ChunkHeight ||
                    neighborPos.Z < 0 || neighborPos.Z >= ChunkSize)
                    continue;
                
                var neighbor = chunk.Blocks[neighborPos.X, neighborPos.Y, neighborPos.Z];
                
                // Skip if neighbor is opaque
                bool updated = false;

                // Propagate sky light ONLY to air/transparent
                if (neighbor.Info.IsTransparent || neighbor.Info.Type == BlockType.Air)
                {
                    if (block.SkyLight > 1) {
                        byte newSkyLight = (byte)(block.SkyLight - 1);
                        if (newSkyLight > neighbor.SkyLight) {
                            neighbor.SkyLight = newSkyLight;
                            updated = true;
                        }
                    }
                }

                // Propagate block light to ALL block types (even opaque)
                if (block.BlockLight > 1)
                {
                    byte newBlockLight = (byte)(block.BlockLight - 1);
                    
                    if (newBlockLight > neighbor.BlockLight)
                    {
                        neighbor.BlockLight = newBlockLight;
                        updated = true;
                    }
                }

                // Only add neighbor to queue if it is air/transparent OR if we updated its block light for the first time (so emission can light up one layer of stone, but not pass through)
                if (updated)
                {
                    if (neighbor.Info.IsTransparent || neighbor.Info.Type == BlockType.Air)
                        lightQueue.Enqueue(neighborPos);
                }
            }
        }
    }
}
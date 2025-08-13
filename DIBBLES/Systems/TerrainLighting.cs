using System.Numerics;
using DIBBLES.Utils;
using Raylib_cs;
using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

// TODO: Overhangs don't create shadows.
// TODO: Adjacent chunks need to be updated regardless
// of distance to edge.
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
    
    public void UpdateSkyLightColumn(Chunk chunk, int x, int z)
    {
        bool blocked = false;
        
        for (int y = ChunkSize - 1; y >= 0; y--)
        {
            var block = chunk.Blocks[x, y, z];

            if (!blocked && (block.Info.Type == BlockType.Air || block.Info.IsTransparent))
            {
                block.SkyLight = 15;
            }
            else
            {
                blocked = true;
                block.SkyLight = 0;
            }
        }
    }
    
    private void initializeSkyLighting(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                // Set all air/transparent blocks at the top to full sky light
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var block = chunk.Blocks[x, y, z];
                    
                    if (block.Info.Type == BlockType.Air || block.Info.IsTransparent)
                        block.SkyLight = 15;
                    else
                        break; // As soon as we hit a solid block, stop
                }
            }
        }
    }
    
    private void initializeBlockLighting(Chunk chunk)
    {
        // Initialize block light from emissive blocks
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
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
        Queue<(Chunk, Vector3Int)> lightQueue = new();
        
        // Add all blocks with light to the queue for propagation
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    var block = chunk.Blocks[x, y, z];
                    
                    if (block.SkyLight > 0 || block.BlockLight > 0)
                        lightQueue.Enqueue((chunk, new Vector3Int(x, y, z)));
                }
            }
        }
        
        // Process light propagation
        while (lightQueue.Count > 0)
        {
            var (curChunk, pos) = lightQueue.Dequeue();
            var block = curChunk.Blocks[pos.X, pos.Y, pos.Z];
            
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
                // Check for out-of-bounds (cross-chunk)
                Chunk neighborChunk = curChunk;
                Vector3Int localPos = neighborPos;
                
                if (neighborPos.X < 0 || neighborPos.X >= ChunkSize ||
                    neighborPos.Y < 0 || neighborPos.Y >= ChunkSize ||
                    neighborPos.Z < 0 || neighborPos.Z >= ChunkSize)
                {
                    // Compute neighbor chunk position
                    var chunkPos = curChunk.Position;
                    Vector3Int chunkOffset = Vector3Int.Zero;
                    
                    if (neighborPos.X < 0) { chunkOffset.X = -ChunkSize; localPos.X = ChunkSize - 1; }
                    else if (neighborPos.X >= ChunkSize) { chunkOffset.X = ChunkSize; localPos.X = 0; }
                    if (neighborPos.Z < 0) { chunkOffset.Z = -ChunkSize; localPos.Z = ChunkSize - 1; }
                    else if (neighborPos.Z >= ChunkSize) { chunkOffset.Z = ChunkSize; localPos.Z = 0; }
                    if (neighborPos.Y < 0 || neighborPos.Y >= ChunkSize) continue; // No chunk up/down in Y

                    var neighborChunkPos = chunkPos + chunkOffset;
                    
                    if (!Chunks.TryGetValue(neighborChunkPos, out neighborChunk))
                        continue; // No neighbor chunk loaded
                }
                
                var neighbor = neighborChunk.Blocks[localPos.X, localPos.Y, localPos.Z];
                
                // Skip if neighbor is opaque
                bool updated = false;

                // Propagate sky light ONLY to air/transparent
                if (neighbor.Info.IsTransparent || neighbor.Info.Type == BlockType.Air)
                {
                    if (block.SkyLight > 1)
                    {
                        byte newSkyLight = (byte)(block.SkyLight - 1);
                        
                        if (newSkyLight > neighbor.SkyLight)
                        {
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
                        lightQueue.Enqueue((neighborChunk, localPos));
                }
            }
        }
    }
    
    public void UpdateNeighborChunkLighting(Vector3 blockPos)
    {
        int[] edgeOffsets = { -1, 1 };

        var localX = (int)(blockPos.X % ChunkSize);
        var localZ = (int)(blockPos.Z % ChunkSize);

        Vector3Int chunkCoord = new Vector3Int(
            ((int)blockPos.X / ChunkSize) * ChunkSize,
            ((int)blockPos.Y / ChunkSize) * ChunkSize,
            ((int)blockPos.Z / ChunkSize) * ChunkSize
        );

        foreach (int dx in edgeOffsets)
        {
            if ((localX == 0 && dx == -1) || (localX == ChunkSize - 1 && dx == 1))
            {
                Vector3Int nChunkCoord = chunkCoord + new Vector3Int(0, 0, dx * ChunkSize);

                if (Chunks.TryGetValue(nChunkCoord, out var nChunk))
                {
                    Lighting.Generate(nChunk);
                    
                    Raylib.UnloadModel(nChunk.Model);
                    
                    var meshData = TMesh.GenerateMeshData(nChunk);
                    nChunk.Model = TMesh.UploadMesh(meshData);
                }
            }
        }
        
        foreach (int dz in edgeOffsets)
        {
            if ((localZ == 0 && dz == -1) || (localZ == ChunkSize - 1 && dz == 1))
            {
                Vector3Int nChunkCoord = chunkCoord + new Vector3Int(0, 0, dz * ChunkSize);

                if (Chunks.TryGetValue(nChunkCoord, out var nChunk))
                {
                    Lighting.Generate(nChunk);
                    
                    Raylib.UnloadModel(nChunk.Model);
                    
                    var meshData = TMesh.GenerateMeshData(nChunk);
                    nChunk.Model = TMesh.UploadMesh(meshData);
                }
            }
        }
    }
}
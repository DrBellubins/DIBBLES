using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Utils;
using Raylib_cs;
using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

/// <summary>
/// New 3D block lighting system. No skylight; only propagates block light from emissive blocks using BFS.
/// </summary>
public class TerrainLighting2
{
    /// <summary>
    /// Generate block lighting for a chunk. Propagates only block light (no skylight).
    /// </summary>
    public void Generate(Chunk chunk)
    {
        // Step 1: Initialize block light from emissive blocks
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var block = chunk.Blocks[x, y, z];
            block.BlockLight = block.Info.LightEmission;
        }

        // Step 2: Propagate block light using BFS
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();

        // Enqueue all blocks in this chunk with block light > 0
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var block = chunk.Blocks[x, y, z];
            if (block.BlockLight > 0)
                queue.Enqueue((chunk, new Vector3Int(x, y, z)));
        }

        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();
            var block = curChunk.Blocks[pos.X, pos.Y, pos.Z];
            byte lightLevel = block.BlockLight;

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
                Chunk neighborChunk = curChunk;
                Vector3Int localPos = neighborPos;

                // Handle cross-chunk neighbors
                if (neighborPos.X < 0 || neighborPos.X >= ChunkSize ||
                    neighborPos.Y < 0 || neighborPos.Y >= ChunkSize ||
                    neighborPos.Z < 0 || neighborPos.Z >= ChunkSize)
                {
                    var chunkPos = curChunk.Position;
                    Vector3Int chunkOffset = Vector3Int.Zero;

                    if (neighborPos.X < 0) { chunkOffset.X = -ChunkSize; localPos.X = ChunkSize - 1; }
                    else if (neighborPos.X >= ChunkSize) { chunkOffset.X = ChunkSize; localPos.X = 0; }
                    if (neighborPos.Y < 0) { chunkOffset.Y = -ChunkSize; localPos.Y = ChunkSize - 1; }
                    else if (neighborPos.Y >= ChunkSize) { chunkOffset.Y = ChunkSize; localPos.Y = 0; }
                    if (neighborPos.Z < 0) { chunkOffset.Z = -ChunkSize; localPos.Z = ChunkSize - 1; }
                    else if (neighborPos.Z >= ChunkSize) { chunkOffset.Z = ChunkSize; localPos.Z = 0; }

                    var neighborChunkPos = chunkPos + chunkOffset;
                    if (!Chunks.TryGetValue(neighborChunkPos, out neighborChunk))
                        continue;
                }

                var neighborBlock = neighborChunk.Blocks[localPos.X, localPos.Y, localPos.Z];

                // Only propagate if neighbor is transparent or air
                if (neighborBlock.Info.Type == BlockType.Air || neighborBlock.Info.IsTransparent)
                {
                    byte newLight = (byte)(lightLevel - 1);
                    if (newLight > neighborBlock.BlockLight)
                    {
                        neighborBlock.BlockLight = newLight;
                        queue.Enqueue((neighborChunk, localPos));
                    }
                }
            }
        }
    }
}
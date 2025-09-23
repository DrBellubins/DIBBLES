using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using DIBBLES.Scenes;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class TerrainTick
{
    // Max concurrent tick threads
    private const int MaxThreads = 4;
    private SemaphoreSlim semaphore = new SemaphoreSlim(MaxThreads);

    // Queue to request mesh updates (to be processed on main thread)
    private readonly ConcurrentQueue<Vector3Int> meshUpdateQueue = new();

    // Extension point: block tick logic (delegate per block type)
    public delegate void BlockTickDelegate(Chunk chunk, int x, int y, int z, TerrainTick context);

    // Registered tick handlers for block types
    private readonly Dictionary<BlockType, BlockTickDelegate> blockTickHandlers = new();

    // Register a block tick handler
    public void RegisterBlockTicker(BlockType type, BlockTickDelegate ticker)
    {
        blockTickHandlers[type] = ticker;
    }

    // Main tick update
    public void Tick(Vector3Int playerChunkPos)
    {
        var chunkEntries = TerrainGeneration.ChunkBuffer
            .Select(kv => new
            {
                Position = kv.Key,
                Chunk = kv.Value,
                Distance = Vector3.DistanceSquared(playerChunkPos.ToVector3(), kv.Key.ToVector3())
            })
            .OrderBy(e => e.Distance)
            .ToList();

        // Process chunk updates in parallel
        foreach (var entry in chunkEntries)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                semaphore.Wait();

                try
                {
                    UpdateChunk(entry.Chunk, entry.Position);
                }
                catch (Exception ex)
                {
                    //Console.Write(ex);
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        // Main thread: process mesh update requests
        while (meshUpdateQueue.TryDequeue(out var chunkPos))
        {
            if (TerrainGeneration.ChunkBuffer.TryGetValue(chunkPos, out var chunk))
            {
                var meshData = TerrainGeneration.Mesh.GenerateMeshData(chunk, false);
                var tMeshData = TerrainGeneration.Mesh.GenerateMeshData(chunk, true, GameScene.PlayerCharacter.Camera.Position);

                // Unload and upload models (main thread only)
                if (TerrainGeneration.Mesh.OpaqueModels.TryGetValue(chunkPos, out var oldOpaque))
                    oldOpaque.Dispose();
                
                TerrainGeneration.Mesh.OpaqueModels[chunkPos] = TerrainGeneration.Mesh.UploadMesh(meshData);

                if (TerrainGeneration.Mesh.TransparentModels.TryGetValue(chunkPos, out var oldTrans))
                    oldTrans.Dispose();
                
                TerrainGeneration.Mesh.TransparentModels[chunkPos] = TerrainGeneration.Mesh.UploadMesh(tMeshData);
            }
        }
    }

    // Per-chunk update logic
    private void UpdateChunk(Chunk chunk, Vector3Int chunkPos)
    {
        bool needsMeshUpdate = false;

        for (int x = 0; x < TerrainGeneration.ChunkSize; x++)
        for (int y = 0; y < TerrainGeneration.ChunkSize; y++)
        for (int z = 0; z < TerrainGeneration.ChunkSize; z++)
        {
            var blockType = chunk.GetTypeAt(x, y, z);

            // Call registered updater if available
            if (blockTickHandlers.TryGetValue(blockType, out var updater))
            {
                updater(chunk, x, y, z, this);

                // Example: if updater changed block state
                // needsMeshUpdate = true; // Set in handler if needed
            }
        }

        // For now, just queue mesh update if any block was updated (customize this logic)
        if (needsMeshUpdate)
            meshUpdateQueue.Enqueue(chunkPos);
    }

    // Example: Handler can call this to request mesh update
    public void RequestMeshUpdate(Vector3Int chunkPos)
    {
        meshUpdateQueue.Enqueue(chunkPos);
    }
}
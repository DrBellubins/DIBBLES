using System.Collections.Concurrent;
using System.Threading;
using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainLighting
{
    private const int MaxThreads = 4; // Number of concurrent lighting threads

    private void placeLights(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
            chunk.SetLightLevelAt(x, y, z, chunk.GetInfoAt(x, y, z).LightEmission);
    }

    // Multi-threaded cross-chunk lighting
    public void GenerateAllLighting(Vector3Int centerChunk)
    {
        int halfRD = RenderDistance / 2;
        var chunkPositions = new List<Vector3Int>();

        for (int cx = centerChunk.X - halfRD; cx <= centerChunk.X + halfRD; cx++)
        for (int cy = centerChunk.Y - halfRD; cy <= centerChunk.Y + halfRD; cy++)
        for (int cz = centerChunk.Z - halfRD; cz <= centerChunk.Z + halfRD; cz++)
        {
            var chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
            if (ChunkBuffer.ContainsKey(chunkPos))
                chunkPositions.Add(chunkPos);
        }

        // Place all lights
        foreach (var chunkPos in chunkPositions)
        {
            var chunk = ChunkBuffer[chunkPos];
            placeLights(chunk);
        }

        // Global queue for BFS and visited positions for deduplication
        var queue = new ConcurrentQueue<(Vector3Int chunkPos, Vector3Int localPos)>();
        var visited = new ConcurrentDictionary<(Vector3Int chunkPos, Vector3Int localPos), byte>();

        // Enqueue all initial light sources
        foreach (var chunkPos in chunkPositions)
        {
            var chunk = ChunkBuffer[chunkPos];
            for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
            for (int z = 0; z < ChunkSize; z++)
            {
                byte blockLight = chunk.GetLightLevelAt(x, y, z);
                
                if (blockLight > 0)
                {
                    var localPos = new Vector3Int(x, y, z);
                    queue.Enqueue((chunkPos, localPos));
                    visited.TryAdd((chunkPos, localPos), blockLight);
                }
            }
        }

        var directions = new[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        int runningThreads = 0;
        ManualResetEvent doneEvent = new ManualResetEvent(false);

        void Worker()
        {
            Interlocked.Increment(ref runningThreads);

            while (true)
            {
                if (!queue.TryDequeue(out var item))
                    break;

                var (chunkPos, localPos) = item;

                if (!ChunkBuffer.TryGetValue(chunkPos, out var chunk))
                    continue;

                byte lightLevel = chunk.GetLightLevelAt(localPos.X, localPos.Y, localPos.Z);

                if (lightLevel <= 1) continue;

                foreach (var dir in directions)
                {
                    var neighborLocal = new Vector3Int(localPos.X + dir.X, localPos.Y + dir.Y, localPos.Z + dir.Z);
                    var neighborChunk = chunk;
                    var neighborChunkPos = chunkPos;

                    // Cross chunk boundary if needed
                    if (neighborLocal.X < 0 || neighborLocal.X >= ChunkSize ||
                        neighborLocal.Y < 0 || neighborLocal.Y >= ChunkSize ||
                        neighborLocal.Z < 0 || neighborLocal.Z >= ChunkSize)
                    {
                        // Convert to world position
                        Vector3Int worldPos = chunk.Position + neighborLocal;
                        int ncx = (int)Math.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
                        int ncy = (int)Math.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
                        int ncz = (int)Math.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
                        neighborChunkPos = new Vector3Int(ncx, ncy, ncz);

                        if (!ChunkBuffer.TryGetValue(neighborChunkPos, out neighborChunk))
                            continue;

                        neighborLocal = new Vector3Int(worldPos.X - ncx, worldPos.Y - ncy, worldPos.Z - ncz);
                    }

                    var neighborBlockType = neighborChunk.GetTypeAt(neighborLocal.X, neighborLocal.Y, neighborLocal.Z);
                    var neighborBlockInfo = neighborChunk.GetInfoAt(neighborLocal.X, neighborLocal.Y, neighborLocal.Z);
                    var neighborBlockLightLevel = neighborChunk.GetLightLevelAt(neighborLocal.X, neighborLocal.Y, neighborLocal.Z);

                    if (neighborBlockType == BlockType.Air ||
                        (neighborBlockType != BlockType.Leaves && neighborBlockInfo.IsTransparent))
                    {
                        byte newLight = (byte)(lightLevel - 1);
                        if (newLight > neighborBlockLightLevel)
                        {
                            neighborChunk.SetLightLevelAt(neighborLocal.X, neighborLocal.Y, neighborLocal.Z, newLight);
                            if (visited.TryAdd((neighborChunkPos, neighborLocal), newLight))
                                queue.Enqueue((neighborChunkPos, neighborLocal));
                        }
                    }
                }
            }

            if (Interlocked.Decrement(ref runningThreads) == 0)
                doneEvent.Set();
        }

        // Start threads
        for (int i = 0; i < MaxThreads; i++)
            ThreadPool.QueueUserWorkItem(_ => Worker());

        // Wait for all threads to finish
        doneEvent.WaitOne();
    }

    // Per-chunk BFS, unchanged
    private void floodFill(Chunk chunk)
    {
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();

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
                        continue;

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
    }
}
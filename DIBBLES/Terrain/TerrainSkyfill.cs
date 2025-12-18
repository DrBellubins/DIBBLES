using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainSkyfill
{
    private static readonly Vector3Int[] Directions =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    public void Generate(Chunk chunk)
    {
        PropagateSky(chunk);
    }

    public void PropagateSky(Chunk startChunk)
    {
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();

        Chunk lastChunk = null;
        NeighborCache neighborCache = default;

        // Seed from border cells
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (!(x == 0 || x == ChunkSize - 1 || y == 0 || y == ChunkSize - 1 || z == 0 || z == ChunkSize - 1))
                        continue;

                    var type = startChunk.GetTypeAt(x, y, z);
                    var info = startChunk.GetInfoAt(x, y, z);

                    if (type != BlockType.Air && !info.IsTransparent)
                        continue;

                    var pos = new Vector3Int(x, y, z);

                    if (isBorderExposedToSky(startChunk, pos))
                    {
                        if (startChunk.GetSkyLevelAt(x, y, z) < 15)
                        {
                            startChunk.SetSkyLevelAt(x, y, z, 15);
                            queue.Enqueue((startChunk, pos));
                        }
                    }
                }
            }
        }

        // BFS flood (no attenuation)
        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();

            if (!ReferenceEquals(curChunk, lastChunk))
            {
                neighborCache = NeighborCache.Build(curChunk);
                lastChunk = curChunk;
            }

            foreach (var dir in Directions)
            {
                Vector3Int nPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);
                Chunk targetChunk = curChunk;

                // Cross-chunk wrapping (single-axis like lighting)
                if (nPos.X < 0)
                {
                    if (neighborCache.NegX == null)
                        continue;

                    targetChunk = neighborCache.NegX;
                    nPos.X += ChunkSize;
                }
                else if (nPos.X >= ChunkSize)
                {
                    if (neighborCache.PosX == null)
                        continue;

                    targetChunk = neighborCache.PosX;
                    nPos.X -= ChunkSize;
                }

                if (nPos.Y < 0)
                {
                    if (neighborCache.NegY == null)
                        continue;

                    targetChunk = neighborCache.NegY;
                    nPos.Y += ChunkSize;
                }
                else if (nPos.Y >= ChunkSize)
                {
                    if (neighborCache.PosY == null)
                        continue;

                    targetChunk = neighborCache.PosY;
                    nPos.Y -= ChunkSize;
                }

                if (nPos.Z < 0)
                {
                    if (neighborCache.NegZ == null)
                        continue;

                    targetChunk = neighborCache.NegZ;
                    nPos.Z += ChunkSize;
                }
                else if (nPos.Z >= ChunkSize)
                {
                    if (neighborCache.PosZ == null)
                        continue;

                    targetChunk = neighborCache.PosZ;
                    nPos.Z -= ChunkSize;
                }

                var nType = targetChunk.GetTypeAt(nPos.X, nPos.Y, nPos.Z);
                var nInfo = targetChunk.GetInfoAt(nPos.X, nPos.Y, nPos.Z);

                if (nType == BlockType.Air || nInfo.IsTransparent)
                {
                    if (targetChunk.GetSkyLevelAt(nPos.X, nPos.Y, nPos.Z) < 15)
                    {
                        targetChunk.SetSkyLevelAt(nPos.X, nPos.Y, nPos.Z, 15);
                        queue.Enqueue((targetChunk, nPos));
                    }
                }
            }
        }
    }

    private bool isBorderExposedToSky(Chunk chunk, Vector3Int pos)
    {
        // A border cell is exposed if stepping out-of-bounds in any direction:
        // - neighbor chunk is unloaded (null)
        // - or neighbor loaded and neighbor cell already has SkyLevel > 0
        var cache = NeighborCache.Build(chunk);

        foreach (var dir in Directions)
        {
            Vector3Int nPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);

            // Only consider exposures where we would go out of bounds
            if (chunk.IsInBounds(nPos))
                continue;

            Chunk nChunk = chunk;
            Vector3Int local = nPos;

            // Single-axis wrap like lighting
            if (local.X < 0)
            {
                nChunk = cache.NegX;
                local.X += ChunkSize;
            }
            else if (local.X >= ChunkSize)
            {
                nChunk = cache.PosX;
                local.X -= ChunkSize;
            }

            if (local.Y < 0)
            {
                nChunk = nChunk == chunk ? cache.NegY : nChunk == null ? null : nChunk;
                local.Y += ChunkSize;
            }
            else if (local.Y >= ChunkSize)
            {
                nChunk = nChunk == chunk ? cache.PosY : nChunk == null ? null : nChunk;
                local.Y -= ChunkSize;
            }

            if (local.Z < 0)
            {
                nChunk = nChunk == chunk ? cache.NegZ : nChunk == null ? null : nChunk;
                local.Z += ChunkSize;
            }
            else if (local.Z >= ChunkSize)
            {
                nChunk = nChunk == chunk ? cache.PosZ : nChunk == null ? null : nChunk;
                local.Z -= ChunkSize;
            }

            // Unloaded neighbor = open sky
            if (nChunk == null)
                return true;

            // Loaded neighbor with existing sky
            if (nChunk.GetSkyLevelAt(local.X, local.Y, local.Z) > 0)
                return true;
        }

        return false;
    }
}
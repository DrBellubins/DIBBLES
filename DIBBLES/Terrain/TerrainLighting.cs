using System.Collections.Concurrent;
using System.Threading;
using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainLighting
{
    public void Generate(Chunk chunk)
    {
        placeLights(chunk);
        PropagateLight(chunk);
    }

    private void placeLights(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            byte currentLightLevel = 0;
            
            if (chunk.GetTypeAt(x, y, z) == BlockType.Air
                && !chunk.GetCaveAt(x, y, z) && isOpenToSky(chunk, x, y, z))
            {
                currentLightLevel = 15;
                chunk.SetLightLevelAt(x, y, z, currentLightLevel);
            }
            
            /*var emission = chunk.GetInfoAt(x, y, z).LightEmission;
            
            if (emission > 0)
            {
                // Preserve any previously propagated light: take the max of existing and emission
                var newLevel = (byte)Math.Max(currentLightLevel, emission);
                chunk.SetLightLevelAt(x, y, z, newLevel);
            }*/

            // IMPORTANT: do NOT write 0 to non-emissive cells here; that would erase cross-chunk propagation
        }
    }

    public void PropagateLight(Chunk chunk)
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

        // Track the last-chunk to avoid rebuilding caches every cell
        Chunk? lastChunk = null;
        NeighborCache? neighborCache = null;

        Vector3Int[] directions =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();

            // Rebuild neighbor cache if chunk changed
            if (!ReferenceEquals(curChunk, lastChunk))
            {
                neighborCache = NeighborCache.Build(curChunk);
                lastChunk = curChunk;
            }

            var lightLevel = curChunk.GetLightLevelAt(pos.X, pos.Y, pos.Z);

            if (lightLevel <= 1)
                continue;

            foreach (var dir in directions)
            {
                // Compute local neighbor
                Vector3Int localPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);

                Chunk targetChunk = curChunk;

                // Fast wrap without world math or dictionary lookups:
                // X axis
                if (localPos.X < 0)
                {
                    if (neighborCache == null || neighborCache.NegX == null)
                        continue;

                    targetChunk = neighborCache.NegX;
                    localPos.X += ChunkSize;
                }
                else if (localPos.X >= ChunkSize)
                {
                    if (neighborCache == null || neighborCache.PosX == null)
                        continue;

                    targetChunk = neighborCache.PosX;
                    localPos.X -= ChunkSize;
                }

                // Y axis
                if (localPos.Y < 0)
                {
                    if (neighborCache == null || neighborCache.NegY == null)
                        continue;

                    targetChunk = neighborCache.NegY;
                    localPos.Y += ChunkSize;
                }
                else if (localPos.Y >= ChunkSize)
                {
                    if (neighborCache == null || neighborCache.PosY == null)
                        continue;

                    targetChunk = neighborCache.PosY;
                    localPos.Y -= ChunkSize;
                }

                // Z axis
                if (localPos.Z < 0)
                {
                    if (neighborCache == null || neighborCache.NegZ == null)
                        continue;

                    targetChunk = neighborCache.NegZ;
                    localPos.Z += ChunkSize;
                }
                else if (localPos.Z >= ChunkSize)
                {
                    if (neighborCache == null || neighborCache.PosZ == null)
                        continue;

                    targetChunk = neighborCache.PosZ;
                    localPos.Z -= ChunkSize;
                }

                // Inner-chunk path stays on curChunk; cross-chunk path uses targetChunk
                var neighborBlockType = targetChunk.GetTypeAt(localPos.X, localPos.Y, localPos.Z);
                var neighborBlockInfo = targetChunk.GetInfoAt(localPos.X, localPos.Y, localPos.Z);
                var neighborBlockLightLevel = targetChunk.GetLightLevelAt(localPos.X, localPos.Y, localPos.Z);

                // Transparent or air lets light propagate (except leaves special-case)
                if (neighborBlockType == BlockType.Air ||
                    (neighborBlockType != BlockType.Leaves && neighborBlockInfo.IsTransparent))
                {
                    byte newLight = (byte)(lightLevel - 1);

                    if (newLight > neighborBlockLightLevel)
                    {
                        targetChunk.SetLightLevelAt(localPos.X, localPos.Y, localPos.Z, newLight);
                        queue.Enqueue((targetChunk, localPos));
                    }
                }
            }
        }
    }
    
    private bool isOpenToSky(Chunk chunk, int x, int y, int z)
    {
        int worldY = chunk.Position.Y + y;

        for (int cy = worldY + 1; ; cy++)
        {
            // Compute chunk coords for cy
            int chunkY = (int)Math.Floor((float)cy / ChunkSize) * ChunkSize;
            int localY = cy - chunkY;
            var chunkCoord = new Vector3Int(chunk.Position.X, chunkY, chunk.Position.Z);

            // If there's no chunk above, treat as "open to sky"
            if (!ChunkBuffer.TryGetValue(chunkCoord, out var aboveChunk))
                break;

            // Move to next chunk in +Y
            if (localY >= ChunkSize || localY < 0)
                continue;

            var blockType = aboveChunk.GetTypeAt(x, localY, z);
            var isCave = aboveChunk.GetCaveAt(x, localY, z);

            if (blockType != BlockType.Air && !isCave)
                return false;

            // Optionally, set a cutoff at some maximum world height
            // Or make this configurable
        }

        return true;
    }
}
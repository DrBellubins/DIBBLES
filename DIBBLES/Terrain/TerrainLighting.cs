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
            var emission = chunk.GetInfoAt(x, y, z).LightEmission;

            if (emission > 0)
            {
                // Preserve any previously propagated light: take the max of existing and emission
                var current = chunk.GetLightLevelAt(x, y, z);
                var newLevel = (byte)Math.Max(current, emission);

                chunk.SetLightLevelAt(x, y, z, newLevel);
            }
            
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
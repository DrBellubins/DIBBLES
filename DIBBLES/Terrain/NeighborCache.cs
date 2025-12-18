using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class NeighborCache
{
    public Chunk PosX;
    public Chunk NegX;
    public Chunk PosY;
    public Chunk NegY;
    public Chunk PosZ;
    public Chunk NegZ;
    
    public static NeighborCache Build(Chunk chunk)
    {
        var pos = chunk.Position;

        ChunkBuffer.TryGetValue(pos + new Vector3Int(ChunkSize, 0, 0), out var px);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(-ChunkSize, 0, 0), out var nx);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, ChunkSize, 0), out var py);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, -ChunkSize, 0), out var ny);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, 0, ChunkSize), out var pz);
        ChunkBuffer.TryGetValue(pos + new Vector3Int(0, 0, -ChunkSize), out var nz);

        return new NeighborCache
        {
            PosX = px,
            NegX = nx,
            PosY = py,
            NegY = ny,
            PosZ = pz,
            NegZ = nz
        };
    }
    
    // Fast space check using base-chunk neighbor cache and diagonal fallback
    public static bool CheckSpaceFast(Vector3Int startPos, Vector3Int size, Chunk baseChunk, NeighborCache cache)
    {
        Vector3Int basePos = baseChunk.Position;
    
        for (int dx = 0; dx < size.X; dx++)
        {
            for (int dy = 0; dy < size.Y; dy++)
            {
                for (int dz = 0; dz < size.Z; dz++)
                {
                    Vector3Int worldPos = new Vector3Int(
                        startPos.X + dx,
                        startPos.Y + dy,
                        startPos.Z + dz
                    );
    
                    if (GetBlockTypeFast(worldPos, baseChunk, cache) != BlockType.Air)
                        return false;
                }
            }
        }
    
        return true;
    }

    // Helper: fast global read using local wrapping against baseChunk + neighbor cache,
    // with diagonal fallback to direct dictionary lookup when crossing multiple axes.
    public static BlockType GetBlockTypeFast(Vector3Int worldPos, Chunk baseChunk, NeighborCache cache)
    {
        Vector3Int basePos = baseChunk.Position;
    
        int lx = worldPos.X - basePos.X;
        int ly = worldPos.Y - basePos.Y;
        int lz = worldPos.Z - basePos.Z;
    
        Chunk target = baseChunk;
    
        int crosses =
            (lx < 0 || lx >= ChunkSize ? 1 : 0) +
            (ly < 0 || ly >= ChunkSize ? 1 : 0) +
            (lz < 0 || lz >= ChunkSize ? 1 : 0);
    
        if (crosses == 0)
            return target.GetTypeAt(lx, ly, lz);
    
        // Single-axis wrap via cache
        if (crosses == 1)
        {
            if (lx < 0)
            {
                if (cache.NegX == null) return BlockType.Air;
                target = cache.NegX; lx += ChunkSize;
            }
            else if (lx >= ChunkSize)
            {
                if (cache.PosX == null) return BlockType.Air;
                target = cache.PosX; lx -= ChunkSize;
            }
    
            if (ly < 0)
            {
                if (cache.NegY == null) return BlockType.Air;
                target = cache.NegY; ly += ChunkSize;
            }
            else if (ly >= ChunkSize)
            {
                if (cache.PosY == null) return BlockType.Air;
                target = cache.PosY; ly -= ChunkSize;
            }
    
            if (lz < 0)
            {
                if (cache.NegZ == null) return BlockType.Air;
                target = cache.NegZ; lz += ChunkSize;
            }
            else if (lz >= ChunkSize)
            {
                if (cache.PosZ == null) return BlockType.Air;
                target = cache.PosZ; lz -= ChunkSize;
            }
    
            return target.GetTypeAt(lx, ly, lz);
        }
    
        // Diagonal/corner fallback: compute target chunk coord directly
        int offX = (lx < 0 ? -ChunkSize : (lx >= ChunkSize ? ChunkSize : 0));
        int offY = (ly < 0 ? -ChunkSize : (ly >= ChunkSize ? ChunkSize : 0));
        int offZ = (lz < 0 ? -ChunkSize : (lz >= ChunkSize ? ChunkSize : 0));
    
        Vector3Int targetChunkPos = basePos + new Vector3Int(offX, offY, offZ);
    
        if (!ChunkBuffer.TryGetValue(targetChunkPos, out target))
            return BlockType.Air;
    
        lx = (lx % ChunkSize + ChunkSize) % ChunkSize;
        ly = (ly % ChunkSize + ChunkSize) % ChunkSize;
        lz = (lz % ChunkSize + ChunkSize) % ChunkSize;
    
        return target.GetTypeAt(lx, ly, lz);
    }

    // Helper: fast global write using same wrapping/fallback strategy
    public static void SetBlockTypeFast(Vector3Int worldPos, BlockType type, Chunk baseChunk, NeighborCache cache)
    {
        Vector3Int basePos = baseChunk.Position;

        int lx = worldPos.X - basePos.X;
        int ly = worldPos.Y - basePos.Y;
        int lz = worldPos.Z - basePos.Z;

        Chunk target = baseChunk;

        int crosses =
            (lx < 0 || lx >= ChunkSize ? 1 : 0) +
            (ly < 0 || ly >= ChunkSize ? 1 : 0) +
            (lz < 0 || lz >= ChunkSize ? 1 : 0);

        if (crosses == 0)
        {
            target.SetTypeAt(lx, ly, lz, type);
            target.IsModified = true;
            return;
        }

        // Single-axis wrap via cache
        if (crosses == 1)
        {
            if (lx < 0)
            {
                if (cache.NegX == null) return;
                target = cache.NegX;
                lx += ChunkSize;
            }
            else if (lx >= ChunkSize)
            {
                if (cache.PosX == null) return;
                target = cache.PosX;
                lx -= ChunkSize;
            }

            if (ly < 0)
            {
                if (cache.NegY == null) return;
                target = cache.NegY;
                ly += ChunkSize;
            }
            else if (ly >= ChunkSize)
            {
                if (cache.PosY == null) return;
                target = cache.PosY;
                ly -= ChunkSize;
            }

            if (lz < 0)
            {
                if (cache.NegZ == null) return;
                target = cache.NegZ;
                lz += ChunkSize;
            }
            else if (lz >= ChunkSize)
            {
                if (cache.PosZ == null) return;
                target = cache.PosZ;
                lz -= ChunkSize;
            }

            target.SetTypeAt(lx, ly, lz, type);
            target.IsModified = true;
            return;
        }

        // Diagonal/corner fallback
        int offX = (lx < 0 ? -ChunkSize : (lx >= ChunkSize ? ChunkSize : 0));
        int offY = (ly < 0 ? -ChunkSize : (ly >= ChunkSize ? ChunkSize : 0));
        int offZ = (lz < 0 ? -ChunkSize : (lz >= ChunkSize ? ChunkSize : 0));

        Vector3Int targetChunkPos = basePos + new Vector3Int(offX, offY, offZ);

        if (!ChunkBuffer.TryGetValue(targetChunkPos, out target))
            return;

        lx = (lx % ChunkSize + ChunkSize) % ChunkSize;
        ly = (ly % ChunkSize + ChunkSize) % ChunkSize;
        lz = (lz % ChunkSize + ChunkSize) % ChunkSize;

        target.SetTypeAt(lx, ly, lz, type);
        target.IsModified = true;
    }
}
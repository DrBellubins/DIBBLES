using Microsoft.Xna.Framework;
using DIBBLES.Utils;
using DIBBLES.Terrain;

namespace DIBBLES.Terrain;

public class CaveGeneration
{
    private readonly int seed;

    public CaveGeneration(int seed)
    {
        this.seed = seed;
    }

    public void CarveCavesCrossChunk(Vector3Int chunkOrigin, ChunkManager chunkManager, TerrainGeneration terrainGeneration)
    {
        var noise = new FastNoiseLite();
        noise.SetSeed(seed);
        noise.SetFrequency(0.05f);
    
        int wormsPerChunk = 1;
    
        for (int w = 0; w < wormsPerChunk; w++)
        {
            var rng = new SeededRandom(seed + chunkOrigin.X + chunkOrigin.Y + chunkOrigin.Z + w);
            float px = rng.NextFloat(0, TerrainGeneration.ChunkSize);
            float py = rng.NextFloat(TerrainGeneration.ChunkSize * 0.3f, TerrainGeneration.ChunkSize * 0.8f);
            float pz = rng.NextFloat(0, TerrainGeneration.ChunkSize);
    
            Vector3 pos = chunkOrigin.ToVector3() + new Vector3(px, py, pz);
    
            int length = rng.NextInt(80, 150);
            float radius = rng.NextFloat(1.5f, 2.5f);
    
            for (int step = 0; step < length; step++)
            {
                CarveAirSphereCrossChunk(pos, radius, chunkManager, terrainGeneration);
    
                float angleYaw = noise.GetNoise(pos.X, pos.Y, pos.Z) * MathF.PI * 2f;
                float anglePitch = noise.GetNoise(pos.Z, pos.X, pos.Y) * MathF.PI * 0.5f;
    
                Vector3 dir = new Vector3(
                    MathF.Cos(angleYaw) * MathF.Cos(anglePitch),
                    MathF.Sin(anglePitch),
                    MathF.Sin(angleYaw) * MathF.Cos(anglePitch)
                );
    
                pos += dir * radius * 0.8f;
            }
        }
    }
    
    // Carve cave sphere potentially across chunk boundaries
    private void CarveAirSphereCrossChunk(Vector3 worldPos, float radius, ChunkManager chunkManager, TerrainGeneration terrainGeneration)
    {
        int minX = (int)MathF.Floor(worldPos.X - radius);
        int maxX = (int)MathF.Ceiling(worldPos.X + radius);
        int minY = (int)MathF.Floor(worldPos.Y - radius);
        int maxY = (int)MathF.Ceiling(worldPos.Y + radius);
        int minZ = (int)MathF.Floor(worldPos.Z - radius);
        int maxZ = (int)MathF.Ceiling(worldPos.Z + radius);
    
        float r2 = radius * radius;
    
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            var dist2 = (worldPos.X - x) * (worldPos.X - x) + (worldPos.Y - y) * (worldPos.Y - y) + (worldPos.Z - z) * (worldPos.Z - z);
            if (dist2 > r2) continue;
    
            Vector3Int blockWorldPos = new Vector3Int(x, y, z);
            var chunkPos = new Vector3Int(
                (int)Math.Floor((float)x / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize,
                (int)Math.Floor((float)y / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize,
                (int)Math.Floor((float)z / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize
            );
    
            var chunk = chunkManager.GetOrCreateChunk(chunkPos, terrainGeneration); // Must handle async/generation if not present
    
            int localX = x - chunkPos.X;
            int localY = y - chunkPos.Y;
            int localZ = z - chunkPos.Z;
    
            if (localX < 0 || localX >= TerrainGeneration.ChunkSize ||
                localY < 0 || localY >= TerrainGeneration.ChunkSize ||
                localZ < 0 || localZ >= TerrainGeneration.ChunkSize)
                continue;
    
            chunk.SetBlock(localX, localY, localZ, new Block(blockWorldPos, BlockType.Air));
        }
    }
}
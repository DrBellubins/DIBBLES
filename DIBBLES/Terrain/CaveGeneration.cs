using System.Numerics;
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

    public void CarveCaves(Chunk chunk)
    {
        var noise = new FastNoiseLite();
        noise.SetSeed(seed);
        noise.SetFrequency(0.05f);

        int wormsPerChunk = 1; // Adjust for density

        for (int w = 0; w < wormsPerChunk; w++)
        {
            // Start worm at a random position in chunk
            var rng = new SeededRandom(seed + chunk.Position.X + chunk.Position.Y + chunk.Position.Z + w);
            float px = rng.NextFloat(0, TerrainGeneration.ChunkSize);
            float py = rng.NextFloat(TerrainGeneration.ChunkSize * 0.3f, TerrainGeneration.ChunkSize * 0.8f); // Start underground
            float pz = rng.NextFloat(0, TerrainGeneration.ChunkSize);

            Vector3 pos = new Vector3(px, py, pz);

            int length = rng.NextInt(80, 150); // How long the worm is
            float radius = rng.NextFloat(1.5f, 2.5f);

            for (int step = 0; step < length; step++)
            {
                // Carve a sphere of air at the worm's position
                CarveAirSphere(chunk, pos, radius);

                // Use noise to steer the worm
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

    private void CarveAirSphere(Chunk chunk, Vector3 pos, float radius)
    {
        int minX = (int)MathF.Floor(pos.X - radius);
        int maxX = (int)MathF.Ceiling(pos.X + radius);
        int minY = (int)MathF.Floor(pos.Y - radius);
        int maxY = (int)MathF.Ceiling(pos.Y + radius);
        int minZ = (int)MathF.Floor(pos.Z - radius);
        int maxZ = (int)MathF.Ceiling(pos.Z + radius);

        float r2 = radius * radius;

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            if (x < 0 || x >= TerrainGeneration.ChunkSize || y < 0
                || y >= TerrainGeneration.ChunkSize || z < 0 || z >= TerrainGeneration.ChunkSize) continue;

            var dist2 = (pos.X - x) * (pos.X - x) + (pos.Y - y) * (pos.Y - y) + (pos.Z - z) * (pos.Z - z);
            if (dist2 > r2) continue;

            chunk.SetBlock(x, y, z, new Block(new Vector3Int(
                chunk.Position.X + x,
                chunk.Position.Y + y,
                chunk.Position.Z + z
            ), BlockType.Air));
        }
    }
}
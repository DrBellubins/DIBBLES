namespace DIBBLES.Terrain.Features;

using static TerrainGeneration;

public class TerrainIsland
{
    private const float IslandThreshold = 0.6f;
    private const float CaveThresholdBase = 0.7f;

    // How close to the island surface we consider "shell" (noise distance band)
    private const float SurfaceMargin = 0.06f;

    // Directional exposure penalties (top strongest, sides medium, bottom light)
    private const float CavePenaltyTop = 0.25f;
    private const float CavePenaltySide = 0.12f;
    private const float CavePenaltyBottom = 0.08f;

    // Additional penalty when close to the island boundary (shell)
    // 0 when deep interior, up to this value when right at the surface threshold
    private const float BoundaryPenaltyScale = 0.25f;
    
    public void Generate(Chunk chunk)
    {
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        // Cave noise: low frequency 3D OpenSimplex, independent seed
        var caveNoise = new FastNoiseLite();
        caveNoise.SetSeed(Seed + 424242);
        caveNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        caveNoise.SetFrequency(0.05f);
        caveNoise.SetFractalType(FastNoiseLite.FractalType.None);
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    int worldX = chunk.Position.X + x;
                    int worldY = chunk.Position.Y + y;
                    int worldZ = chunk.Position.Z + z;
                    
                    // Island noise
                    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                    noise.SetFrequency(0.01f);
                    noise.SetFractalType(FastNoiseLite.FractalType.FBm);
                    noise.SetFractalOctaves(4);
                    noise.SetFractalLacunarity(2.0f);
                    noise.SetFractalGain(0.5f);
                    
                    float islandNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                    float cave = caveNoise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                    
                    // How deep into the island we are, 0 = right at boundary, 1 = deep interior
                    float interiorDepth01 = Math.Clamp((islandNoise - IslandThreshold) / SurfaceMargin, 0f, 1f);
                    
                    // Near-boundary cave suppression
                    float boundaryPenalty = (1f - interiorDepth01) * BoundaryPenaltyScale;

                    // Directional exposure cave suppression
                    float exposurePenalty = computeExposurePenalty(chunk, x, y, z);

                    // Final dynamic threshold
                    float caveThresholdDyn = CaveThresholdBase + boundaryPenalty + exposurePenalty;
                    caveThresholdDyn = Math.Clamp(caveThresholdDyn, 0f, 1f);
                    
                    if (islandNoise > IslandThreshold)
                    {
                        // Start as stone
                        chunk.SetTypeAt(x, y, z, BlockType.Stone);
                    
                        if (cave > caveThresholdDyn)
                        {
                            chunk.SetTypeAt(x, y, z, BlockType.Air);
                            chunk.SetCaveAt(x, y, z, true);
                        }
                    }
                    else
                        chunk.SetTypeAt(x, y, z, BlockType.Air);
                }
            }
        }
    }
    
    // Safe neighbor fetch that treats out-of-chunk as air (discourages openings on borders)
    private static BlockType safeGetTypeAt(Chunk chunk, int x, int y, int z)
    {
        if (chunk.IsInBounds(x, y, z))
            return chunk.GetTypeAt(x, y, z);

        return BlockType.Air;
    }

    // Compute exposure penalty from neighboring air directions
    private static float computeExposurePenalty(Chunk chunk, int x, int y, int z)
    {
        float penalty = 0f;

        // Top and bottom
        var up = safeGetTypeAt(chunk, x, y + 1, z);
        var down = safeGetTypeAt(chunk, x, y - 1, z);

        if (up == BlockType.Air)
            penalty += CavePenaltyTop;

        if (down == BlockType.Air)
            penalty += CavePenaltyBottom;

        // Sides
        int sideAir = 0;

        if (safeGetTypeAt(chunk, x - 1, y, z) == BlockType.Air) sideAir++;
        if (safeGetTypeAt(chunk, x + 1, y, z) == BlockType.Air) sideAir++;
        if (safeGetTypeAt(chunk, x, y, z - 1) == BlockType.Air) sideAir++;
        if (safeGetTypeAt(chunk, x, y, z + 1) == BlockType.Air) sideAir++;

        penalty += sideAir * CavePenaltySide;

        return penalty;
    }
}
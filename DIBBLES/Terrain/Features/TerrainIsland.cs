namespace DIBBLES.Terrain.Features;

using static TerrainGeneration;

public class TerrainIsland
{
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
        
        const float islandThreshold = 0.6f;
        const float caveThreshold = 0.7f;
        
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
                    
                    if (islandNoise > islandThreshold)
                    {
                        // Start as stone
                        chunk.SetTypeAt(x, y, z, BlockType.Stone);
                        
                        float cave = caveNoise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                    
                        if (cave > caveThreshold)
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
}
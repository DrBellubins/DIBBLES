namespace DIBBLES.Terrain.Features;

using static TerrainGeneration;

public class TerrainIsland
{
    public void Generate(Chunk chunk)
    {
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;
                    
                    // Island noise
                    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                    noise.SetFrequency(0.01f);
                    noise.SetFractalType(FastNoiseLite.FractalType.FBm);
                    noise.SetFractalOctaves(4);
                    noise.SetFractalLacunarity(2.0f);
                    noise.SetFractalGain(0.5f);
                    
                    var islandNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                    
                    // Loop downward
                    if (islandNoise > 0.6f) // Islands
                        chunk.SetTypeAt(x, y, z, BlockType.Stone);
                    else // Not islands
                        chunk.SetTypeAt(x, y, z, BlockType.Air);
                }
            }
        }
    }
}
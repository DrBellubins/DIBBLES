using DIBBLES.Terrain.Biomes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Features;

public class TerrainSurface
{
    public const float BiomeSize = 256f;
    
    private FastNoiseLite temperatureNoise = new(Seed);
    private FastNoiseLite moistureNoise = new(Seed + 1337);
    
    public TerrainSurface()
    {
        // Average biome size: BiomeCellSize controls spacing; frequency is inverse
        float freq = 1f / BiomeSize;
        
        temperatureNoise.SetSeed(Seed + 11);
        temperatureNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        temperatureNoise.SetFrequency(freq);

        moistureNoise.SetSeed(Seed + 23);
        moistureNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        moistureNoise.SetFrequency(freq);
    }
    
    public void Generate(Chunk chunk)
    {
        long chunkSeed = Seed
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
    
        var rng = new SeededRandom(chunkSeed);
    
        FastNoiseLite biomeWarpNoise = new(Seed);
        biomeWarpNoise.SetSeed(Seed);
        biomeWarpNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var bRet = new BlockReturnData();
                bRet.RNG = rng;
                bRet.Noise = biomeWarpNoise;
    
                int worldX = chunk.Position.X + x;
                int worldZ = chunk.Position.Z + z;
    
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    int worldY = chunk.Position.Y + y;
    
                    bRet.LocalPos = new Vector3Int(x, y, z);
    
                    var current = Chunk.GetBlockTypeGlobal(new Vector3Int(worldX, worldY, worldZ));
                    var currentBiome = SampleClimateBiome(worldX, worldY, worldZ);
                    
                    chunk.SetBiomeAt(x, y, z, currentBiome); // Always set biome!
                    
                    if (current.Item1 != BlockType.Stone)
                        continue;
    
                    switch (currentBiome)
                    {
                        case TerrainBiome.Plains:
                        {
                            plainsBiome.Generate(chunk, ref bRet);
                            break;
                        }
                        case TerrainBiome.Desert:
                        {
                            desertBiome.Generate(chunk, ref bRet);
                            break;
                        }
                        case TerrainBiome.Snowlands:
                        {
                            snowlandsBiome.Generate(chunk, ref bRet);
                            break;
                        }
                    }
    
                    if (bRet.FoundSurface && bRet.IslandDepth >= 3)
                        break;
                }
            }
        }
    }
    
    // Climate-sampled biome LUT with blue-noise-style jitter
    private TerrainBiome SampleClimateBiome(int worldX, int worldY, int worldZ)
    {
        float temp = temperatureNoise.GetNoise(worldX, worldY, worldZ);
        float moist = moistureNoise.GetNoise(worldX + 9173, worldY, worldZ - 5521);

        // 3D jitter so we don't get obvious stratified "bands"
        float jitter = ((GMath.Hash3i(worldX, worldY, worldZ, Seed) & 1023) / 1023f) * 0.06f - 0.03f;
        temp += jitter * 0.1f;
        moist += jitter;

        return LookupBiomeLUT(temp, moist);
    }

    private TerrainBiome LookupBiomeLUT(float temp, float moist)
    {
        if (temp <= -0.20f)
            return TerrainBiome.Snowlands;

        if ((temp >= 0.35f && moist <= 0.10f) ||
            (moist <= -0.40f && temp >= 0.10f))
            return TerrainBiome.Desert;

        return TerrainBiome.Plains;
    }
}
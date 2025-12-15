using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Features;

public class TerrainSurface
{
    public void Generate(Chunk chunk)
    {
        long chunkSeed = Seed 
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
        
        var rng = new SeededRandom(chunkSeed);
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        var plainsBiome = new PlainsBiome();
        var desertBiome = new DesertBiome();
        var snowlandsBiome = new SnowlandsBiome();
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var blockReturnData = new BlockReturnData();
                blockReturnData.RNG = rng;
                blockReturnData.Noise = noise;
                
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;

                    blockReturnData.LocalPos = new Vector3Int(x, y, z);

                    var currentType = Chunk.GetBlockTypeGlobal(new Vector3Int(worldX, worldY, worldZ));
                    
                    if (currentType.Item1 != BlockType.Stone)
                        continue;
                    
                    // TODO: Biomes other than Plains are really rare
                    /*noise.SetFrequency(0.001f);
                    var biomeNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;

                    if (GMath.InRangeNotEqual(biomeNoise, 0f, 0.25f)) // Desert
                        desertBiome.Generate(chunk, ref blockReturnData);
                    else if (GMath.InRangeNotEqual(biomeNoise, 0.25f, 0.5f)) // Plains
                        plainsBiome.Generate(chunk, ref blockReturnData);
                    else if (GMath.InRangeNotEqual(biomeNoise, 0.5f, 0.75f)) // Snowlands
                        plainsBiome.Generate(chunk, ref blockReturnData);
                    else // Fallback
                        snowlandsBiome.Generate(chunk, ref blockReturnData);*/
                    
                    plainsBiome.Generate(chunk, ref blockReturnData);
                }
            }
        }
    }
}
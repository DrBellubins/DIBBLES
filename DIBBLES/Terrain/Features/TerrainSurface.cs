using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Features;

public class TerrainSurface
{
    // Average biome region size in blocks (XZ). Tune between 256 and 1024.
    public const int BiomeCellSize = 128;
    
    public void Generate(Chunk chunk)
    {
        long chunkSeed = Seed 
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
        
        var rng = new SeededRandom(chunkSeed);
        
        var plainsBiome = new PlainsBiome();
        var desertBiome = new DesertBiome();
        var snowlandsBiome = new SnowlandsBiome();
        
        var biomeNoise = new FastNoiseLite(Seed);
        biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var blockReturnData = new BlockReturnData();
                blockReturnData.RNG = rng;
                blockReturnData.Noise = biomeNoise;
                
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;
    
                    blockReturnData.LocalPos = new Vector3Int(x, y, z);
    
                    var currentType = Chunk.GetBlockTypeGlobal(new Vector3Int(worldX, worldY, worldZ));
                    if (currentType.Item1 != BlockType.Stone)
                        continue;
    
                    // 3D biome selection at the block’s world XYZ
                    var selectedBiome = ComputeBiomeAt(new Vector3Int(worldX, worldY, worldZ), biomeNoise);
    
                    switch (selectedBiome)
                    {
                        case TerrainBiome.Plains:
                        {
                            plainsBiome.Generate(chunk, ref blockReturnData);
                            break;
                        }
                        case TerrainBiome.Desert:
                        {
                            desertBiome.Generate(chunk, ref blockReturnData);
                            break;
                        }
                        case TerrainBiome.Snowlands:
                        {
                            snowlandsBiome.Generate(chunk, ref blockReturnData);
                            break;
                        }
                    }
                }
            }
        }
    }
    
    public TerrainBiome[] BiomeCycle = new TerrainBiome[]
    {
        TerrainBiome.Plains,
        TerrainBiome.Desert,
        TerrainBiome.Snowlands
    };

    // 3D Voronoi with domain warp to curve borders
    public TerrainBiome ComputeBiomeAt(Vector3Int worldPos, FastNoiseLite biomeNoise)
    {
        // Domain warp parameters
        float warpAmp = BiomeCellSize * 0.35f;            // 0.25–0.50 looks good
        float warpFreq = 1f / (BiomeCellSize * 12f);      // very low-frequency warp
    
        // Use shared noise; decorrelate vector components with fixed offsets
        float wx = biomeNoise.GetNoise(worldPos.X * warpFreq,
                                       worldPos.Y * warpFreq,
                                       worldPos.Z * warpFreq) * warpAmp;
    
        float wy = biomeNoise.GetNoise((worldPos.X + 101) * warpFreq,
                                       (worldPos.Y - 311) * warpFreq,
                                       (worldPos.Z + 29) * warpFreq) * warpAmp;
    
        float wz = biomeNoise.GetNoise((worldPos.X - 73) * warpFreq,
                                       (worldPos.Y + 421) * warpFreq,
                                       (worldPos.Z - 199) * warpFreq) * warpAmp;
    
        float qx = worldPos.X + wx;
        float qy = worldPos.Y + wy;
        float qz = worldPos.Z + wz;
    
        int cellX = (int)MathF.Floor(qx / (float)BiomeCellSize);
        int cellY = (int)MathF.Floor(qy / (float)BiomeCellSize);
        int cellZ = (int)MathF.Floor(qz / (float)BiomeCellSize);
    
        float bestDist2 = float.MaxValue;
        TerrainBiome bestBiome = TerrainBiome.Plains;
    
        // Search 3x3x3 macro-cell neighborhood
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cellX + dx;
                    int ny = cellY + dy;
                    int nz = cellZ + dz;
    
                    int h = GMath.Hash3i(nx, ny, nz, Seed);
    
                    // Jitter centers slightly to avoid axis-aligned planes
                    float jitterMag = BiomeCellSize * 0.20f;
    
                    float jx = biomeNoise.GetNoise(nx * 3.1f, ny * 3.1f, nz * 3.1f) * jitterMag;
                    float jy = biomeNoise.GetNoise(nx * 3.1f + 57f, ny * 3.1f - 19f, nz * 3.1f + 83f) * jitterMag;
                    float jz = biomeNoise.GetNoise(nx * 3.1f - 11f, ny * 3.1f + 23f, nz * 3.1f - 47f) * jitterMag;
    
                    float cx = nx * BiomeCellSize + BiomeCellSize * 0.5f + jx;
                    float cy = ny * BiomeCellSize + BiomeCellSize * 0.5f + jy;
                    float cz = nz * BiomeCellSize + BiomeCellSize * 0.5f + jz;
    
                    float dxw = qx - cx;
                    float dyw = qy - cy;
                    float dzw = qz - cz;
                    float d2 = dxw * dxw + dyw * dyw + dzw * dzw;
    
                    if (d2 < bestDist2)
                    {
                        bestDist2 = d2;
                        int pick = Math.Abs(h) % BiomeCycle.Length;
                        bestBiome = BiomeCycle[pick];
                    }
                }
            }
        }
    
        return bestBiome;
    }
}
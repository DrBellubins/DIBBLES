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
        biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
        biomeNoise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
        biomeNoise.SetCellularReturnType(FastNoiseLite.CellularReturnType.CellValue);
        biomeNoise.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
        biomeNoise.SetDomainWarpAmp(130f);
    
        // Drive average biome size; we’ll scale inputs per sample in TerrainSurface
        biomeNoise.SetFrequency(2f);
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var bRet = new BlockReturnData();
                bRet.RNG = rng;
                bRet.Noise = biomeNoise;
        
                bool biomeSelected = false;
                TerrainBiome selectedBiome = TerrainBiome.Plains;
        
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    int worldX = chunk.Position.X + x;
                    int worldY = chunk.Position.Y + y;
                    int worldZ = chunk.Position.Z + z;
        
                    bRet.LocalPos = new Vector3Int(x, y, z);
        
                    var current = Chunk.GetBlockTypeGlobal(new Vector3Int(worldX, worldY, worldZ));
                    if (current.Item1 != BlockType.Stone)
                        continue;
        
                    // Select biome only when this voxel is the surface (Air above)
                    if (!biomeSelected)
                    {
                        var above = Chunk.GetBlockTypeGlobal(new Vector3Int(worldX, worldY + 1, worldZ));
                        if (above.Item1 == BlockType.Air && above.Item2)
                        {
                            selectedBiome = ComputeBiomeAtCell3D(new Vector3Int(worldX, worldY, worldZ), biomeNoise);
                            biomeSelected = true;
                        }
                        else
                        {
                            continue; // Not surface yet; skip until we find it
                        }
                    }
        
                    // Route to the chosen biome generator for surface + lower 2–3 layers
                    switch (selectedBiome)
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
        
                    // Stop once the biome’s lower layer thickness is placed (<= 3)
                    if (bRet.FoundSurface && bRet.IslandDepth >= 3)
                        break;
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

    public TerrainBiome ComputeBiomeAtCell3D(Vector3Int worldPos, FastNoiseLite biomeNoise)
    {
        // Scale inputs so Cellular frequency maps to ~BiomeCellSize voxels
        float s = 1f / (float)BiomeCellSize;

        // CellValue is constant inside each cellular region and changes only at organic cell borders.
        float v = biomeNoise.GetNoise(worldPos.X * s, worldPos.Y * s, worldPos.Z * s); // [-1,1]

        // Map to [0..N) evenly
        int pick = (int)((v * 0.5f + 0.5f) * BiomeCycle.Length);
        pick = Math.Clamp(pick, 0, BiomeCycle.Length - 1);

        return BiomeCycle[pick];
    }
    
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
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Features;

public class TerrainSurface
{
    // Average biome region size in blocks (XZ). Tune between 256 and 1024.
    public const int BiomeCellSize = 128;
    
    // Width of the mixed band near biome borders (in blocks)
    public const int BiomeTransitionWidth = 6;
    
    private FastNoiseLite biomeNoise = new(Seed);
    private FastNoiseLite biomeDitherNoise = new(Seed);

    public TerrainSurface()
    {
        // Average biome size: BiomeCellSize controls spacing; frequency is inverse
        float freq = 1f / (float)BiomeCellSize;

        biomeNoise.SetSeed(Seed);
        biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
        biomeNoise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
        biomeNoise.SetCellularReturnType(FastNoiseLite.CellularReturnType.CellValue);
        biomeNoise.SetFrequency(freq);
        
        biomeDitherNoise.SetSeed(Seed);
        biomeDitherNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        biomeDitherNoise.SetFrequency(0.07f); // tune as desired
    }
    
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
        
        FastNoiseLite biomeWarpNoise = new(Seed);
        
        // Set warp noise per-chunk
        biomeWarpNoise.SetSeed(Seed);
        biomeWarpNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);;
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var bRet = new BlockReturnData();
                bRet.RNG = rng;
                bRet.Noise = biomeWarpNoise;
        
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
                            // Compute a blended biome near borders
                            var blend = ComputeBiomeBlendCell(
                                new Vector3Int(worldX, worldY, worldZ), biomeWarpNoise);
                            
                            float dither = biomeDitherNoise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                            
                            selectedBiome = dither < blend.BlendT ? blend.Primary : blend.Secondary;
                            biomeSelected = true;
                        }
                        else
                            continue; // Not surface yet; skip until we find it
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
    
    private struct BiomeBlend
    {
        public TerrainBiome Primary;
        public TerrainBiome Secondary;
        
        // 0 at the border (favor Secondary), 1 deep inside Primary
        public float BlendT;
    }

    private BiomeBlend ComputeBiomeBlendCell(Vector3Int worldPos, FastNoiseLite _biomeWarpNoise)
    {
        float warpAmp  = BiomeCellSize * 0.25f;
        float warpFreq = 1f / (BiomeCellSize * 10f);

        float wx = _biomeWarpNoise.GetNoise(worldPos.X * warpFreq, worldPos.Y * warpFreq, worldPos.Z * warpFreq) * warpAmp;
        float wy = _biomeWarpNoise.GetNoise((worldPos.X + 101) * warpFreq, (worldPos.Y - 311) * warpFreq, (worldPos.Z + 29) * warpFreq) * warpAmp;
        float wz = _biomeWarpNoise.GetNoise((worldPos.X - 73) * warpFreq, (worldPos.Y + 421) * warpFreq, (worldPos.Z - 199) * warpFreq) * warpAmp;

        float qx = worldPos.X + wx;
        float qy = worldPos.Y + wy;
        float qz = worldPos.Z + wz;

        // Keep noise-space coords explicit
        float freq = biomeNoise.GetFrequency();
        float nx = qx * freq;
        float ny = qy * freq;
        float nz = qz * freq;

        biomeNoise.GetNoiseWithF1F2(qx, qy, qz, out float f1, out float f2);

        int cx = (int)MathF.Floor(nx);
        int cy = (int)MathF.Floor(ny);
        int cz = (int)MathF.Floor(nz);

        int sx = cx, sy = cy, sz = cz;
        {
            float best = float.MaxValue;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int tx = cx + dx;
                int ty = cy + dy;
                int tz = cz + dz;

                // Centers in noise space (cell size = 1)
                float px = tx + 0.5f;
                float py = ty + 0.5f;
                float pz = tz + 0.5f;

                float dxn = nx - px;
                float dyn = ny - py;
                float dzn = nz - pz;

                float d = MathF.Sqrt(dxn * dxn + dyn * dyn + dzn * dzn);
                float err = MathF.Abs(d - f2);

                if (err < best)
                {
                    best = err;
                    sx = tx; sy = ty; sz = tz;
                }
            }
        }

        TerrainBiome primary = BiomeCycle[Math.Abs(GMath.Hash3i(cx, cy, cz, Seed)) % BiomeCycle.Length];
        TerrainBiome secondary = BiomeCycle[Math.Abs(GMath.Hash3i(sx, sy, sz, Seed)) % BiomeCycle.Length];

        // Convert blend width to noise space: world blocks -> multiply by freq
        float border = GMath.Clamp((f2 - f1) / (BiomeTransitionWidth * freq), 0f, 1f);
        float t = GMath.Smoothstep(border);

        return new BiomeBlend
        {
            Primary = primary,
            Secondary = secondary,
            BlendT = t
        };
    }
}
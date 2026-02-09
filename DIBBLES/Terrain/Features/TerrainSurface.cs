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
                                new Vector3Int(worldX, worldY, worldZ), biomeNoise, biomeWarpNoise);
                            
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

    private BiomeBlend ComputeBiomeBlendCell(Vector3Int worldPos, FastNoiseLite biomeNoise, FastNoiseLite biomeWarpNoise)
    {
        // Optional mild warp to curve borders
        float warpAmp  = BiomeCellSize * 0.25f;
        float warpFreq = 1f / (BiomeCellSize * 10f);
    
        float wx = biomeWarpNoise.GetNoise(worldPos.X * warpFreq, worldPos.Y * warpFreq, worldPos.Z * warpFreq) * warpAmp;
        float wy = biomeWarpNoise.GetNoise((worldPos.X + 101) * warpFreq, (worldPos.Y - 311) * warpFreq, (worldPos.Z + 29) * warpFreq) * warpAmp;
        float wz = biomeWarpNoise.GetNoise((worldPos.X - 73) * warpFreq, (worldPos.Y + 421) * warpFreq, (worldPos.Z - 199) * warpFreq) * warpAmp;
    
        float qx = worldPos.X + wx;
        float qy = worldPos.Y + wy;
        float qz = worldPos.Z + wz;
    
        // Cellular F1/F2 sampling
        biomeNoise.GetNoiseWithF1F2(qx, qy, qz, out float f1, out float f2);
    
        // Cell coordinates: use the internal helper from FastNoiseLite (emulate via floor on scaled coords)
        // We reconstruct cell coords by flooring the input space scaled by frequency
        float freq = biomeNoise.GetFrequency(); // if you expose it; otherwise store it
        int cx = (int)MathF.Floor(qx * freq);
        int cy = (int)MathF.Floor(qy * freq);
        int cz = (int)MathF.Floor(qz * freq);
    
        // Secondary cell is the F2 nearest; approximate by searching 3x3x3 around primary and picking the
        // one whose distance matches f2 best.
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
    
                // Cell corner (center) in noise space
                float px = (tx + 0.5f) / freq;
                float py = (ty + 0.5f) / freq;
                float pz = (tz + 0.5f) / freq;
    
                float d = Vector3.Distance(new Vector3(qx, qy, qz), new Vector3(px, py, pz));
    
                if (MathF.Abs(d - f2) < best)
                {
                    best = MathF.Abs(d - f2);
                    sx = tx; sy = ty; sz = tz;
                }
            }
        }
    
        // Hash cells to biome IDs
        TerrainBiome primary = BiomeCycle[Math.Abs(GMath.Hash3i(cx, cy, cz, Seed)) % BiomeCycle.Length];
        TerrainBiome secondary = BiomeCycle[Math.Abs(GMath.Hash3i(sx, sy, sz, Seed)) % BiomeCycle.Length];
    
        // Blend: closer to F1 center -> higher t; at border (F1 ~ F2) -> t ~ 0.5
        float border = GMath.Clamp((f2 - f1) / BiomeTransitionWidth, 0f, 1f);
        float t = GMath.Smoothstep(border);
    
        return new BiomeBlend
        {
            Primary = primary,
            Secondary = secondary,
            BlendT = t
        };
    }
}
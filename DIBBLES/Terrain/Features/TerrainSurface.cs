using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Features;

public class TerrainSurface
{
    // Average biome region size in blocks (XZ). Tune between 256 and 1024.
    public const int BiomeCellSize = 128;
    
    // Width of the mixed band near biome borders (in blocks)
    public const int BiomeTransitionWidth = 6;
    
    private FastNoiseLite biomeNoise = new(Seed);
    
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
        
        biomeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        //biomeNoise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
        //biomeNoise.SetCellularReturnType(FastNoiseLite.CellularReturnType.CellValue);
        //biomeNoise.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
        //biomeNoise.SetDomainWarpAmp(130f);
    
        // Drive average biome size; we’ll scale inputs per sample in TerrainSurface
        //biomeNoise.SetFrequency(2f);
        
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
                            // Compute a blended biome near borders
                            var blend = ComputeBiomeBlend3D(new Vector3Int(worldX, worldY, worldZ), biomeNoise);

                            // Deterministic dithering so the band doesn’t look like a hard line
                            float dither = biomeNoise.GetNoise(worldX * 0.07f,
                                worldY * 0.07f, worldZ * 0.07f) * 0.5f + 0.5f;
                            
                            selectedBiome = dither < blend.BlendT ? blend.Primary : blend.Secondary;

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

    public TerrainBiome ComputeBiomeAtCell3D(Vector3Int worldPos, FastNoiseLite warpNoise)
    {
        // Very low‑frequency warp so borders curve organically
        float warpAmp = BiomeCellSize * 0.35f;
        float warpFreq = (1f / (BiomeCellSize)) * 12f;

        float wx = warpNoise.GetNoise(worldPos.X * warpFreq,
            worldPos.Y * warpFreq,
            worldPos.Z * warpFreq) * warpAmp;

        float wy = warpNoise.GetNoise((worldPos.X + 101) * warpFreq,
            (worldPos.Y - 311) * warpFreq,
            (worldPos.Z + 29) * warpFreq) * warpAmp;

        float wz = warpNoise.GetNoise((worldPos.X - 73) * warpFreq,
            (worldPos.Y + 421) * warpFreq,
            (worldPos.Z - 199) * warpFreq) * warpAmp;

        float qx = worldPos.X + wx;
        float qy = worldPos.Y + wy;
        float qz = worldPos.Z + wz;

        int cellX = (int)MathF.Floor(qx / (float)BiomeCellSize);
        int cellY = (int)MathF.Floor(qy / (float)BiomeCellSize);
        int cellZ = (int)MathF.Floor(qz / (float)BiomeCellSize);

        int h = GMath.Hash3i(cellX, cellY, cellZ, Seed);
        int pick = Math.Abs(h) % BiomeCycle.Length;

        return BiomeCycle[pick];
    }
    
    private struct BiomeBlend
    {
        public TerrainBiome Primary;
        public TerrainBiome Secondary;
        
        // 0 at the border (favor Secondary), 1 deep inside Primary
        public float BlendT;
    }

    // Domain‑warp, then compute distance to nearest macro‑cell face to form a blend band.
    // Secondary biome is the neighbor across that face. Borders become curved by the warp,
    // but not harsh: we dither across BiomeTransitionWidth.
    private BiomeBlend ComputeBiomeBlend3D(Vector3Int worldPos, FastNoiseLite warpNoise)
    {
        float warpAmp  = BiomeCellSize * 0.35f;
        float warpFreq = 1f / (BiomeCellSize * 12f);
    
        float wx = warpNoise.GetNoise(worldPos.X * warpFreq, worldPos.Y * warpFreq, worldPos.Z * warpFreq) * warpAmp;
        float wy = warpNoise.GetNoise((worldPos.X + 101) * warpFreq, (worldPos.Y - 311) * warpFreq, (worldPos.Z + 29) * warpFreq) * warpAmp;
        float wz = warpNoise.GetNoise((worldPos.X - 73) * warpFreq, (worldPos.Y + 421) * warpFreq, (worldPos.Z - 199) * warpFreq) * warpAmp;
    
        float qx = worldPos.X + wx;
        float qy = worldPos.Y + wy;
        float qz = worldPos.Z + wz;
    
        // Primary macro cell
        int cx = (int)MathF.Floor(qx / (float)BiomeCellSize);
        int cy = (int)MathF.Floor(qy / (float)BiomeCellSize);
        int cz = (int)MathF.Floor(qz / (float)BiomeCellSize);
    
        // Local position within the cell [0..CellSize)
        float lx = qx - cx * (float)BiomeCellSize;
        float ly = qy - cy * (float)BiomeCellSize;
        float lz = qz - cz * (float)BiomeCellSize;
    
        // Distance to the nearest face along each axis
        float dxFace = MathF.Min(lx, BiomeCellSize - lx);
        float dyFace = MathF.Min(ly, BiomeCellSize - ly);
        float dzFace = MathF.Min(lz, BiomeCellSize - lz);
    
        // Nearest axis determines which neighbor cell is the "other" biome across the boundary
        int nx = cx, ny = cy, nz = cz;
        float minFace = dxFace;
        int axis = 0; // 0=X,1=Y,2=Z
    
        if (dyFace < minFace) { minFace = dyFace; axis = 1; }
        if (dzFace < minFace) { minFace = dzFace; axis = 2; }
    
        // Direction to the neighbor (which side of the cell center we’re on)
        if (axis == 0)
            nx = cx + (lx < BiomeCellSize * 0.5f ? -1 : 1);
        else if (axis == 1)
            ny = cy + (ly < BiomeCellSize * 0.5f ? -1 : 1);
        else
            nz = cz + (lz < BiomeCellSize * 0.5f ? -1 : 1);
    
        // Pick biomes by hashing each macro cell
        int hPrimary   = GMath.Hash3i(cx, cy, cz, Seed);
        int hSecondary = GMath.Hash3i(nx, ny, nz, Seed);
    
        TerrainBiome primary   = BiomeCycle[Math.Abs(hPrimary)   % BiomeCycle.Length];
        TerrainBiome secondary = BiomeCycle[Math.Abs(hSecondary) % BiomeCycle.Length];
    
        // Blend factor grows from 0 at the border to 1 inside the primary cell
        float t = GMath.Clamp(minFace / (float)BiomeTransitionWidth, 0f, 1f);
        
        // Smooth falloff looks nicer than linear
        t = GMath.Smoothstep(t);
    
        return new BiomeBlend
        {
            Primary = primary,
            Secondary = secondary,
            BlendT = t
        };
    }
}
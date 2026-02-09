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
                
                // Pick biome once per XZ column using world coordinates
                var worldColumnXZ = new Vector3Int(chunk.Position.X + x,
                    chunk.Position.Y, chunk.Position.Z + z);
                
                var selectedBiome = ComputeBiomeAt(worldColumnXZ);
                
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;

                    blockReturnData.LocalPos = new Vector3Int(x, y, z);

                    var currentType = Chunk.GetBlockTypeGlobal(new Vector3Int(worldX, worldY, worldZ));
                    
                    if (currentType.Item1 != BlockType.Stone)
                        continue;

                    // Route to the selected biome’s surface generator
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

    public TerrainBiome ComputeBiomeAt(Vector3Int worldPos)
    {
        // Work on XZ; biomes vary horizontally
        int cellX = (int)MathF.Floor(worldPos.X / (float)BiomeCellSize);
        int cellZ = (int)MathF.Floor(worldPos.Z / (float)BiomeCellSize);

        float bestDist2 = float.MaxValue;
        TerrainBiome bestBiome = TerrainBiome.Plains;

        // Search 3x3 neighborhood of macro cells for nearest jittered center
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = cellX + dx;
                int nz = cellZ + dz;

                int h = GMath.Hash2i(nx, nz, Seed);

                // Deterministic jitter inside the macro cell (keep below cell size)
                float jx = (((h & 0xFFFF) / 65535f) - 0.5f) * BiomeCellSize * 0.2f;
                float jz = ((((h >> 16) & 0xFFFF) / 65535f) - 0.5f) * BiomeCellSize * 0.2f;

                float cx = nx * BiomeCellSize + BiomeCellSize * 0.5f + jx;
                float cz = nz * BiomeCellSize + BiomeCellSize * 0.5f + jz;

                float dxw = worldPos.X - cx;
                float dzw = worldPos.Z - cz;
                float d2 = dxw * dxw + dzw * dzw;

                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    int pick = Math.Abs(h) % BiomeCycle.Length;
                    bestBiome = BiomeCycle[pick];
                }
            }
        }

        return bestBiome;
    }
}
using DIBBLES.Systems;
using DIBBLES.Utils;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Biomes;

public class DesertBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;

        var worldPos = chunk.Position + returnData.LocalPos;
        var typeAbove = Chunk.GetBlockTypeGlobal(new Vector3Int(worldPos.X, worldPos.Y + 1, worldPos.Z));
        
        if (!returnData.FoundSurface)
        {
            if (typeAbove.Item1 == BlockType.Air && typeAbove.Item2)
            {
                // This is the surface
                chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Sand);
            
                returnData.FoundSurface = true;
                returnData.IslandDepth = 0;
            }
        }
        else if (returnData.IslandDepth < 3) // lower sand thickness = 3
        {
            chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Sandstone);
            returnData.IslandDepth++;
        }
        
        chunk.SetBiomeAt(returnData.LocalPos.X, returnData.LocalPos.Y, returnData.LocalPos.Z, TerrainBiome.Desert);
        
        bRetData = returnData;
    }
    
    public void GenerateDecorations(Chunk chunk)
    {
        long chunkSeed = Seed 
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
        
        var rng = new SeededRandom(chunkSeed);
        
        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
        {
            for (int y = ChunkSize - 1; y >= 0; y--)
            {
                var currentBlockType =  chunk.GetTypeAt(x, y, z);
                var currentBiome = chunk.GetBiomeAt(x, y, z);
                var pos = new Vector3Int(x, y, z);

                if (currentBiome == TerrainBiome.Desert && currentBlockType == BlockType.Sand)
                {
                    // Wisps
                    if (rng.NextChance(0.5f))
                    {
                        var rndHeight = rng.NextInt(3, 6);
                        BiomeUtils.GenerateBlockAbove(pos, chunk, rndHeight, BlockType.Wisp);
                    }
                }
            }
        }
    }
}
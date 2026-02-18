using DIBBLES.Systems;
using DIBBLES.Utils;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Biomes;

public class PlainsBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;

        var worldPos = chunk.Position + returnData.LocalPos;
        var positionAbove = new Vector3Int(worldPos.X, worldPos.Y + 1, worldPos.Z);
        var isCave = Chunk.GetCaveGlobal(positionAbove);

        if (!isCave)
        {
            var typeAbove = Chunk.GetBlockTypeGlobal(positionAbove);
            
            if (!returnData.FoundSurface)
            {
                if (typeAbove.Item1 == BlockType.Air && typeAbove.Item2)
                {
                    // This is the surface
                    chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Grass);
            
                    returnData.FoundSurface = true;
                    returnData.IslandDepth = 0;
                }
            }
            else if (returnData.IslandDepth < 3) // dirt thickness = 3
            {
                chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Dirt);
                returnData.IslandDepth++;
            }
        }
        
        chunk.SetBiomeAt(returnData.LocalPos.X, returnData.LocalPos.Y, returnData.LocalPos.Z, TerrainBiome.Plains);
        
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

                if (currentBiome == TerrainBiome.Plains && currentBlockType == BlockType.Grass)
                {
                    // Grass blades/flowers
                    if (rng.NextChance(35f))
                    {
                        var worldAbove = chunk.Position + pos + new Vector3Int(0, 1, 0);
                        var aboveType = Chunk.GetBlockTypeGlobal(worldAbove);

                        
                        if (aboveType.Item1 == BlockType.Air)
                        {
                            float pick = rng.NextFloat(); // [0,1)

                            if (pick < 0.85f)
                                Chunk.SetBlockTypeGlobal(worldAbove, BlockType.GrassBlades);
                            else
                            {
                                // Remaining 15%: 50-50 between red and blue
                                if (rng.NextChance(50f))
                                    Chunk.SetBlockTypeGlobal(worldAbove, BlockType.RedFlower);
                                else
                                    Chunk.SetBlockTypeGlobal(worldAbove, BlockType.BlueFlower);
                            };
                        }
                    }
                    
                    // Trees
                    if (rng.NextChance(0.5f))
                        BiomeUtils.GenerateTrees(pos, chunk);
                }
            }
        }
    }
}
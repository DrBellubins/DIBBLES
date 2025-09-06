using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class PlainsBiome
{
    public void Generate(ref BlockReturnData bRetData)
    {
        var returnData = bRetData;
        
        if (!returnData.FoundSurface)
        {
            // This is the surface
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, Block.Prefabs[BlockType.Grass]);
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // dirt thickness = 3
        {
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, Block.Prefabs[BlockType.Dirt]);
            returnData.IslandDepth++;
        }
        else
        {
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, Block.Prefabs[BlockType.Stone]);
            returnData.IslandDepth++;
        }
        
        returnData.CurrentBlock.Info.Biome = TerrainBiome.Plains;
        
        bRetData = returnData;
    }
}
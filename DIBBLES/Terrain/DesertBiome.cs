using DIBBLES.Systems;

namespace DIBBLES.Terrain;

public class DesertBiome
{
    public void Generate(ref BlockReturnData bRetData)
    {
        var returnData = bRetData;
        
        if (!returnData.FoundSurface)
        {
            // This is the surface
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, BlockType.Sand);
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // sand thickness = 4
        {
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, BlockType.Sand);
            returnData.IslandDepth++;
        }
        else
        {
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, BlockType.Stone);
            returnData.IslandDepth++;
        }

        returnData.CurrentBlock.Biome = TerrainBiome.Desert;
        
        bRetData = returnData;
    }
}
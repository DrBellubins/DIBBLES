using DIBBLES.Systems;

namespace DIBBLES.Terrain;

public class SnowlandsBiome
{
    public void Generate(ref BlockReturnData bRetData)
    {
        var returnData = bRetData;
        
        if (!returnData.FoundSurface)
        {
            // This is the surface
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, BlockType.Snow);
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // snow thickness = 4
        {
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, BlockType.Snow); // TODO: Should be ice
            returnData.IslandDepth++;
        }
        else
        {
            returnData.CurrentBlock = new BlockData(returnData.WorldPos, BlockType.Stone);
            returnData.IslandDepth++;
        }

        returnData.CurrentBlock.Biome = TerrainBiome.Snowlands;
        
        bRetData = returnData;
    }
}
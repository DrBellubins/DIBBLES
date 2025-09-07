using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class PlainsBiome
{
    public void Generate(ref BlockReturnData bRetData)
    {
        var returnData = bRetData;
        
        var aboveCoord = new Vector3Int(returnData.WorldPos.X, returnData.WorldPos.Y + 1, returnData.WorldPos.Z);
        var aboveBlock = TerrainHelpers.GetBlockAtWorldPos(aboveCoord);

        if (aboveBlock.Type == BlockType.Air && !returnData.FoundSurface)
        {
            // This is the surface
            returnData.CurrentBlock = new Block(returnData.WorldPos, BlockType.Grass);
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // dirt thickness = 3
        {
            returnData.CurrentBlock = new Block(returnData.WorldPos, BlockType.Dirt);
            returnData.IslandDepth++;
        }
        else
        {
            returnData.CurrentBlock = new Block(returnData.WorldPos, BlockType.Stone);
            returnData.IslandDepth++;
        }
        
        returnData.CurrentBlock.Biome = TerrainBiome.Plains;
        
        bRetData = returnData;
    }
}
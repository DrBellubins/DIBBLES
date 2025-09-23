using DIBBLES.Systems;

namespace DIBBLES.Terrain;

public class SnowlandsBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;
        
        if (!returnData.FoundSurface)
        {
            // This is the surface
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Snow);
            
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // dirt thickness = 3
        {
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Snow); // TODO: Should be ice
            returnData.IslandDepth++;
        }
        else
        {
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Stone);
            returnData.IslandDepth++;
        }
        
        chunk.SetBiomeAt(returnData.WorldPos.X, returnData.WorldPos.Y, returnData.WorldPos.Z, TerrainBiome.Snowlands);
        
        bRetData = returnData;
    }
}
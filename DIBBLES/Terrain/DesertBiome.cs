using DIBBLES.Systems;

namespace DIBBLES.Terrain;

public class DesertBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;
        
        if (!returnData.FoundSurface)
        {
            // This is the surface
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Sand);
            
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // dirt thickness = 3
        {
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Sand);
            returnData.IslandDepth++;
        }
        else
        {
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Stone);
            returnData.IslandDepth++;
        }
        
        chunk.SetBiomeAt(returnData.WorldPos.X, returnData.WorldPos.Y, returnData.WorldPos.Z, TerrainBiome.Desert);
        
        bRetData = returnData;
    }
}
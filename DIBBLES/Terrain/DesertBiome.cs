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
            chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Sand);
            
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // dirt thickness = 3
        {
            chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Sand);
            returnData.IslandDepth++;
        }
        else
        {
            chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Stone);
            returnData.IslandDepth++;
        }
        
        chunk.SetBiomeAt(returnData.LocalPos.X, returnData.LocalPos.Y, returnData.LocalPos.Z, TerrainBiome.Desert);
        
        bRetData = returnData;
    }
}
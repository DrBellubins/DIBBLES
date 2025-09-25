using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class PlainsBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;

        var worldPos = chunk.Position + returnData.LocalPos;
        var noiseCheck = returnData.Noise.GetNoise(worldPos.Y);
        
        if (noiseCheck < 0f)
        {
            // This is the surface
            chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Grass);
            
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        /*else if (returnData.IslandDepth < 3) // dirt thickness = 3
        {
            chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Dirt);
            returnData.IslandDepth++;
        }
        else
        {
            returnData.IslandDepth++;
        }*/
        
        chunk.SetBiomeAt(returnData.LocalPos.X, returnData.LocalPos.Y, returnData.LocalPos.Z, TerrainBiome.Plains);
        
        bRetData = returnData;
    }
}
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class PlainsBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;

        var blockAboveType = Chunk.GetBlockTypeGlobal(new Vector3Int(returnData.WorldPos.X, returnData.WorldPos.Y + 1, returnData.WorldPos.Z));
        
        if (blockAboveType == BlockType.Air)
        {
            // This is the surface
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Grass);
            
            returnData.FoundSurface = true;
            returnData.IslandDepth = 0;
        }
        else if (returnData.IslandDepth < 3) // dirt thickness = 3
        {
            chunk.SetTypeAt(returnData.WorldPos.X,  returnData.WorldPos.Y, returnData.WorldPos.Z, BlockType.Dirt);
            returnData.IslandDepth++;
        }
        else
        {
            returnData.IslandDepth++;
        }
        
        chunk.SetBiomeAt(returnData.WorldPos.X, returnData.WorldPos.Y, returnData.WorldPos.Z, TerrainBiome.Plains);
        
        bRetData = returnData;
    }
}
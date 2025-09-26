using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class SnowlandsBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;

        var worldPos = chunk.Position + returnData.LocalPos;
        var typeAbove = Chunk.GetBlockTypeGlobal(new Vector3Int(worldPos.X, worldPos.Y + 1, worldPos.Z));
        
        if (!returnData.FoundSurface)
        {
            if (typeAbove.Item1 == BlockType.Air && typeAbove.Item2)
            {
                // This is the surface
                chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Snow);
            
                returnData.FoundSurface = true;
                returnData.IslandDepth = 0;
            }
        }
        else if (returnData.IslandDepth < 3) // lower snow thickness = 3
        {
            chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Snow); // TODO: Should be ice!
            returnData.IslandDepth++;
        }
        
        chunk.SetBiomeAt(returnData.LocalPos.X, returnData.LocalPos.Y, returnData.LocalPos.Z, TerrainBiome.Plains);
        
        bRetData = returnData;
    }
}
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class PlainsBiome
{
    public void Generate(Chunk chunk, ref BlockReturnData bRetData)
    {
        var returnData = bRetData;

        var worldPos = chunk.Position + returnData.LocalPos;
        var positionAbove = new Vector3Int(worldPos.X, worldPos.Y + 1, worldPos.Z);
        var isCave = Chunk.GetCaveGlobal(positionAbove);

        if (!isCave)
        {
            var typeAbove = Chunk.GetBlockTypeGlobal(positionAbove);
            
            if (!returnData.FoundSurface)
            {
                if (typeAbove.Item1 == BlockType.Air && typeAbove.Item2)
                {
                    // This is the surface
                    chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Grass);
            
                    returnData.FoundSurface = true;
                    returnData.IslandDepth = 0;
                }
            }
            else if (returnData.IslandDepth < 3) // dirt thickness = 3
            {
                chunk.SetTypeAt(returnData.LocalPos.X,  returnData.LocalPos.Y, returnData.LocalPos.Z, BlockType.Dirt);
                returnData.IslandDepth++;
            }
        }
        
        chunk.SetBiomeAt(returnData.LocalPos.X, returnData.LocalPos.Y, returnData.LocalPos.Z, TerrainBiome.Plains);
        
        bRetData = returnData;
    }
}
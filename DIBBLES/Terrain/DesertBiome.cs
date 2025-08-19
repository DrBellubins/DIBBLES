using DIBBLES.Systems;

namespace DIBBLES.Terrain;

public class DesertBiome
{
    public void Generate(ref BlockReturnData bRetData)
    {
        if (!bRetData.FoundSurface)
        {
            // This is the surface
            bRetData.CurrentBlock = new Block(bRetData.WorldPos, Block.Prefabs[BlockType.Sand]);
            bRetData.FoundSurface = true;
            bRetData.IslandDepth = 0;
            bRetData.SurfaceY = bRetData.WorldPos.Y;
        }
        else if (bRetData.IslandDepth < 3) // sand thickness = 4
        {
            bRetData.CurrentBlock = new Block(bRetData.WorldPos, Block.Prefabs[BlockType.Sand]);
            bRetData.IslandDepth++;
        }
        else
        {
            bRetData.CurrentBlock = new Block(bRetData.WorldPos, Block.Prefabs[BlockType.Stone]);
            bRetData.IslandDepth++;
        }
    }
}
using DIBBLES.Systems;

namespace DIBBLES.Terrain;

public class SnowlandsBiome
{
    public void Generate(ref BlockReturnData bRetData)
    {
        if (!bRetData.FoundSurface)
        {
            // This is the surface
            bRetData.CurrentBlock = new Block(bRetData.WorldPos, Block.Prefabs[BlockType.Snow]);
            bRetData.FoundSurface = true;
            bRetData.IslandDepth = 0;
            bRetData.SurfaceY = bRetData.WorldPos.Y;
        }
        else if (bRetData.IslandDepth < 3) // snow thickness = 4
        {
            bRetData.CurrentBlock = new Block(bRetData.WorldPos, Block.Prefabs[BlockType.Snow]); // TODO: Should be ice
            bRetData.IslandDepth++;
        }
        else
        {
            bRetData.CurrentBlock = new Block(bRetData.WorldPos, Block.Prefabs[BlockType.Stone]);
            bRetData.IslandDepth++;
        }
    }
}
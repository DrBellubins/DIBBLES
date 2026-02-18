using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Features;

/// <summary>
/// Things generated in the world after the terrain.
/// Trees, grass blades, buildings, etc.
/// </summary>
public class TerrainDecorations
{
    public void Generate(Chunk chunk)
    {
        // TODO: This is really messy, should be done in some cleaner way.
        plainsBiome.GenerateDecorations(chunk);
    }
}
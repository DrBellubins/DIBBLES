using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class TerrainHelpers
{
    public static Block GetBlockAtWorldPos(Vector3Int worldPos)
    {
        int chunkX = (int)Math.Floor((float)worldPos.X / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        int chunkY = (int)Math.Floor((float)worldPos.Y / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        int chunkZ = (int)Math.Floor((float)worldPos.Z / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (TerrainGeneration.ECSChunks.TryGetValue(chunkCoord, out var chunk))
        {
            int localX = worldPos.X - chunkX;
            int localY = worldPos.Y - chunkY;
            int localZ = worldPos.Z - chunkZ;
            
            if (localX >= 0 && localX < TerrainGeneration.ChunkSize &&
                localY >= 0 && localY < TerrainGeneration.ChunkSize &&
                localZ >= 0 && localZ < TerrainGeneration.ChunkSize)
            {
                return chunk.GetBlock(localX, localY, localZ);
            }
        }
        
        return new Block(worldPos, BlockType.Air); // Treat out-of-bounds as air
    }
}
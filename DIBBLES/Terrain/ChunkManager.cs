using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class ChunkManager
{
    private Dictionary<Vector3Int, Chunk> chunks = TerrainGeneration.ECSChunks;
    
    public Chunk GetOrCreateChunk(Vector3Int chunkPos, TerrainGeneration terrainGeneration)
    {
        if (!chunks.TryGetValue(chunkPos, out var chunk))
        {
            chunk = new Chunk(chunkPos);
            
            //terrainGeneration.GenerateChunkData(chunk);
            
            chunks[chunkPos] = chunk;
        }
        return chunk;
    }
}
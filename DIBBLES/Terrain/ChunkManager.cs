using System.Collections.Concurrent;
using DIBBLES.Utils;

namespace DIBBLES.Terrain;

public class ChunkManager
{
    private ConcurrentDictionary<Vector3Int, Chunk> chunks = TerrainGeneration.ECSChunks;
    
    public Chunk GetOrCreateChunk(Vector3Int chunkPos, TerrainGeneration terrainGeneration)
    {
        if (!chunks.TryGetValue(chunkPos, out var chunk))
        {
            chunk = new Chunk(chunkPos);
            terrainGeneration.GenerateChunkData(chunk); // Generate data before adding!
            chunk.GenerationState = ChunkGenerationState.TerrainGenerated;
            chunks.TryAdd(chunkPos, chunk);
        }
        return chunk;
    }
}
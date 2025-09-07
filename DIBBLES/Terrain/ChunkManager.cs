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
            // Add to ECSChunks *before* generating data so it's visible to all systems
            chunks.TryAdd(chunkPos, chunk);
            terrainGeneration.GenerateChunkData(chunk); // Now chunk is visible, but .GenerationState controls readiness
            chunk.GenerationState = ChunkGenerationState.TerrainGenerated;
        }
        return chunk;
    }
}
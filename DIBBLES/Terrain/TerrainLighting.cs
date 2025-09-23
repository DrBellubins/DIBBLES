using DIBBLES.Utils;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainLighting
{
    public void GenerateNew(Chunk chunk)
    {
        placeLights(chunk);
        //placeLightsTest(chunk);
        floodFill(chunk);
    }

    private void placeLights(Chunk chunk)
    {
        // Step 1: Set block light for emissives
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
            chunk.SetLightLevelAt(x, y, z, chunk.GetInfoAt(x, y, z).LightEmission);
        
        // Step 2: Place skylights
        int halfRD = RenderDistance / 2;
        int minChunk = -halfRD;
        int maxChunk = halfRD;

        // Directions: (dir, edge selector)
        var directions = new[]
        {
            (dir: new Vector3Int(1, 0, 0), axis: 0, edge: maxChunk * ChunkSize),    // +X
            (dir: new Vector3Int(-1, 0, 0), axis: 0, edge: minChunk * ChunkSize),   // -X
            (dir: new Vector3Int(0, 1, 0), axis: 1, edge: maxChunk * ChunkSize),    // +Y
            (dir: new Vector3Int(0, -1, 0), axis: 1, edge: minChunk * ChunkSize),   // -Y
            (dir: new Vector3Int(0, 0, 1), axis: 2, edge: maxChunk * ChunkSize),    // +Z
            (dir: new Vector3Int(0, 0, -1), axis: 2, edge: minChunk * ChunkSize),   // -Z
        };

        // For each direction, process the corresponding edge
        foreach (var (dir, axis, edge) in directions)
        {
            // For every block in the plane at the edge of the render distance
            for (int c0 = minChunk * ChunkSize; c0 <= maxChunk * ChunkSize; c0 += ChunkSize)
            for (int c1 = minChunk * ChunkSize; c1 <= maxChunk * ChunkSize; c1 += ChunkSize)
            for (int i0 = 0; i0 < ChunkSize; i0++)
            for (int i1 = 0; i1 < ChunkSize; i1++)
            {
                int[] coords = new int[3];
                coords[axis] = edge;
                coords[(axis + 1) % 3] = c0 + i0;
                coords[(axis + 2) % 3] = c1 + i1;

                Vector3Int start = new Vector3Int(coords[0], coords[1], coords[2]);
                castSkylightRay(start, dir);
            }
        }
    }

    // TODO: Needs to be cross-chunk based on SetLightLevelGlobal
    private void floodFill(Chunk chunk)
    {
        Queue<(Chunk chunk, Vector3Int pos)> queue = new();

        // Enqueue all blocks in this chunk with block light > 0
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var blockLightLevel = chunk.GetLightLevelAt(x, y, z);
            
            if (blockLightLevel > 0)
                queue.Enqueue((chunk, new Vector3Int(x, y, z)));
        }

        while (queue.Count > 0)
        {
            var (curChunk, pos) = queue.Dequeue();
            var lightLevel = curChunk.GetLightLevelAt(pos.X, pos.Y, pos.Z);
    
            // Skip if no light to propagate
            if (lightLevel <= 1) continue;
    
            // Define the six possible directions (±X, ±Y, ±Z)
            Vector3Int[] directions = {
                new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 1, 0),
                new Vector3Int(0, -1, 0),
                new Vector3Int(0, 0, 1),
                new Vector3Int(0, 0, -1)
            };
    
            // Check all six neighbors
            foreach (var dir in directions)
            {
                Vector3Int newPos = new Vector3Int(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z);
        
                // Skip if out of bounds
                if (newPos.X < 0 || newPos.X >= ChunkSize || 
                    newPos.Y < 0 || newPos.Y >= ChunkSize || 
                    newPos.Z < 0 || newPos.Z >= ChunkSize)
                    continue;
                
                var neighborBlockType = curChunk.GetTypeAt(newPos.X, newPos.Y, newPos.Z);
                var neighborBlockInfo = curChunk.GetInfoAt(newPos.X, newPos.Y, newPos.Z);
                var neighborBlockLightLevel = curChunk.GetLightLevelAt(newPos.X, newPos.Y, newPos.Z);
                
                // Only propagate to transparent (except leaves for thicker look) or air blocks
                if (neighborBlockType == BlockType.Air ||
                    (neighborBlockType != BlockType.Leaves && neighborBlockInfo.IsTransparent))
                {
                    byte newLight = (byte)(lightLevel - 1);
            
                    // Only update if the new light is brighter
                    if (newLight > neighborBlockLightLevel)
                    {
                        curChunk.SetLightLevelAt(newPos.X, newPos.Y, newPos.Z, newLight);
                        queue.Enqueue((curChunk, newPos)); // Add to queue for further propagation
                    }
                }
            }
        }
    }
    
    private void castSkylightRay(Vector3Int start, Vector3Int direction)
    {
        int maxSteps = RenderDistance * ChunkSize;
        Vector3Int pos = start;

        // --- NEW: Check initial block at edge ---
        var initialBlockType = Chunk.GetBlockTypeGlobal(pos);
        
        if (initialBlockType != BlockType.Air)
            return; // Terminate immediately if not air

        for (int step = 0; step < maxSteps; step++)
        {
            var blockType = Chunk.GetBlockTypeGlobal(pos);
            
            if (blockType != BlockType.Air)
                break;

            // --- NEW: Use cross-chunk SetLightLevelGlobal ---
            Chunk.SetLightLevelGlobal(pos, 15);

            pos += direction;
        }
    }
}
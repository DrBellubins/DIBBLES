using System.Numerics;
using DIBBLES.Utils;
using Raylib_cs;

using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

public class TerrainGameplay
{
    private Vector3Int selectedNormal;
    
    public void Update(Camera3D camera)
    {
        var (block, normal) = selectBlock(camera);
        SelectedBlock = block;
        selectedNormal = normal;
    }
    
    private (Block?, Vector3Int) selectBlock(Camera3D camera)
    {
        var rayPosition = camera.Position;
        var rayDirection = Vector3.Normalize(camera.Target - camera.Position);
    
        var mapPos = new Vector3Int(
            (int)MathF.Floor(rayPosition.X),
            (int)MathF.Floor(rayPosition.Y),
            (int)MathF.Floor(rayPosition.Z)
        );
    
        // Handle near-zero components to avoid div-by-zero and precision issues
        const float epsilon = 1e-6f;
        
        var deltaDist = new Vector3(
            Math.Abs(rayDirection.X) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.X),
            Math.Abs(rayDirection.Y) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.Y),
            Math.Abs(rayDirection.Z) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.Z)
        );
    
        var step = new Vector3Int(
            rayDirection.X > 0 ? 1 : (rayDirection.X < 0 ? -1 : 0),
            rayDirection.Y > 0 ? 1 : (rayDirection.Y < 0 ? -1 : 0),
            rayDirection.Z > 0 ? 1 : (rayDirection.Z < 0 ? -1 : 0)
        );
    
        var sideDist = new Vector3(
            rayDirection.X == 0 ? float.PositiveInfinity : (rayDirection.X > 0 ? (mapPos.X + 1f - rayPosition.X) * deltaDist.X : (rayPosition.X - mapPos.X) * deltaDist.X),
            rayDirection.Y == 0 ? float.PositiveInfinity : (rayDirection.Y > 0 ? (mapPos.Y + 1f - rayPosition.Y) * deltaDist.Y : (rayPosition.Y - mapPos.Y) * deltaDist.Y),
            rayDirection.Z == 0 ? float.PositiveInfinity : (rayDirection.Z > 0 ? (mapPos.Z + 1f - rayPosition.Z) * deltaDist.Z : (rayPosition.Z - mapPos.Z) * deltaDist.Z)
        );
    
        // Optional small nudge to avoid boundary ambiguities
        const float nudge = 1e-5f;
        
        if (sideDist.X < nudge) sideDist.X += nudge;
        if (sideDist.Y < nudge) sideDist.Y += nudge;
        if (sideDist.Z < nudge) sideDist.Z += nudge;
    
        Block? hitBlock = null;
        Vector3Int hitNormal = Vector3Int.Zero;
    
        // Check starting voxel first
        var startChunkPos = new Vector3Int(
            (int)Math.Floor((float)mapPos.X / ChunkSize) * ChunkSize,
            (int)Math.Floor((float)mapPos.Y / ChunkSize) * ChunkSize,
            (int)Math.Floor((float)mapPos.Z / ChunkSize) * ChunkSize
        );
        
        if (Chunks.TryGetValue(startChunkPos, out var startChunk))
        {
            var localX = (mapPos.X - startChunkPos.X);
            var localY = (mapPos.Y - startChunkPos.X);
            var localZ = (mapPos.Z - startChunkPos.Z);
            
            if (localX >= 0 && localX < ChunkSize && localY >= 0 && localY < ChunkSize && localZ >= 0 && localZ < ChunkSize)
            {
                var block = startChunk.Blocks[localX, localY, localZ];
                
                if (block.Info.Type != BlockType.Air)
                {
                    hitBlock = block;
                    return (hitBlock, hitNormal); // Normal arbitrary for starting block hit; could compute based on direction or set to zero
                }
            }
        }
    
        // DDA loop
        const int maxSteps = 500; // Adjust based on ReachDistance
        int steps = 0;
        
        while (steps < maxSteps)
        {
            steps++;
    
            float nextT;
    
            if (sideDist.X <= sideDist.Y && sideDist.X <= sideDist.Z)
            {
                nextT = sideDist.X;
                sideDist.X += deltaDist.X;
                mapPos.X += step.X;
                hitNormal = new Vector3Int(-step.X, 0, 0);
            }
            else if (sideDist.Y <= sideDist.Z)
            {
                nextT = sideDist.Y;
                sideDist.Y += deltaDist.Y;
                mapPos.Y += step.Y;
                hitNormal = new Vector3Int(0, -step.Y, 0);
            }
            else
            {
                nextT = sideDist.Z;
                sideDist.Z += deltaDist.Z;
                mapPos.Z += step.Z;
                hitNormal = new Vector3Int(0, 0, -step.Z);
            }
    
            if (nextT > ReachDistance) break;
    
            // Check current voxel
            var currentChunkPos = new Vector3Int(
                (int)Math.Floor((float)mapPos.X / ChunkSize) * ChunkSize,
                (int)Math.Floor((float)mapPos.Y / ChunkSize) * ChunkSize,
                (int)Math.Floor((float)mapPos.Z / ChunkSize) * ChunkSize
            );
    
            if (!Chunks.TryGetValue(currentChunkPos, out var chunk)) continue;
    
            var localX = (mapPos.X - currentChunkPos.X);
            var localY = (mapPos.Y - currentChunkPos.Y);
            var localZ = (mapPos.Z - currentChunkPos.Z);
    
            if (localX < 0 || localX >= ChunkSize || localY < 0 || localY >= ChunkSize || localZ < 0 || localZ >= ChunkSize) continue;
    
            var block = chunk.Blocks[localX, localY, localZ];
    
            if (block.Info.Type != BlockType.Air)
            {
                hitBlock = block;
                break;
            }
        }
    
        return (hitBlock, hitNormal);
    }
    
    public void BreakBlock()
    {
        if (SelectedBlock == null)
            return;

        // Get the chunk containing the selected block
        var blockPos = SelectedBlock.Position;
        
        int chunkX = (int)Math.Floor((float)blockPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)blockPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)blockPos.Z / ChunkSize) * ChunkSize;
        
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!Chunks.TryGetValue(chunkCoord, out var chunk))
            return;

        // Calculate local block coordinates within the chunk
        var localPos = blockPos - chunkCoord;
        var localX = localPos.X;
        var localY = localPos.Y;
        var localZ = localPos.Z;

        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkSize ||
            localZ < 0 || localZ >= ChunkSize)
            return;

        // Set block to Air If block is breakable
        var oldBlock = chunk.Blocks[localX, localY, localZ];

        if (oldBlock.Info.Hardness != 10)
        {
            chunk.Blocks[localX, localY, localZ] = new Block(blockPos, Block.Prefabs[BlockType.Air]);
            chunk.Info.Modified = true;

            // Update lighting if the broken block was opaque or emissive
            Lighting.Generate(chunk);
        
            // Regenerate mesh
            Raylib.UnloadModel(chunk.Model); // Unload old model
        
            var meshData = TMesh.GenerateMeshData(chunk);
            chunk.Model = TMesh.UploadMesh(meshData);

            TMesh.RemeshNeighborsIfBlockOnEdge(chunk, blockPos);
        
            // Add to modified chunks for saving
            if (WorldSave.Data.ModifiedChunks.All(c => c.Position != chunk.Position))
                WorldSave.Data.ModifiedChunks.Add(chunk);

            // Play break sound
            var sound = Block.Sounds[SelectedBlock.Info.Type].RND;
        
            if (sound.FrameCount != 0)
                Raylib.PlaySound(sound);
        }
    }
    
    public void PlaceBlock(BlockType blockType)
    {
        if (SelectedBlock == null || blockType == BlockType.Air)
            return;

        // Quantize the normal to the nearest axis-aligned direction
        Vector3Int normal = selectedNormal;
        
        Vector3Int quantizedNormal = new Vector3Int(
            Math.Abs(normal.X) > Math.Abs(normal.Y) && Math.Abs(normal.X) > Math.Abs(normal.Z) ? Math.Sign(normal.X) : 0,
            Math.Abs(normal.Y) > Math.Abs(normal.X) && Math.Abs(normal.Y) > Math.Abs(normal.Z) ? Math.Sign(normal.Y) : 0,
            Math.Abs(normal.Z) > Math.Abs(normal.X) && Math.Abs(normal.Z) > Math.Abs(normal.Y) ? Math.Sign(normal.Z) : 0
        );
        
        // Calculate the position to place the new block
        var newBlockPos = SelectedBlock.Position + quantizedNormal;
        
        // Determine the chunk for the new block position
        int chunkX = (int)Math.Floor((float)newBlockPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)newBlockPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)newBlockPos.Z / ChunkSize) * ChunkSize;
        
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);
        
        Chunks.TryGetValue(chunkCoord, out var chunk);
        
        // There is no chunk to build in
        if (chunk == null)
            return;
        
        // Calculate local block coordinates within the chunk
        var localPos = newBlockPos - chunkCoord;
        var localX = (int)localPos.X;
        var localY = (int)localPos.Y;
        var localZ = (int)localPos.Z;
        
        // Check if the position is within bounds and not occupied
        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkSize ||
            localZ < 0 || localZ >= ChunkSize ||
            chunk.Blocks[localX, localY, localZ]?.Info.Type != BlockType.Air)
            return;
        
        // Place the new block
        var oldBlock = chunk.Blocks[localX, localY, localZ];
        chunk.Blocks[localX, localY, localZ] = new Block(newBlockPos, Block.Prefabs[blockType]);
        chunk.Info.Modified = true;
        
        // Update lighting for the placed block
        Lighting.Generate(chunk);
        
        // Regenerate mesh
        Raylib.UnloadModel(chunk.Model); // Unload old model
        
        var meshData = TMesh.GenerateMeshData(chunk);
        chunk.Model = TMesh.UploadMesh(meshData);
        
        TMesh.RemeshNeighborsIfBlockOnEdge(chunk, newBlockPos);
        
        // Add to modified chunks for saving
        if (WorldSave.Data.ModifiedChunks.All(c => c.Position != chunk.Position))
            WorldSave.Data.ModifiedChunks.Add(chunk);
        
        // Play place sound
        var sound = Block.Sounds[blockType].RND;
        
        if (sound.FrameCount != 0)
            Raylib.PlaySound(sound);
    }
}
using Microsoft.Xna.Framework;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Gameplay.Terrain;

public class TerrainGameplay
{
    public void Update(Systems.Camera3D camera)
    {
        var (block, normal) = selectBlock(camera);
        SelectedBlock = block;
    }
    
    private (Block, Vector3Int) selectBlock(Systems.Camera3D camera)
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
    
        Block hitBlock = new Block();
        Vector3Int hitNormal = Vector3Int.Zero;
    
        // Check starting voxel first
        var startChunkPos = new Vector3Int(
            (int)Math.Floor((float)mapPos.X / ChunkSize) * ChunkSize,
            (int)Math.Floor((float)mapPos.Y / ChunkSize) * ChunkSize,
            (int)Math.Floor((float)mapPos.Z / ChunkSize) * ChunkSize
        );
        
        if (ECSChunks.TryGetValue(startChunkPos, out var startChunk))
        {
            var localX = (mapPos.X - startChunkPos.X);
            var localY = (mapPos.Y - startChunkPos.X);
            var localZ = (mapPos.Z - startChunkPos.Z);
            
            if (localX >= 0 && localX < ChunkSize && localY >= 0 && localY < ChunkSize && localZ >= 0 && localZ < ChunkSize)
            {
                var block = startChunk.GetBlock(localX, localY, localZ);
                
                if (!block.IsAir)
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
    
            // Set normal
            SelectedNormal = QuantizedNormal(hitNormal);
            
            // Check current voxel
            var currentChunkPos = new Vector3Int(
                (int)Math.Floor((float)mapPos.X / ChunkSize) * ChunkSize,
                (int)Math.Floor((float)mapPos.Y / ChunkSize) * ChunkSize,
                (int)Math.Floor((float)mapPos.Z / ChunkSize) * ChunkSize
            );
    
            if (!ECSChunks.TryGetValue(currentChunkPos, out var chunk)) continue;
    
            var localX = (mapPos.X - currentChunkPos.X);
            var localY = (mapPos.Y - currentChunkPos.Y);
            var localZ = (mapPos.Z - currentChunkPos.Z);
    
            if (localX < 0 || localX >= ChunkSize || localY < 0 || localY >= ChunkSize || localZ < 0 || localZ >= ChunkSize) continue;
    
            var block = chunk.GetBlock(localX, localY, localZ);
    
            if (!block.IsAir)
            {
                hitBlock = block;
                break;
            }
        }
    
        return (hitBlock, hitNormal);
    }
    
    public void BreakBlock()
    {
        if (SelectedBlock.IsAir)
            return;
        
        // Get the chunk containing the selected block
        var blockPos = SelectedBlock.Position;
        
        int chunkX = (int)Math.Floor((float)blockPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)blockPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)blockPos.Z / ChunkSize) * ChunkSize;
        
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!ECSChunks.TryGetValue(chunkCoord, out var chunk))
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
        var oldBlock = chunk.GetBlock(localX, localY, localZ);

        if (oldBlock.Info.Hardness != 10)
        {
            // Maintain GeneratedInsideIsland for lighting checks.
            var generatedInsideIsland = oldBlock.GeneratedInsideIsland;

            var newBlock = new Block(blockPos, BlockType.Air);
            newBlock.GeneratedInsideIsland = generatedInsideIsland;
            
            chunk.SetBlock(localX, localY, localZ, newBlock);

            chunk.GenerationState = ChunkGenerationState.Modified;
            chunk.IsModified = true;

            // Update lighting if the broken block was opaque or emissive
            Lighting.Generate(chunk);
            
            // Regenerate mesh
            //TMesh.OpaqueModels[chunkCoord].Dispose(); // Unload old model
            //TMesh.TransparentModels[chunkCoord].Dispose(); // Unload old tModel
        
            var meshData = TMesh.GenerateMeshData(chunk, false);
            var tMeshData = TMesh.GenerateMeshData(chunk, true);
            
            TMesh.OpaqueModels[chunkCoord] = TMesh.UploadMesh(meshData);
            TMesh.TransparentModels[chunkCoord] = TMesh.UploadMesh(tMeshData);
            
            TMesh.RemeshNeighbors(chunk, false);
            TMesh.RemeshNeighbors(chunk, true);
        
            // Add to modified chunks for saving
            if (WorldSave.Data.ModifiedChunks.All(c => c.Key != chunk.Position))
                WorldSave.Data.ModifiedChunks.Add(chunk.Position, chunk);

            // Play break sound
            //var sound = BlockData.Sounds[SelectedBlock.Type].RND;
        
            //if (sound != null)
            //    sound.Play();
        }
    }
    
    public void PlaceBlock(PlayerCharacter player, BlockType blockType)
    {
        if (SelectedBlock.IsAir)
            return;

        // Quantize the normal to the nearest axis-aligned direction
        Vector3Int normal = SelectedNormal;
        
        // Calculate the position to place the new block
        var newBlockPos = SelectedBlock.Position + normal;
        
        // Determine the chunk for the new block position
        int chunkX = (int)Math.Floor((float)newBlockPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)newBlockPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)newBlockPos.Z / ChunkSize) * ChunkSize;
        
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);
        
        ECSChunks.TryGetValue(chunkCoord, out var chunk);
        
        // There is no chunk to build in
        if (chunk == null)
            return;
        
        // Calculate local block coordinates within the chunk
        var localPos = newBlockPos - chunkCoord;
        var localX = localPos.X;
        var localY = localPos.Y;
        var localZ = localPos.Z;
        
        // Check if the position is within bounds and not occupied
        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkSize ||
            localZ < 0 || localZ >= ChunkSize ||
            chunk.GetBlock(localX, localY, localZ).Type != BlockType.Air)
            return;

        var newBlockBoundingBox = getBlockBB(newBlockPos.ToVector3());

        // Don't place if collides with player
        // TODO: Update to monogame
        //if (Raylib.CheckCollisionBoxes(player.CollisionBox, newBlockBoundingBox))
        //    return;
        
        // Place the new block
        var generatedInsideIsland = chunk.GetBlock(localX, localY, localZ).GeneratedInsideIsland;

        var newBlock = new Block(newBlockPos, blockType);
        newBlock.GeneratedInsideIsland = generatedInsideIsland;
            
        chunk.SetBlock(localX, localY, localZ, newBlock);

        chunk.GenerationState = ChunkGenerationState.Modified;
        chunk.IsModified = true;
        
        // Update lighting for the placed block
        Lighting.Generate(chunk);
        
        // Regenerate mesh
        //TMesh.OpaqueModels[chunkCoord].Dispose(); // Unload old model
        //TMesh.TransparentModels[chunkCoord].Dispose(); // Unload old tModel
        
        var meshData = TMesh.GenerateMeshData(chunk, false);
        var tMeshData = TMesh.GenerateMeshData(chunk, true, GameScene.PlayerCharacter.Camera.Position);
            
        TMesh.OpaqueModels[chunkCoord] = TMesh.UploadMesh(meshData);
        TMesh.TransparentModels[chunkCoord] = TMesh.UploadMesh(tMeshData);
            
        TMesh.RemeshNeighbors(chunk, false);
        TMesh.RemeshNeighbors(chunk, true);
        
        // Add to modified chunks for saving
        if (WorldSave.Data.ModifiedChunks.All(c => c.Key != chunk.Position))
            WorldSave.Data.ModifiedChunks.Add(chunk.Position, chunk);
        
        // Play place sound
        //var sound = BlockData.Sounds[blockType].RND;
        
        //if (sound != null)
        //    sound.Play();
    }

    public Vector3Int QuantizedNormal(Vector3Int normal)
    {
        return new Vector3Int(
            Math.Abs(normal.X) > Math.Abs(normal.Y) && Math.Abs(normal.X) > Math.Abs(normal.Z) ? Math.Sign(normal.X) : 0,
            Math.Abs(normal.Y) > Math.Abs(normal.X) && Math.Abs(normal.Y) > Math.Abs(normal.Z) ? Math.Sign(normal.Y) : 0,
            Math.Abs(normal.Z) > Math.Abs(normal.X) && Math.Abs(normal.Z) > Math.Abs(normal.Y) ? Math.Sign(normal.Z) : 0
        );
    }
    
    private Microsoft.Xna.Framework.BoundingBox getBlockBB(Vector3 position)
    {
        Vector3 min = new Vector3(
            position.X - 0.5f,
            position.Y - 0.5f,
            position.Z - 0.5f
        );
        Vector3 max = new Vector3(
            position.X + 0.5f,
            position.Y + 0.5f,
            position.Z + 0.5f
        );
        
        return new Microsoft.Xna.Framework.BoundingBox(min + new Vector3(0.5f), max + new Vector3(0.5f));
    }
}
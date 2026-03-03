using Microsoft.Xna.Framework;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Gameplay.Terrain;

public class TerrainGameplay
{
    public static Block SolidBlockBelowPlayer;  // Only updated when non air is below player
    public static Block BlockBelowPlayer;       // Updated with either air or solid is below player
    public static Block BlockAtPlayersFeet;     // Updated with air block at player's feet
    
    public static Block[,,] BlocksAroundPlayer;
    public static Vector3Int BlocksAroundPlayerOrigin { get; private set; } = Vector3Int.Zero;

    public static bool BlocksAroundDebug = false;
    
    private static int BlocksAroundPlayerRadius { get; set; } = -1;
    
    private AudioPlayer breakPlacePlayer = new();

    private float blockAroundTimer = 0f;
    private const float blockAroundInterval = 0.25f; // 250ms
    
    public void Update(Camera3D camera)
    {
        var (block, normal) = selectBlock(camera);
        SelectedBlock = block;

        blockAroundTimer += Time.DeltaTime;

        if (blockAroundTimer > blockAroundInterval)
        {
            GetBlocksAroundPlayer3D(PlayerManager.Current, 8);
            blockAroundTimer = 0f;
        }
        
        /*var solidBelowBlock = GetBlockBelowPlayer(PlayerManager.Current, true, false);
        var blockBelowBlock = GetBlockBelowPlayer(PlayerManager.Current, false, false);
        var blockAtFeetBlock = GetBlockBelowPlayer(PlayerManager.Current, false, true);

        if (solidBelowBlock != null)
            SolidBlockBelowPlayer = solidBelowBlock.Value;

        if (blockBelowBlock != null)
            BlockBelowPlayer = blockBelowBlock.Value;

        if (blockAtFeetBlock != null)
            BlockAtPlayersFeet = blockAtFeetBlock.Value;*/
    }

    // TODO: When at pos > 10000, DrawCubeWiresThick flails around wildly.
    // Needs to be depth tested against terrain
    public void Draw()
    {
        if (SelectedBlock.Type != BlockType.Air && GameScene.UIEnabled)
        {
            Primatives3D.DrawCubeWiresThick(
                SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f),
                1f, 1f, 1f, Color.Black, 0.025f);
        }
        
        const float debugBelowBlockSize = 0.25f;

        if (BlocksAroundDebug)
        {
            foreach (var block in BlocksAroundPlayer)
            {
                var blockPos = block.Position;
                
                if (block.Type != BlockType.Air)
                    Debug.DrawBox(blockPos.ToVector3() + new Vector3(1f), Vector3.One, Color.White);
            }
            
            /*Debug.DrawBox(SolidBlockBelowPlayer.Position.ToVector3() + new Vector3(0.5f) +
                          new Vector3(debugBelowBlockSize * 0.5f) +
                          new Vector3(0f, 1f, 0f), new Vector3(debugBelowBlockSize), Color.Red);
        
            Debug.DrawBox(BlockBelowPlayer.Position.ToVector3() + new Vector3(0.5f) +
                          new Vector3(debugBelowBlockSize * 0.5f) +
                          new Vector3(0f, 1f, 0f), new Vector3(debugBelowBlockSize), Color.Green);
        
            Debug.DrawBox(BlockAtPlayersFeet.Position.ToVector3() + new Vector3(0.5f) +
                          new Vector3(debugBelowBlockSize * 0.5f) +
                          new Vector3(0f, 1f, 0f), new Vector3(debugBelowBlockSize), Color.Blue);*/
        }
    }

    // Rendered into the UI buffer.
    public void DrawPlane()
    {
        if (SelectedBlock.Type != BlockType.Air && GameScene.UIEnabled)
        {
            Vector3 center = SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 faceCenter = center + (SelectedNormal.ToVector3() * 0.51f);
            
            var dist = Vector3.Distance(PlayerManager.Current.Position.ToVector3(), faceCenter);
            var smoothStepDist = GMath.Smoothstep(dist * 0.1f);
            var faceSelectionColor = new Color(1f, 1f, 1f, smoothStepDist * 0.35f);

            if (!SelectedBlock.Info.IsBillboard)
                Primatives3D.DrawPlane(faceCenter, new Vector2(0.25f, 0.25f), faceSelectionColor, -SelectedNormal.ToVector3());
        }
    }
    
    public Block[,,] GetBlocksAroundPlayer3D(PlayerCharacter player, int radius)
    {
        if (radius < 0)
        {
            radius = 0;
        }
    
        int size = radius * 2 + 1;
    
        if (BlocksAroundPlayerRadius != radius ||
            BlocksAroundPlayer.GetLength(0) != size ||
            BlocksAroundPlayer.GetLength(1) != size ||
            BlocksAroundPlayer.GetLength(2) != size)
        {
            BlocksAroundPlayer = new Block[size, size, size];
            BlocksAroundPlayerRadius = radius;
        }
    
        var center = new Vector3Int(
            (int)MathF.Floor((float)player.Position.X),
            (int)MathF.Floor((float)player.Position.Y),
            (int)MathF.Floor((float)player.Position.Z)
        );
    
        BlocksAroundPlayerOrigin = new Vector3Int(center.X - radius, center.Y - radius, center.Z - radius);
    
        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int ix = dx + radius; // [0..size-1]
                    int iy = dy + radius;
                    int iz = dz + radius;
    
                    var worldPos = new Vector3Int(center.X + dx, center.Y + dy, center.Z + dz);
    
                    int chunkX = (int)MathF.Floor((float)worldPos.X / ChunkSize) * ChunkSize;
                    int chunkY = (int)MathF.Floor((float)worldPos.Y / ChunkSize) * ChunkSize;
                    int chunkZ = (int)MathF.Floor((float)worldPos.Z / ChunkSize) * ChunkSize;
    
                    var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);
    
                    if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
                    {
                        //BlocksAroundPlayer[ix, iy, iz] = null; // chunk not loaded
                        continue;
                    }
    
                    int localX = worldPos.X - chunkX;
                    int localY = worldPos.Y - chunkY;
                    int localZ = worldPos.Z - chunkZ;
    
                    BlocksAroundPlayer[ix, iy, iz] = chunk.GetBlock(localX, localY, localZ);
                }
            }
        }
    
        return BlocksAroundPlayer;
    }
    
    public Block? GetBlockBelowPlayer(PlayerCharacter player, bool ignoreAir, bool atFeet)
    {
        var floored = new Vector3Int(
            (int)MathF.Floor((float)player.Position.X),
            (int)MathF.Floor((float)player.Position.Y),
            (int)MathF.Floor((float)player.Position.Z)
        );

        // Block directly below (always Y - 1!)
        Vector3Int below;
        
        if (atFeet)
            below = new Vector3Int(floored.X, floored.Y, floored.Z);
        else // Below feet
            below = new Vector3Int(floored.X, floored.Y - 1, floored.Z);

        int chunkX = (int)MathF.Floor((float)below.X / ChunkSize) * ChunkSize;
        int chunkY = (int)MathF.Floor((float)below.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)MathF.Floor((float)below.Z / ChunkSize) * ChunkSize;

        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            return null;

        int localX = below.X - chunkX;
        int localY = below.Y - chunkY;
        int localZ = below.Z - chunkZ;

        var block = chunk.GetBlock(localX, localY, localZ);

        if (block.Type == BlockType.Air && ignoreAir)
            return null;
        
        return block;
    }
    
    private (Block, Vector3Int) selectBlock(Camera3D camera)
    {
        var rayPosition = camera.Position.ToVector3();
        var rayDirection = Vector3.Normalize(camera.Target - camera.Position.ToVector3());
    
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
        
        if (ChunkBuffer.TryGetValue(startChunkPos, out var startChunk))
        {
            var localX = (mapPos.X - startChunkPos.X);
            var localY = (mapPos.Y - startChunkPos.X);
            var localZ = (mapPos.Z - startChunkPos.Z);
            
            if (localX >= 0 && localX < ChunkSize && localY >= 0 && localY < ChunkSize && localZ >= 0 && localZ < ChunkSize)
            {
                var block = startChunk.GetBlock(localX, localY, localZ);
                
                if (block.Type != BlockType.Air)
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
    
            if (!ChunkBuffer.TryGetValue(currentChunkPos, out var chunk)) continue;
    
            var localX = (mapPos.X - currentChunkPos.X);
            var localY = (mapPos.Y - currentChunkPos.Y);
            var localZ = (mapPos.Z - currentChunkPos.Z);
    
            if (localX < 0 || localX >= ChunkSize || localY < 0 || localY >= ChunkSize || localZ < 0 || localZ >= ChunkSize) continue;
    
            var block = chunk.GetBlock(localX, localY, localZ);
    
            if (block.Type != BlockType.Air)
            {
                hitBlock = block;
                break;
            }
        }
    
        return (hitBlock, hitNormal);
    }
    
    public void BreakBlock()
    {
        if (SelectedBlock.Type == BlockType.Air)
            return;
        
        // Get the chunk containing the selected block
        var blockPos = SelectedBlock.Position;
        
        int chunkX = (int)Math.Floor((float)blockPos.X / ChunkSize) * ChunkSize;
        int chunkY = (int)Math.Floor((float)blockPos.Y / ChunkSize) * ChunkSize;
        int chunkZ = (int)Math.Floor((float)blockPos.Z / ChunkSize) * ChunkSize;
        
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
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
            var newBlock = new Block(blockPos, BlockType.Air);

            newBlock.Biome = oldBlock.Biome; // Preserve biome always!
            
            chunk.SetBlock(localX, localY, localZ, newBlock);
            chunk.IsModified = true;

            // Update lighting if the broken block was opaque or emissive
            //Lighting.PropagateLight(chunk);
            
            // Regenerate mesh
            var meshData = Mesh.MeshDataGen.Generate(chunk, false);
            var tMeshData = Mesh.MeshDataGen.Generate(chunk, true);
            
            Mesh.OpaqueModels[chunkCoord] = Mesh.UploadMesh(meshData);
            Mesh.TransparentModels[chunkCoord] = Mesh.UploadMesh(tMeshData);
        
            // Regenerate neighboring mesh
            Mesh.RemeshBorderingChunks(chunkCoord, localPos);
            
            // Add to modified chunks for saving
            if (WorldSave.Data.ModifiedChunks.All(c => c.Key != chunk.Position))
                WorldSave.Data.ModifiedChunks.Add(chunk.Position, chunk);

            // Play break sound
            var sound = BlockData.Sounds[SelectedBlock.Type].RND;
        
            if (!sound.IsDisposed)
                breakPlacePlayer.Play(sound, blockPos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f));
                //AudioPlayer.CreateAndPlay(sound, blockPos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f));
                
            // Add block to inventory
            if (PlayerManager.Current.IsSurvival)
                GameScene.Inventory.PlayerInventory.AddBlock(oldBlock.Type);
        }
    }
    
    public void PlaceBlock(PlayerCharacter player, BlockType blockType)
    {
        if (SelectedBlock.Type == BlockType.Air || blockType == BlockType.Air)
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
        
        ChunkBuffer.TryGetValue(chunkCoord, out var chunk);
        
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
        if (!player.FreeCamEnabled && newBlockBoundingBox.Intersects(player.CollisionBox))
            return;
        
        // Place the new block
        var biome = chunk.GetBiomeAt(localX, localY, localZ);
        var newBlock = new Block(newBlockPos, blockType);

        newBlock.Biome = biome; // Preserve biome always!
        
        chunk.SetBlock(localX, localY, localZ, newBlock);

        chunk.IsModified = true;
        
        // Update lighting for the placed block
        //Lighting.PropagateLight(chunk);
        
        // Regenerate mesh
        var meshData = Mesh.MeshDataGen.Generate(chunk, false);
        var tMeshData = Mesh.MeshDataGen.Generate(chunk, true);
            
        Mesh.OpaqueModels[chunkCoord] = Mesh.UploadMesh(meshData);
        Mesh.TransparentModels[chunkCoord] = Mesh.UploadMesh(tMeshData);
        
        // Regenerate neighboring mesh
        Mesh.RemeshBorderingChunks(chunkCoord, localPos);
        
        // Add to modified chunks for saving
        if (WorldSave.Data.ModifiedChunks.All(c => c.Key != chunk.Position))
            WorldSave.Data.ModifiedChunks.Add(chunk.Position, chunk);
        
        // Play place sound
        var sound = BlockData.Sounds[blockType].RND;

        if (!sound.IsDisposed)
            breakPlacePlayer.Play(sound, newBlockPos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f));
            //AudioPlayer.CreateAndPlay(sound, newBlockPos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f));
        
        // Decrement stack amount
        if (PlayerManager.Current.IsSurvival)
            GameScene.Inventory.PlayerInventory.DecrementHeldStack();
    }

    public Vector3Int QuantizedNormal(Vector3Int normal)
    {
        return new Vector3Int(
            Math.Abs(normal.X) > Math.Abs(normal.Y) && Math.Abs(normal.X) > Math.Abs(normal.Z) ? Math.Sign(normal.X) : 0,
            Math.Abs(normal.Y) > Math.Abs(normal.X) && Math.Abs(normal.Y) > Math.Abs(normal.Z) ? Math.Sign(normal.Y) : 0,
            Math.Abs(normal.Z) > Math.Abs(normal.X) && Math.Abs(normal.Z) > Math.Abs(normal.Y) ? Math.Sign(normal.Z) : 0
        );
    }
    
    private BoundingBox getBlockBB(Vector3 position)
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
        
        return new BoundingBox(min + new Vector3(0.5f), max + new Vector3(0.5f));
    }
}

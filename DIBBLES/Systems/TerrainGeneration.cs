using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Utils;

namespace DIBBLES.Systems;

public class TerrainGeneration
{
    public const int RenderDistance = 16;
    public const int ChunkSize = 16;
    public const int ChunkHeight = 32;
    public const float ReachDistance = 100f; // Has to be finite!
    
    public static Dictionary<Vector3, Chunk> Chunks = new Dictionary<Vector3, Chunk>();
    
    private readonly TerrainMesh terrainMesh = new TerrainMesh();
    private readonly TerrainLighting lighting = new TerrainLighting();
    private readonly FastNoiseLite noise = new FastNoiseLite();
    private Vector3 lastCameraChunk = Vector3.One; // Needs to != zero for first gen

    public int Seed;
    
    private Vector3 selectedNormal;
    private Block? selectedBlock;
    
    public void Start()
    {
        Block.InitializeBlockPrefabs();
        
        if (WorldSave.Data.Seed != 0)
            Seed = WorldSave.Data.Seed;
        else
            Seed = new Random().Next(int.MaxValue);
        
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetSeed(1337);
        noise.SetFrequency(0.02f);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(4);
        noise.SetFractalLacunarity(2.0f);
        noise.SetFractalGain(0.5f);
    }

    public void UpdateTerrain(Camera3D camera)
    {
        // Calculate current chunk coordinates based on camera position
        var currentChunk = new Vector3(
            (int)Math.Floor(camera.Position.X / ChunkSize),
            0f,
            (int)Math.Floor(camera.Position.Z / ChunkSize)
        );
        
        // Only update if the camera has moved to a new chunk
        if (currentChunk != lastCameraChunk)
        {
            lastCameraChunk = currentChunk;
            GenerateTerrain(currentChunk);
            UnloadDistantChunks(currentChunk);
        }

    }
    
    public void UpdateMovement(Camera3D camera)
    {
        var (block, normal) = selectBlock(camera);
        selectedBlock = block;
        selectedNormal = normal;
    }
    
    private void GenerateTerrain(Vector3 centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3> chunksToGenerate = new List<Vector3>();
        
        for (int cx = (int)centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        {
            for (int cz = (int)centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
            {
                Vector3 chunkPos = new Vector3(cx * ChunkSize, 0f, cz * ChunkSize);
                
                if (!Chunks.ContainsKey(chunkPos))
                {
                    chunksToGenerate.Add(chunkPos);
                }
            }
        }
    
        foreach (var pos in chunksToGenerate)
        {
            var chunk = new Chunk(pos);
            generateChunkData(chunk);
            lighting.GenerateLighting(chunk);
            
            Chunks[pos] = chunk;
        
            chunk.Model = terrainMesh.GenerateChunkMesh(chunk);
        }
    }
    
    private void generateChunkData(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                float height = noise.GetNoise(chunk.Position.X + x, chunk.Position.Z + z) * 0.5f + 0.5f;
                int terrainHeight = (int)(height * (ChunkHeight - 10)) + 10;

                for (int y = 0; y < ChunkHeight; y++)
                {
                    var worldPos = chunk.Position + new Vector3(x, y, z);
                    
                    if (y >= terrainHeight) // Air
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Air]);
                    }
                    else if (y == terrainHeight - 1) // Top layer: Grass
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Grass]);
                    }
                    else if (y >= terrainHeight - 3) // Near surface: Dirt
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Dirt]);
                    }
                    else // Deeper: Stone
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Stone]);
                    }
                }
            }
        }
    }
    
    private (Block?, Vector3) selectBlock(Camera3D camera)
    {
        var rayPosition = camera.Position;
        var rayDirection = Vector3.Normalize(camera.Target - camera.Position);
    
        var mapPos = new Vector3(
            MathF.Floor(rayPosition.X),
            MathF.Floor(rayPosition.Y),
            MathF.Floor(rayPosition.Z)
        );
    
        // Handle near-zero components to avoid div-by-zero and precision issues
        const float epsilon = 1e-6f;
        
        var deltaDist = new Vector3(
            Math.Abs(rayDirection.X) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.X),
            Math.Abs(rayDirection.Y) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.Y),
            Math.Abs(rayDirection.Z) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.Z)
        );
    
        var step = new Vector3(
            rayDirection.X > 0 ? 1f : (rayDirection.X < 0 ? -1f : 0f),
            rayDirection.Y > 0 ? 1f : (rayDirection.Y < 0 ? -1f : 0f),
            rayDirection.Z > 0 ? 1f : (rayDirection.Z < 0 ? -1f : 0f)
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
        Vector3 hitNormal = Vector3.Zero;
    
        // Check starting voxel first
        var startChunkPos = new Vector3(
            MathF.Floor(mapPos.X / ChunkSize) * ChunkSize,
            0f,
            MathF.Floor(mapPos.Z / ChunkSize) * ChunkSize
        );
        
        if (Chunks.TryGetValue(startChunkPos, out var startChunk))
        {
            var localX = (int)(mapPos.X - startChunkPos.X);
            var localY = (int)mapPos.Y;
            var localZ = (int)(mapPos.Z - startChunkPos.Z);
            
            if (localX >= 0 && localX < ChunkSize && localY >= 0 && localY < ChunkHeight && localZ >= 0 && localZ < ChunkSize)
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
            Vector3 stepAxis = Vector3.Zero;
    
            if (sideDist.X <= sideDist.Y && sideDist.X <= sideDist.Z)
            {
                nextT = sideDist.X;
                sideDist.X += deltaDist.X;
                mapPos.X += step.X;
                hitNormal = new Vector3(-step.X, 0, 0);
            }
            else if (sideDist.Y <= sideDist.Z)
            {
                nextT = sideDist.Y;
                sideDist.Y += deltaDist.Y;
                mapPos.Y += step.Y;
                hitNormal = new Vector3(0, -step.Y, 0);
            }
            else
            {
                nextT = sideDist.Z;
                sideDist.Z += deltaDist.Z;
                mapPos.Z += step.Z;
                hitNormal = new Vector3(0, 0, -step.Z);
            }
    
            if (nextT > ReachDistance) break;
    
            // Check current voxel
            var currentChunkPos = new Vector3(
                MathF.Floor(mapPos.X / ChunkSize) * ChunkSize,
                0f,
                MathF.Floor(mapPos.Z / ChunkSize) * ChunkSize
            );
    
            if (!Chunks.TryGetValue(currentChunkPos, out var chunk)) continue;
    
            var localX = (int)(mapPos.X - currentChunkPos.X);
            var localY = (int)mapPos.Y;
            var localZ = (int)(mapPos.Z - currentChunkPos.Z);
    
            if (localX < 0 || localX >= ChunkSize || localY < 0 || localY >= ChunkHeight || localZ < 0 || localZ >= ChunkSize) continue;
    
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
        if (selectedBlock == null)
            return;

        // Get the chunk containing the selected block
        var blockPos = selectedBlock.Position;
        var chunkCoord = new Vector3(
            (int)Math.Floor(blockPos.X / ChunkSize) * ChunkSize,
            0f,
            (int)Math.Floor(blockPos.Z / ChunkSize) * ChunkSize
        );

        if (!Chunks.TryGetValue(chunkCoord, out var chunk))
            return;

        // Calculate local block coordinates within the chunk
        var localPos = blockPos - chunkCoord;
        var localX = (int)localPos.X;
        var localY = (int)localPos.Y;
        var localZ = (int)localPos.Z;

        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkHeight ||
            localZ < 0 || localZ >= ChunkSize)
            return;

        // Set block to Air
        var oldBlock = chunk.Blocks[localX, localY, localZ];
        chunk.Blocks[localX, localY, localZ] = new Block(blockPos, Block.Prefabs[BlockType.Air]);
        chunk.Info.Modified = true;

        // Update lighting if the broken block was opaque or emissive
        updateLightingForBlockChange(chunk, localX, localY, localZ, oldBlock, chunk.Blocks[localX, localY, localZ]);
        
        // Regenerate mesh
        Raylib.UnloadModel(chunk.Model); // Unload old model
        chunk.Model = terrainMesh.GenerateChunkMesh(chunk);

        // Add to modified chunks for saving
        if (!WorldSave.Data.ModifiedChunks.Any(c => c.Position == chunk.Position))
            WorldSave.Data.ModifiedChunks.Add(chunk);

        // Play break sound
        var sound = Block.Sounds[selectedBlock.Info.Type].RND;
        
        if (sound.FrameCount != 0)
            Raylib.PlaySound(sound);
    }
    
    public void PlaceBlock(BlockType blockType)
    {
        if (selectedBlock == null || blockType == BlockType.Air)
            return;

        // Quantize the normal to the nearest axis-aligned direction
        Vector3 normal = selectedNormal;
        
        Vector3 quantizedNormal = new Vector3(
            Math.Abs(normal.X) > Math.Abs(normal.Y) && Math.Abs(normal.X) > Math.Abs(normal.Z) ? Math.Sign(normal.X) : 0,
            Math.Abs(normal.Y) > Math.Abs(normal.X) && Math.Abs(normal.Y) > Math.Abs(normal.Z) ? Math.Sign(normal.Y) : 0,
            Math.Abs(normal.Z) > Math.Abs(normal.X) && Math.Abs(normal.Z) > Math.Abs(normal.Y) ? Math.Sign(normal.Z) : 0
        );
        
        // Calculate the position to place the new block
        var newBlockPos = selectedBlock.Position + quantizedNormal;
        
        // Determine the chunk for the new block position
        var chunkCoord = new Vector3(
            (int)Math.Floor(newBlockPos.X / ChunkSize) * ChunkSize,
            0f,
            (int)Math.Floor(newBlockPos.Z / ChunkSize) * ChunkSize
        );
        
        // Create chunk if it doesn't exist
        if (!Chunks.TryGetValue(chunkCoord, out var chunk))
        {
            chunk = new Chunk(chunkCoord);
            generateChunkData(chunk);
            lighting.GenerateLighting(chunk);
            Chunks[chunkCoord] = chunk;
        }
        
        // Calculate local block coordinates within the chunk
        var localPos = newBlockPos - chunkCoord;
        var localX = (int)localPos.X;
        var localY = (int)localPos.Y;
        var localZ = (int)localPos.Z;
        
        // Check if the position is within bounds and not occupied
        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkHeight ||
            localZ < 0 || localZ >= ChunkSize ||
            chunk.Blocks[localX, localY, localZ]?.Info.Type != BlockType.Air)
            return;
        
        // Place the new block
        var oldBlock = chunk.Blocks[localX, localY, localZ];
        chunk.Blocks[localX, localY, localZ] = new Block(newBlockPos, Block.Prefabs[blockType]);
        chunk.Info.Modified = true;
        
        // Update lighting for the placed block
        updateLightingForBlockChange(chunk, localX, localY, localZ, oldBlock, chunk.Blocks[localX, localY, localZ]);
        
        // Regenerate mesh
        Raylib.UnloadModel(chunk.Model); // Unload old model
        chunk.Model = terrainMesh.GenerateChunkMesh(chunk);
        
        // Add to modified chunks for saving
        if (!WorldSave.Data.ModifiedChunks.Any(c => c.Position == chunk.Position))
            WorldSave.Data.ModifiedChunks.Add(chunk);
        
        // Play place sound
        var sound = Block.Sounds[selectedBlock.Info.Type].RND;
        
        if (sound.FrameCount != 0)
            Raylib.PlaySound(sound);
    }
    
    private void updateLightingForBlockChange(Chunk chunk, int x, int y, int z, Block oldBlock, Block newBlock)
    {
        // For now, do full relighting of the chunk when blocks change
        // This is simpler but less efficient than proper local relighting
        lighting.GenerateLighting(chunk);
    }
    
    private void UnloadDistantChunks(Vector3 centerChunk)
    {
        List<Vector3> chunksToRemove = new List<Vector3>();

        foreach (var chunk in Chunks)
        {
            // Convert world-space key to chunk coordinates
            int chunkX = (int)Math.Floor(chunk.Key.X / ChunkSize);
            int chunkZ = (int)Math.Floor(chunk.Key.Z / ChunkSize);
            int centerX = (int)centerChunk.X;
            int centerZ = (int)centerChunk.Z;

            int dx = Math.Abs(chunkX - centerX);
            int dz = Math.Abs(chunkZ - centerZ);
        
            if (dx > RenderDistance / 2 || dz > RenderDistance / 2)
            {
                chunksToRemove.Add(chunk.Key);
            }
        }

        foreach (var coord in chunksToRemove)
        {
            Raylib.UnloadModel(Chunks[coord].Model); // Unload the model to free memory
            Chunks.Remove(coord);
        }
    }
    
    public void Draw()
    {
        foreach (var chunk in Chunks.Values)
        {
            Raylib.DrawModel(chunk.Model, chunk.Position, 1.0f, Color.White);
            
            var debugColor = Color.Black;
            
            if (chunk.Info.Modified)
                debugColor = Color.Red;
            else
                debugColor = Color.Blue;
            
            if (selectedBlock != null)
                Raylib.DrawCubeWires(selectedBlock.Position + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Black);
            
            //Raylib.DrawCubeWires(chunk.Position + new Vector3(ChunkSize / 2f, ChunkHeight / 2f, ChunkSize / 2f),
            //    ChunkSize, ChunkHeight, ChunkSize, debugColor);
        }
    }
}
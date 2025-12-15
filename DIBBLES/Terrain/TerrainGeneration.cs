using System.Collections.Concurrent;
using System.Net;
using DIBBLES.Effects;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain.Features;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Terrain;

public class TerrainGeneration
{
    public const int RenderDistance = 12;
    public const int ChunkSize = 16;
    public const float ReachDistance = 5f; // Has to be finite!
    
    public static int Seed = -1888248476;
    public static readonly ConcurrentDictionary<Vector3Int, Chunk> ChunkBuffer = new();
    
    // Generation engine
    public static TerrainMesh Mesh = new();
    public static TerrainLighting Lighting = new();
    
    // Features
    public TerrainIsland Islands = new();
    public TerrainSurface Surface = new();
    public TerrainDecorations Decorations = new();
    
    // Gameplay
    public static TerrainGameplay Gameplay = new();
    public TerrainCommands Commands = new();
    
    public static Effect terrainShader;
    public static bool InitialLoadDone = false;
    
    public static Block SelectedBlock;
    public static Vector3Int SelectedNormal;

    private ChunkGenerationStage terrainGenerationStage = ChunkGenerationStage.Uninitialized;
    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen
    private int chunksLoaded = 0;
    
    public void Start()
    {
        BlockData.InitializeBlockPrefabs();
        
        WorldSave.Initialize();
        WorldSave.LoadWorldData("test");
        
        foreach (var kv in WorldSave.Data.ModifiedChunks)
            ChunkBuffer[kv.Key] = kv.Value;
        
        //if (WorldSave.Exists)
        //    Seed = WorldSave.Data.Seed;
        //else
        //    Seed = new Random().Next(Int32.MinValue, int.MaxValue);
        
        terrainShader = Engine.Instance.Content.Load<Effect>("Shaders/Terrain");
        
        Commands.Register();
    }

    public void Update(PlayerCharacter playerCharacter)
    {
        // Calculate current chunk coordinates based on camera position
        var centerChunk = new Vector3Int(
            (int)Math.Floor(playerCharacter.Position.X / ChunkSize),
            (int)Math.Floor(playerCharacter.Position.Y / ChunkSize),
            (int)Math.Floor(playerCharacter.Position.Z / ChunkSize)
        );
        
        // Only update if the camera has moved to a new chunk
        if (centerChunk != lastCameraChunk)
        {
            lastCameraChunk = centerChunk;
            chunksLoaded = 0;

            // Start chunk staging
            terrainGenerationThreaded(centerChunk, true);
        }

        updateStageIfReady(centerChunk);
        Debug.Draw2DText($"TerrainGenerationStage: {terrainGenerationStage}", Color.Azure);
        
        float expectedChunkCount = (RenderDistance + 1f) * (RenderDistance + 1f) * (RenderDistance + 1f);
        
        // TODO: Sometimes doesn't run???
        // After all chunk data in render distance has loaded in
        if (chunksLoaded >= expectedChunkCount && !InitialLoadDone)
        {
            playerCharacter.ShouldUpdate = true;
            playerCharacter.IsFrozen = false;
            playerCharacter.FreeCamEnabled = false;
            InitialLoadDone = true;
        }
        
        // Try to upload any queued meshes (must be done on main thread)
        // Opaque pass
        while (Mesh.MeshUploadQueue.TryDequeue(out var entry))
        {
            var chunkPos = entry.chunkPos;
            var meshData = entry.meshData;
            
            // Upload mesh on main thread
            Mesh.OpaqueModels[chunkPos] = Mesh.UploadMesh(meshData);
        }
        
        // Transparent pass
        while (Mesh.TMeshUploadQueue.TryDequeue(out var entry))
        {
            var chunkPos = entry.chunkPos;
            var meshData = entry.meshData;
            
            // Upload mesh on main thread
            Mesh.TransparentModels[chunkPos] = Mesh.UploadMesh(meshData);
            chunksLoaded++;
        }
        
        unloadDistantChunks(centerChunk);
    }

    private void updateStageIfReady(Vector3Int centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        bool allReady = true;

        for (int cx = centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3Int chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
            
            if (ChunkBuffer.TryGetValue(chunkPos, out var chunk))
            {
                if (chunk.GenerationStage <= terrainGenerationStage)
                {
                    allReady = false;
                    break;
                }
            }
            else
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            if (terrainGenerationStage < ChunkGenerationStage.Meshing)
            {
                terrainGenerationStage++;
                terrainGenerationThreaded(centerChunk);
            }
        }
    }
    
    private SemaphoreSlim semaphore = new(4); // Max 4 concurrent tasks
    
    private void terrainGenerationThreaded(Vector3Int centerChunk, bool addAfterInitial = false)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3Int> chunksToGenerate = new();
    
        for (int cx = centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3Int chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
    
            if (ChunkBuffer.TryGetValue(chunkPos, out var chunk))
            {
                // Process any chunk that needs to catch up to (or is at) current stage
                if (chunk.GenerationStage <= terrainGenerationStage)
                    chunksToGenerate.Add(chunkPos);
            }
            else
            {
                // New chunk: always add
                chunksToGenerate.Add(chunkPos);
            }
        }
    
        // Sort by distance to centerChunk
        chunksToGenerate.Sort((a, b) => 
            (a - centerChunk * ChunkSize).ToVector3().LengthSquared()
            .CompareTo((b - centerChunk * ChunkSize).ToVector3().LengthSquared())
        );
        
        foreach (var pos in chunksToGenerate)
        {
            ThreadPool.QueueUserWorkItem(x =>
            {
                semaphore.Wait();
                
                try
                {
                    if (!ChunkBuffer.TryGetValue(pos, out var chunk)) // Not in buffer
                    {
                        chunk = new Chunk(pos);
                        ChunkBuffer.TryAdd(pos, chunk);
                        
                        // Add new chunk after init and get its stage up to date
                        if (InitialLoadDone && addAfterInitial)
                        {
                            while (chunk.GenerationStage <= terrainGenerationStage)
                                proccesChunkStage(chunk);
                        }
                        else // Generate initial stage from Uninitialized > Islands (increment only)
                            proccesChunkStage(chunk);
                    }
                    else // In buffer
                    {
                        // Update pre-existing chunk to next stage(s)
                        while (chunk.GenerationStage <= terrainGenerationStage)
                            proccesChunkStage(chunk);
                    }
                }
                finally { semaphore.Release(); }
            });
        }
    }

    private void proccesChunkStage(Chunk chunk)
    {
        switch (chunk.GenerationStage)
        {
            case ChunkGenerationStage.Uninitialized:
            {
                chunk.GenerationStage++;
                break;
            }
            case ChunkGenerationStage.Islands:
            {
                if (!chunk.IsModified)
                    Islands.Generate(chunk);
                
                chunk.GenerationStage++;
                break;
            }
            case ChunkGenerationStage.Surface:
            {
                if (!chunk.IsModified)
                    Surface.Generate(chunk);
                
                chunk.GenerationStage++;
                break;
            }
            case ChunkGenerationStage.Decorations:
            {
                if (!chunk.IsModified)
                    Decorations.Generate(chunk);
                
                chunk.GenerationStage++;
                break;
            }
            case ChunkGenerationStage.Lighting:
            {
                Lighting.Generate(chunk);
                chunk.GenerationStage++;
                break;
            }
            case ChunkGenerationStage.Meshing:
            {
                Mesh.Generate(chunk);
                chunk.GenerationStage++;
                break;
            }
        }
    }
    
    private void unloadDistantChunks(Vector3Int centerChunk)
    {
        List<Vector3Int> chunksToRemove = new List<Vector3Int>();

        foreach (var chunk in ChunkBuffer)
        {
            // Convert world-space key to chunk coordinates
            int chunkX = chunk.Key.X / ChunkSize;
            int chunkY = chunk.Key.Y / ChunkSize;
            int chunkZ = chunk.Key.Z / ChunkSize;
            
            int centerX = centerChunk.X;
            int centerY = centerChunk.Y;
            int centerZ = centerChunk.Z;

            int dx = Math.Abs(chunkX - centerX);
            int dy = Math.Abs(chunkY - centerY);
            int dz = Math.Abs(chunkZ - centerZ);
        
            if (dx > RenderDistance / 2 || dy > RenderDistance / 2 || dz > RenderDistance / 2)
                chunksToRemove.Add(chunk.Key);
        }

        foreach (var coord in chunksToRemove)
        {
            // Opaque model
            if (Mesh.OpaqueModels.TryGetValue(coord, out var oModel) && oModel != null)
            {
                oModel.Dispose();
                Mesh.OpaqueModels.Remove(coord);
            }

            // Transparent model
            if (Mesh.TransparentModels.TryGetValue(coord, out var tModel) &&  tModel != null)
            {
                tModel.Dispose();
                Mesh.TransparentModels.Remove(coord);
            }
            
            if (ChunkBuffer.TryGetValue(coord, out var chunk))
                chunk.GenerationStage = ChunkGenerationStage.Lighting;
        }
    }
    
    public void Draw()
    {
        // Draw every mesh in the mesh queue
        Mesh.DrawAllMeshes();
        
        // Chunk border debug
        if (Debug.ShowChunkDebug)
        {
            foreach (var chunkPair in ChunkBuffer)
            {
                var chunkPos = chunkPair.Key;
                Debug.DrawBox(chunkPos, new Vector3Int(ChunkSize, ChunkSize, ChunkSize), Color.Blue, 16f);
            }
        }
        
        // Light level debug
        if (Debug.ShowLightDebug)
        {
            var playerBlockPos = new Vector3Int(
                (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.X),
                (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.Y),
                (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.Z)
            );

            int radius = 5; // 16 block radius (cube)
            
            for (int x = playerBlockPos.X - radius; x <= playerBlockPos.X + radius; x++)
            for (int y = playerBlockPos.Y - radius; y <= playerBlockPos.Y + radius; y++)
            for (int z = playerBlockPos.Z - radius; z <= playerBlockPos.Z + radius; z++)
            {
                var worldPos = new Vector3Int(x, y, z);
                
                // Only draw for non-air blocks
                var (type, withinLoaded) = Chunk.GetBlockTypeGlobal(worldPos);
                
                if (!withinLoaded)
                    continue;

                // Get light level (0..15) and map to grayscale color
                byte light = Chunk.GetLightLevelGlobal(worldPos);
                float light01 = light / 15f; // 0..1
                
                var lightColor = (byte)(255f * light01);
                
                var color = new Color(lightColor, lightColor, lightColor, (byte)255);

                // Draw a 1x1x1 wire box around the block (centered on block center)
                Vector3 boxCenter = worldPos.ToVector3() + new Vector3(1f, 1f, 1f);
                Debug.DrawBox(boxCenter, Vector3.One, color);
            }
        }
    }
}
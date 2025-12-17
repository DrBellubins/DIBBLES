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

    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen
    
    private readonly HashSet<Vector3Int> activeViewChunks = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> progressViewChunks = new HashSet<Vector3Int>();
    
    // Exposed progress [0..1]
    public float InitialLoadProgress { get; private set; } = 0f;
    
    // Multi-threading/queues
    private SemaphoreSlim semaphore = new(4); // Max 4 concurrent tasks
    
    private readonly object _pqLock = new object();
    private readonly PriorityQueue<(Vector3Int chunkPos, ChunkGenerationStage targetStage), int> taskQueue
        = new PriorityQueue<(Vector3Int, ChunkGenerationStage), int>();
    
    private static readonly ChunkGenerationStage freezeStage = ChunkGenerationStage.Surface;
    
    
    private static Vector3Int[] getNeighborOffsets()
    {
        return new[]
        {
            new Vector3Int( ChunkSize, 0, 0),
            new Vector3Int(-ChunkSize, 0, 0),
            new Vector3Int(0,  ChunkSize, 0),
            new Vector3Int(0, -ChunkSize, 0),
            new Vector3Int(0, 0,  ChunkSize),
            new Vector3Int(0, 0, -ChunkSize),
        };
    }
    
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
            
            rebuildActiveView(centerChunk);

            // Start chunk queue
            QueueChunksInView(centerChunk);
            UnloadAndFreezeDistant(centerChunk);
        }
        
        ProcessTaskQueue();

        Debug.Draw2DText($"Initial load: {InitialLoadProgress * 100f}%", Color.Azure);
        
        if (!InitialLoadDone)
        {
            int total = progressViewChunks.Count;
            int ready = countChunksLit(progressViewChunks);

            InitialLoadProgress = (total > 0) ? (ready / (float)total) : 0f;

            Debug.Draw2DText($"chunks lit: {ready}, total: {total}", Color.Azure);
            
            if (total > 0 && ready == total)
            {
                InitialLoadDone = true;

                playerCharacter.ShouldUpdate = true;
                playerCharacter.FreeCamEnabled = false;
            }
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
        }
    }
    
    private void QueueChunksInView(Vector3Int center)
    {
        int half = RenderDistance / 2;

        for (int cx = center.X - half; cx <= center.X + half; cx++)
        for (int cy = center.Y - half; cy <= center.Y + half; cy++)
        for (int cz = center.Z - half; cz <= center.Z + half; cz++)
        {
            var pos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);

            if (!ChunkBuffer.TryGetValue(pos, out var chunk))
            {
                chunk = new Chunk(pos);
                ChunkBuffer[pos] = chunk;
            }

            if (chunk.IsFrozen)
                chunk.IsFrozen = false;

            EnqueueAdvance(pos, ChunkGenerationStage.Meshing, center);
        }
    }

    private void UnloadAndFreezeDistant(Vector3Int center)
    {
        foreach (var kv in ChunkBuffer)
        {
            var pos = kv.Key;
            int dx = Math.Abs((pos.X / ChunkSize) - center.X);
            int dy = Math.Abs((pos.Y / ChunkSize) - center.Y);
            int dz = Math.Abs((pos.Z / ChunkSize) - center.Z);

            if (dx > RenderDistance / 2 || dy > RenderDistance / 2 || dz > RenderDistance / 2)
            {
                var chunk = kv.Value;

                if (chunk.GenerationStage > freezeStage)
                    chunk.ResetToStage(freezeStage);

                chunk.IsFrozen = true;

                // Dispose meshes (existing logic)
                if (Mesh.OpaqueModels.TryGetValue(pos, out var oModel) && oModel != null)
                {
                    oModel.Dispose();
                    Mesh.OpaqueModels.Remove(pos);
                }
                
                if (Mesh.TransparentModels.TryGetValue(pos, out var tModel) && tModel != null)
                {
                    tModel.Dispose();
                    Mesh.TransparentModels.Remove(pos);
                }
            }
        }
    }
    
    private void EnqueueAdvance(Vector3Int pos, ChunkGenerationStage target, Vector3Int centerChunk)
    {
        int dist2 = (int)Vector3.DistanceSquared(new Vector3(pos.X / (float)ChunkSize,
                pos.Y / (float)ChunkSize, pos.Z / (float)ChunkSize),
                new Vector3(centerChunk.X, centerChunk.Y, centerChunk.Z));

        lock (_pqLock)
            taskQueue.Enqueue((pos, target), dist2);
    }
    
    private void ProcessTaskQueue()
    {
        while (true)
        {
            (Vector3Int pos, ChunkGenerationStage target) workItem;

            lock (_pqLock)
            {
                if (taskQueue.Count == 0)
                    break;

                workItem = taskQueue.Dequeue();
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                semaphore.Wait();

                try
                {
                    if (ChunkBuffer.TryGetValue(workItem.pos, out var chunk) && !chunk.IsFrozen)
                        AdvanceChunk(chunk, workItem.target);
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }
    }
    
    private void AdvanceChunk(Chunk chunk, ChunkGenerationStage target)
    {
        while (chunk.GenerationStage < target && !chunk.IsFrozen)
        {
            var next = chunk.GenerationStage + 1;

            if (DependenciesMet(chunk, next))
            {
                ProcessStage(chunk, next);
                chunk.GenerationStage = next;
            }
            else
            {
                // Requeue to try later; keep target same
                var playerChunk = lastCameraChunk;
                EnqueueAdvance(chunk.Position, target, playerChunk);
                
                return;
            }
        }
    }

    private bool DependenciesMet(Chunk chunk, ChunkGenerationStage stage)
    {
        // Require the above (+Y) neighbor to have at least Islands before doing Surface
        if (stage == ChunkGenerationStage.Surface)
        {
            var abovePos = chunk.Position + new Vector3Int(0, ChunkSize, 0);

            if (ChunkBuffer.TryGetValue(abovePos, out var aboveChunk))
            {
                // Not ready yet
                if (aboveChunk.GenerationStage < ChunkGenerationStage.Islands)
                    return false;
            }
            else
            {
                // Above chunk missing: ensure it gets queued to Islands, and block until it is
                EnqueueAdvance(abovePos, ChunkGenerationStage.Islands, lastCameraChunk);
                return false;
            }
        }

        // Relaxed neighbor requirements for Lighting
        if (stage == ChunkGenerationStage.Lighting)
        {
            foreach (var offset in getNeighborOffsets())
            {
                var nPos = chunk.Position + offset;

                if (ChunkBuffer.TryGetValue(nPos, out var nChunk))
                {
                    // Require neighbor to have at least terrain populated
                    if (nChunk.GenerationStage < ChunkGenerationStage.Decorations)
                        return false;
                }
                else
                {
                    // Bring neighbor up to Decorations so lighting has stable geometry
                    EnqueueAdvance(nPos, ChunkGenerationStage.Decorations, lastCameraChunk);
                    return false;
                }
            }
        }
        else if (stage == ChunkGenerationStage.Meshing)
        {
            foreach (var offset in getNeighborOffsets())
            {
                var nPos = chunk.Position + offset;

                if (ChunkBuffer.TryGetValue(nPos, out var nChunk))
                {
                    // Require neighbors to be lit before meshing to avoid border seams
                    if (nChunk.GenerationStage < ChunkGenerationStage.Lighting)
                        return false;
                }
                else
                {
                    EnqueueAdvance(nPos, ChunkGenerationStage.Lighting, lastCameraChunk);
                    return false;
                }
            }
        }

        return true;
    }
    
    private void ProcessStage(Chunk chunk, ChunkGenerationStage stage)
    {
        switch (stage)
        {
            case ChunkGenerationStage.Islands:
            {
                if (!chunk.IsModified)
                    Islands.Generate(chunk);
                
                break;
            }
            case ChunkGenerationStage.Surface:
            {
                if (!chunk.IsModified)
                    Surface.Generate(chunk);
                
                break;
            }
            case ChunkGenerationStage.Decorations:
            {
                if (!chunk.IsModified)
                    Decorations.Generate(chunk);
                
                break;
            }
            case ChunkGenerationStage.Lighting:
            {
                Lighting.Generate(chunk);

                // After finishing lighting, nudge neighbors to Lighting (if they aren't yet),
                // and optionally nudge both this chunk and neighbors toward Meshing.
                foreach (var offset in getNeighborOffsets())
                {
                    var nPos = chunk.Position + offset;

                    if (ChunkBuffer.TryGetValue(nPos, out var nChunk))
                    {
                        if (nChunk.GenerationStage < ChunkGenerationStage.Lighting)
                            EnqueueAdvance(nPos, ChunkGenerationStage.Lighting, lastCameraChunk);
                    
                        // Optional: if both are lit, ensure meshing gets queued soon
                        if (nChunk.GenerationStage >= ChunkGenerationStage.Lighting)
                            EnqueueAdvance(nPos, ChunkGenerationStage.Meshing, lastCameraChunk);
                    }
                    else
                    {
                        EnqueueAdvance(nPos, ChunkGenerationStage.Lighting, lastCameraChunk);
                    }
                }

                // Also ensure this chunk proceeds to meshing after lighting
                EnqueueAdvance(chunk.Position, ChunkGenerationStage.Meshing, lastCameraChunk);
                break;
            }
            case ChunkGenerationStage.Meshing:
            {
                Mesh.Generate(chunk);
                break;
            }
        }
    }
    
    private void rebuildActiveView(Vector3Int centerChunk)
    {
        activeViewChunks.Clear();
        progressViewChunks.Clear();

        int half = RenderDistance / 2;

        for (int cx = centerChunk.X - half; cx <= centerChunk.X + half; cx++)
        for (int cy = centerChunk.Y - half; cy <= centerChunk.Y + half; cy++)
        for (int cz = centerChunk.Z - half; cz <= centerChunk.Z + half; cz++)
        {
            var pos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
            activeViewChunks.Add(pos);
        }

        // Build inset set: only chunks whose 6 neighbors are also inside the view cube.
        foreach (var pos in activeViewChunks)
        {
            bool allNeighborsInside = true;

            foreach (var off in getNeighborOffsets())
            {
                if (!activeViewChunks.Contains(pos + off))
                {
                    allNeighborsInside = false;
                    break;
                }
            }

            if (allNeighborsInside)
                progressViewChunks.Add(pos);
        }
    }

    private int countChunksLit(HashSet<Vector3Int> set)
    {
        int ready = 0;

        foreach (var pos in set)
        {
            if (ChunkBuffer.TryGetValue(pos, out var chunk) &&
                chunk.GenerationStage >= ChunkGenerationStage.Lighting)
            {
                ready++;
            }
        }

        return ready;
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
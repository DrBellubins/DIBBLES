using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Concurrent;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain.Biomes;
using DIBBLES.Terrain.Features;
using DIBBLES.Utils;
using DIBBLES.Terrain.Meshing;

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
    public static Effect billboardShader;
    
    public static bool InitialLoadDone = false;
    
    public static Block SelectedBlock;
    public static Vector3Int SelectedNormal;

    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen
    
    private readonly HashSet<Vector3Int> activeViewChunks = new();
    
    public float VisualLoadProgress { get; private set; } = 0f;
    
    // Biomes
    public static PlainsBiome plainsBiome = new();
    public static DesertBiome desertBiome = new();
    public static SnowlandsBiome snowlandsBiome = new();
    
    // Multi-threading/queues
    private SemaphoreSlim semaphore = new(4); // Max 4 concurrent tasks
    
    private readonly object _pqLock = new();
    private readonly PriorityQueue<(Vector3Int chunkPos, ChunkGenerationStage targetStage), int> taskQueue = new();
    
    private static readonly ChunkGenerationStage freezeStage = ChunkGenerationStage.Surface;
    
    // Optimization
    private static Vector3Int[] _viewOffsetsSorted = Array.Empty<Vector3Int>();
    private static int _viewOffsetsHalf = -1;
    private readonly Queue<IDisposable> _disposeQueue = new();
    
    public void Start()
    {
        BlockData.InitializeBlockPrefabs();
        
        WorldSave.Initialize();
        WorldSave.LoadWorldData("test");
        
        foreach (var kv in WorldSave.Data.ModifiedChunks)
            ChunkBuffer[kv.Key] = kv.Value;
        
        if (WorldSave.Exists)
            Seed = WorldSave.Data.Seed;
        else
            Seed = new Random().Next(Int32.MinValue, int.MaxValue);
        
        terrainShader = Engine.Instance.Content.Load<Effect>("Shaders/Terrain");
        billboardShader = Engine.Instance.Content.Load<Effect>("Shaders/BillboardInstanced");
        
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
        VisualLoadProgress = computeVisualProgress(activeViewChunks);

        Debug.Draw2DText($"Load progress: {VisualLoadProgress * 100f}%", Color.Azure);
        
        if (!InitialLoadDone)
        {
            // Use the actual render cube (activeViewChunks), not the inset, for progress
            int total = activeViewChunks.Count;
            int ready = countChunksReady(activeViewChunks);

            if (total > 0 && ready == total)
            {
                InitialLoadDone = true;

                playerCharacter.ShouldUpdate = true;
                playerCharacter.FreeCamEnabled = false;
            }
        }
        
        ProcessDisposeQueue(2);
        
        // Try to upload any queued meshes (must be done on main thread)
        // Opaque pass: throttle to 2 uploads per frame
        while (Mesh.MeshUploadQueue.TryDequeue(out var entry))
        //for (int i = 0; i < 1; i++)
        {
            //if (!Mesh.MeshUploadQueue.TryDequeue(out var entry))
            //    break;
            
            //Debug.TimerStart("Opaque upload");
            
            var chunkPos = entry.chunkPos;
            var meshData = entry.meshData;
        
            Mesh.OpaqueModels[chunkPos] = Mesh.UploadMesh(meshData);
            
            //Debug.TimerStop();
        }
        
        // Transparent pass: throttle to 2 uploads per frame
        while (Mesh.TMeshUploadQueue.TryDequeue(out var entry))
        //for (int i = 0; i < 1; i++)
        {
            //if (!Mesh.TMeshUploadQueue.TryDequeue(out var entry))
            //    break;
            
            //Debug.TimerStart("Transparent upload");
            
            var chunkPos = entry.chunkPos;
            var meshData = entry.meshData;
        
            Mesh.TransparentModels[chunkPos] = Mesh.UploadMesh(meshData);
            
            //Debug.TimerStop();
        }
        
        // Billboard pass: throttle to 1 upload per frame
        while (Mesh.BillboardUploadQueue.TryDequeue(out var entry))
        //for (int i = 0; i < 1; i++)
        {
            //if (!Mesh.BillboardUploadQueue.TryDequeue(out var entry))
            //    break;
            
            var chunkPos = entry.chunkPos;
            var instancesByType = entry.instancesByType;

            // Dispose existing buffers for this chunk (all types)
            foreach (var key in Mesh.BillboardGen.BillboardBatches.Keys.ToList())
            {
                if (key.ChunkPos != chunkPos)
                    continue;

                Mesh.BillboardGen.BillboardBatches[key].Dispose();
                Mesh.BillboardGen.BillboardBatches.Remove(key);
            }

            if (instancesByType == null || instancesByType.Count == 0)
                continue;

            foreach (var kv in instancesByType)
            {
                var type = kv.Key;
                var instances = kv.Value;

                if (instances == null || instances.Length == 0)
                    continue;

                var vb = new VertexBuffer(
                    Engine.Graphics,
                    VertexBillboardInstance.VertexDeclaration,
                    instances.Length,
                    BufferUsage.WriteOnly
                );

                vb.SetData(instances);
                Mesh.BillboardGen.BillboardBatches[(chunkPos, type)] = vb;
            }
        }
    }
    
    private bool IsInsideActiveView(Vector3Int pos)
    {
        return activeViewChunks.Contains(pos);
    }
    
    private static void EnsureViewOffsets()
    {
        int half = RenderDistance / 2;

        if (_viewOffsetsHalf == half && _viewOffsetsSorted.Length > 0)
            return;

        var offsets = new List<Vector3Int>((2 * half + 1) * (2 * half + 1) * (2 * half + 1));

        for (int dx = -half; dx <= half; dx++)
        for (int dy = -half; dy <= half; dy++)
        for (int dz = -half; dz <= half; dz++)
            offsets.Add(new Vector3Int(dx, dy, dz));

        offsets.Sort((a, b) =>
        {
            int da = a.X * a.X + a.Y * a.Y + a.Z * a.Z;
            int db = b.X * b.X + b.Y * b.Y + b.Z * b.Z;
            return da.CompareTo(db);
        });

        _viewOffsetsSorted = offsets.ToArray();
        _viewOffsetsHalf = half;
    }

    private void QueueChunksInView(Vector3Int center)
    {
        EnsureViewOffsets();

        foreach (var off in _viewOffsetsSorted)
        {
            var pos = new Vector3Int(
                (center.X + off.X) * ChunkSize,
                (center.Y + off.Y) * ChunkSize,
                (center.Z + off.Z) * ChunkSize);

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

    private void ProcessDisposeQueue(int maxPerFrame)
    {
        for (int i = 0; i < maxPerFrame; i++)
        {
            if (_disposeQueue.Count == 0)
                return;

            _disposeQueue.Dequeue().Dispose();
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
                    _disposeQueue.Enqueue(oModel);
                    Mesh.OpaqueModels.Remove(pos);
                }

                if (Mesh.TransparentModels.TryGetValue(pos, out var tModel) && tModel != null)
                {
                    _disposeQueue.Enqueue(tModel);
                    Mesh.TransparentModels.Remove(pos);
                }
                
                /*if (Mesh.BillboardGen.BillboardBatches.TryGetValue(pos, out var buffer))
                {
                    buffer.Dispose();
                    Mesh.BillboardGen.BillboardBatches.Remove(pos);
                }*/
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
        // Surface: only enforce +Y dependency if the above chunk is inside the active view.
        if (stage == ChunkGenerationStage.Surface)
        {
            var abovePos = chunk.Position + new Vector3Int(0, ChunkSize, 0);
    
            if (IsInsideActiveView(abovePos))
            {
                if (ChunkBuffer.TryGetValue(abovePos, out var aboveChunk))
                {
                    if (aboveChunk.GenerationStage < ChunkGenerationStage.Islands)
                        return false;
                }
                else
                {
                    EnqueueAdvance(abovePos, ChunkGenerationStage.Islands, lastCameraChunk);
                    return false;
                }
            }
            else
            {
                // Optional nudge for out-of-view above, but do not block
                if (!ChunkBuffer.ContainsKey(abovePos))
                    EnqueueAdvance(abovePos, ChunkGenerationStage.Islands, lastCameraChunk);
            }
        }
    
        // Lighting: require only neighbors inside the active view to have stable terrain (>= Decorations)
        if (stage == ChunkGenerationStage.Lighting)
        {
            foreach (var offset in TerrainUtils.GetNeighborOffsets())
            {
                var nPos = chunk.Position + offset;
    
                if (!IsInsideActiveView(nPos))
                {
                    // Optional nudge for out-of-view neighbor, but do not block
                    if (!ChunkBuffer.ContainsKey(nPos))
                        EnqueueAdvance(nPos, ChunkGenerationStage.Decorations, lastCameraChunk);
    
                    continue;
                }
    
                if (ChunkBuffer.TryGetValue(nPos, out var nChunk))
                {
                    if (nChunk.GenerationStage < ChunkGenerationStage.Decorations)
                        return false;
                }
                else
                {
                    EnqueueAdvance(nPos, ChunkGenerationStage.Decorations, lastCameraChunk);
                    return false;
                }
            }
        }
        else if (stage == ChunkGenerationStage.Meshing)
        {
            foreach (var offset in TerrainUtils.GetNeighborOffsets())
            {
                var nPos = chunk.Position + offset;
    
                if (!IsInsideActiveView(nPos))
                {
                    // Optional nudge for out-of-view neighbor, but do not block
                    if (!ChunkBuffer.ContainsKey(nPos))
                        EnqueueAdvance(nPos, ChunkGenerationStage.Lighting, lastCameraChunk);
    
                    continue;
                }
    
                if (ChunkBuffer.TryGetValue(nPos, out var nChunk))
                {
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
                foreach (var offset in TerrainUtils.GetNeighborOffsets())
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

            foreach (var off in TerrainUtils.GetNeighborOffsets())
            {
                if (!activeViewChunks.Contains(pos + off))
                {
                    allNeighborsInside = false;
                    break;
                }
            }
        }
    }
    
    private int countChunksReady(HashSet<Vector3Int> set)
    {
        int ready = 0;

        foreach (var pos in set)
        {
            if (isChunkReadyForProgress(pos))
                ready++;
        }

        return ready;
    }
    
    private bool isChunkReadyForProgress(Vector3Int pos)
    {
        // Primary threshold: Lighting
        if (ChunkBuffer.TryGetValue(pos, out var chunk))
        {
            if (chunk.GenerationStage >= ChunkGenerationStage.Lighting)
                return true;
        }

        // Fallback: Treat meshes upload as ready
        if (Mesh.OpaqueModels.ContainsKey(pos) || Mesh.TransparentModels.ContainsKey(pos))
            return true;

        return false;
    }
    
    private float stageToProgress(ChunkGenerationStage stage)
    {
        // Normalize stages to [0..1]: Uninitialized=0, Islands=0.2, ..., Meshing=1.0
        int max = (int)ChunkGenerationStage.Meshing;
        int stageNrm = Math.Clamp((int)stage, 0, max);
        
        return stageNrm / (float)max;
    }

    private float computeVisualProgress(HashSet<Vector3Int> set)
    {
        if (set.Count == 0)
            return 0f;

        float sum = 0f;

        foreach (var pos in set)
        {
            float progress = 0f;

            if (ChunkBuffer.TryGetValue(pos, out var chunk))
            {
                // Primary: stage-based progress (starts at Islands)
                progress = stageToProgress(chunk.GenerationStage);

                // If models are uploaded, treat as fully done visually
                if (Mesh.OpaqueModels.ContainsKey(pos) || Mesh.TransparentModels.ContainsKey(pos))
                    progress = 1f;
            }

            sum += progress;
        }

        return sum / set.Count;
    }

    public void DrawOpaque()
    {
        Mesh.DrawOpaque(activeViewChunks);
    }

    public void DrawBillboards()
    {
        Mesh.BillboardGen.Draw();
    }
    
    public void DrawTransparent()
    {
        Mesh.DrawTransparent(activeViewChunks);
    }
    
    public void DrawDebug()
    {
        // Chunk border debug
        if (Debug.ShowChunkDebug)
        {
            foreach (var chunkPair in ChunkBuffer)
            {
                var chunkPos = chunkPair.Key;

                float thickness = 0.01f;
                Color dbgColor = Color.Blue;

                if (chunkPair.Value.IsModified)
                {
                    thickness = 0.05f;
                    dbgColor = Color.Red;
                }
                
                // Use chunk center, not min corner
                Vector3 center = chunkPos.ToVector3() + new Vector3(ChunkSize, ChunkSize, ChunkSize);
                Debug.DrawBox(center, new Vector3(ChunkSize, ChunkSize, ChunkSize), dbgColor, thickness, 16f);
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
using Microsoft.Xna.Framework;
using DIBBLES.Systems;
using DIBBLES.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using DIBBLES.Effects;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Scenes;
using Microsoft.Xna.Framework.Graphics;

//using Debug = DIBBLES.Utils.Debug;

namespace DIBBLES.Terrain;

public class TerrainGeneration
{
    public const int RenderDistance = 12;
    public const int ChunkSize = 16;
    public const float TickRate = 2.0f; // In seconds
    public const float ReachDistance = 5f; // Has to be finite!
    //public const bool DrawDebug = false;
    
    public static TerrainMesh TMesh = new TerrainMesh();
    public static TerrainLighting Lighting = new TerrainLighting();
    public static TerrainGameplay Gameplay = new TerrainGameplay();
    public static TerrainTick TerrainTick = new TerrainTick();
    
    public static readonly ConcurrentDictionary<Vector3Int, Chunk> ECSChunks = new();
    
    // TODO: Monogame
    //public static Shader TerrainShader;

    public int Seed = -1413840509;
    //public int Seed = 1234567;
    
    public static Block SelectedBlock;
    public static Vector3Int SelectedNormal;
    
    public static bool DoneLoading = false;
    
    // Ticks
    private float tickElapsed; // seconds
    
    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen

    // Thread-safe queues for chunk and mesh work
    private readonly ConcurrentQueue<(Chunk chunk, MeshData meshData)> meshUploadQueue = new(); // Opaque
    private readonly ConcurrentQueue<(Chunk chunk, MeshData meshData)> tMeshUploadQueue = new(); // Transparent
    private readonly ConcurrentQueue<Vector3Int> chunkStagingQueue = new();
    private readonly ConcurrentDictionary<Vector3Int, bool> stagingInProgress = new();
    
    private readonly ConcurrentDictionary<Vector3Int, bool> generatingChunks = new();
    
    public void Start()
    {
        BlockData.InitializeBlockPrefabs();
        
        WorldSave.Initialize();
        WorldSave.LoadWorldData("test");
        
        if (WorldSave.Exists)
            Seed = WorldSave.Data.Seed;
        else
            Seed = new Random().Next(Int32.MinValue, int.MaxValue);
        
        WorldSave.Data.Seed = Seed;
        
        // TODO: Monogame
        //TerrainShader = Resource.LoadShader("terrain.vs", "terrain.fs");
    }
    
    private int chunksLoaded = 0;
    
    public void Update(PlayerCharacter playerCharacter)
    {
        // Calculate current chunk coordinates based on camera position
        var currentChunk = new Vector3Int(
            (int)Math.Floor(playerCharacter.Position.X / ChunkSize),
            (int)Math.Floor(playerCharacter.Position.Y / ChunkSize),
            (int)Math.Floor(playerCharacter.Position.Z / ChunkSize)
        );
        
        // Only update if the camera has moved to a new chunk
        if (currentChunk != lastCameraChunk)
        {
            lastCameraChunk = currentChunk;
            generateTerrainAsync(currentChunk);
            chunksLoaded = 0;
        }
        
        foreach (var chunk in ECSChunks.Values)
            tryQueueChunkForStaging(chunk.Position, currentChunk);
        
        processChunkStagingAsync();
        
        float expectedChunkCount = (RenderDistance + 1f) * (RenderDistance + 1f) * (RenderDistance + 1f);
        
        // Initial remesh/lighting
        if (chunksLoaded >= expectedChunkCount && !DoneLoading)
        {
            playerCharacter.NeedsToSpawn = true;
            playerCharacter.FreeCamEnabled = false;
            playerCharacter.ShouldUpdate = true;
            DoneLoading = true;
            
            TMesh.RemeshAllTransparentChunks(playerCharacter.Camera.Position);
        }
        
        // Try to upload any queued meshes (must be done on main thread)
        // Opaque pass
        while (meshUploadQueue.TryDequeue(out var entry))
        {
            var chunk = entry.chunk;
            var meshData = entry.meshData;

            TMesh.OpaqueModels.TryGetValue(chunk.Position, out var currentModel);
            
            // Upload mesh on main thread
            if (currentModel != null)
                currentModel.Dispose();
            
            TMesh.OpaqueModels[chunk.Position] = TMesh.UploadMesh(meshData);
            
            ECSChunks.TryAdd(chunk.Position, chunk);
            
            UnloadDistantChunks(currentChunk);
        }
        
        // Transparent pass
        while (tMeshUploadQueue.TryDequeue(out var entry))
        {
            var chunk = entry.chunk;
            var meshData = entry.meshData;
            
            TMesh.TransparentModels.TryGetValue(chunk.Position, out var currentModel);
            
            // Upload mesh on main thread
            if (currentModel != null)
                currentModel.Dispose();
            
            TMesh.TransparentModels[chunk.Position] = TMesh.UploadMesh(meshData);
            
            ECSChunks.TryAdd(chunk.Position, chunk);
            
            chunksLoaded++;
            
            UnloadDistantChunks(currentChunk);
        }
        
        TMesh.RecentlyRemeshedNeighbors.Clear();
        
        // Terrain ticks
        tickElapsed += Time.DeltaTime;

        if (tickElapsed >= TickRate)
        {
            TerrainTick.Tick(currentChunk);
            tickElapsed -= TickRate;
        }
        
        //if (Raylib.IsKeyPressed(KeyboardKey.U) && !Chat.IsOpen)
        //    Console.WriteLine($"Seed: {Seed}");
    }
    
    SemaphoreSlim semaphore = new(4); // Max 4 concurrent tasks
    
    private void generateTerrainAsync(Vector3Int centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3Int> chunksToGenerate = new();

        for (int cx = centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3Int chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);

            if (!ECSChunks.ContainsKey(chunkPos) && !generatingChunks.ContainsKey(chunkPos))
                chunksToGenerate.Add(chunkPos);
        }

        // Sort by distance to centerChunk
        chunksToGenerate.Sort((a, b) => 
            (a - centerChunk * ChunkSize).ToVector3().LengthSquared()
            .CompareTo((b - centerChunk * ChunkSize).ToVector3().LengthSquared())
        );
        
        foreach (var pos in chunksToGenerate)
        {
            // Spawn a background task for chunk generation
            ThreadPool.QueueUserWorkItem(x =>
            {
                semaphore.Wait(); // Blocks if 4 threads are already running

                try
                {
                    generatingChunks.TryAdd(pos, true);

                    Chunk chunk;
                    
                    if (WorldSave.Data.ModifiedChunks.TryGetValue(pos, out var savedChunk)) // Get chunk from save
                    {
                        chunk = savedChunk;
                        chunk.IsModified = true;
                        chunk.GenerationState = ChunkGenerationState.Modified;
                    }
                    else
                    {
                        chunk = new Chunk(pos);
                        GenerateChunkData(chunk);
                    }
                    
                    Lighting.Generate(chunk);
                    
                    // Gets remeshed anyways, no need for generating twice
                    var meshData = new MeshData(0, 0);
                    var tMeshData = new MeshData(0, 0);

                    // Enqueue for main thread mesh upload
                    meshUploadQueue.Enqueue((chunk, meshData));
                    tMeshUploadQueue.Enqueue((chunk, tMeshData));

                    generatingChunks.TryRemove(pos, out _);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }
    }
    
    public void GenerateChunkData(Chunk chunk)
    {
        long chunkSeed = Seed 
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
        
        var rng = new SeededRandom(chunkSeed);
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var blockReturnData = new BlockReturnData();
                blockReturnData.RNG = rng;
                blockReturnData.Noise = noise;
                
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var plainsBiome = new PlainsBiome();
                    var desertBiome = new DesertBiome();
                    var snowlandsBiome = new SnowlandsBiome();
                    
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;
                    
                    blockReturnData.WorldPos = new Vector3Int(worldX, worldY, worldZ);
                    
                    // Island noise
                    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                    noise.SetFrequency(0.01f);
                    noise.SetFractalType(FastNoiseLite.FractalType.FBm);
                    noise.SetFractalOctaves(4);
                    noise.SetFractalLacunarity(2.0f);
                    noise.SetFractalGain(0.5f);
                    
                    var islandNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                    
                    // Loop downward
                    if (islandNoise > 0.6f) // Islands
                    {
                        // TODO: Biomes other than Plains are really rare
                        // Biome noise
                        noise.SetFrequency(0.001f);
                        
                        var biomeNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;

                        if (GMath.InRangeNotEqual(biomeNoise, 0f, 0.25f)) // Desert
                            desertBiome.Generate(ref blockReturnData);
                        else if (GMath.InRangeNotEqual(biomeNoise, 0.25f, 0.5f)) // Plains
                            plainsBiome.Generate(ref blockReturnData);
                        else if (GMath.InRangeNotEqual(biomeNoise, 0.5f, 0.75f)) // Snowlands
                            plainsBiome.Generate(ref blockReturnData);
                        else // Fallback
                            snowlandsBiome.Generate(ref blockReturnData);
                        
                        blockReturnData.CurrentBlock.GeneratedInsideIsland = true;
                    }
                    else // Not islands
                        blockReturnData.CurrentBlock = new Block(new Vector3Int(worldX, worldY, worldZ), BlockType.Air);
                    
                    chunk.SetBlock(x, y, z, blockReturnData.CurrentBlock);
                }
            }
        }
        
        chunk.GenerationState = ChunkGenerationState.TerrainGenerated;
    }

    private void tryQueueChunkForStaging(Vector3Int chunkPos, Vector3Int centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        
        if (!ECSChunks.TryGetValue(chunkPos, out var chunk))
            return;

        if (chunk.GenerationState == ChunkGenerationState.TerrainGenerated ||
            chunk.GenerationState == ChunkGenerationState.Modified &&
            Math.Abs(chunkPos.X / ChunkSize - centerChunk.X) <= halfRenderDistance &&
            Math.Abs(chunkPos.Y / ChunkSize - centerChunk.Y) <= halfRenderDistance &&
            Math.Abs(chunkPos.Z / ChunkSize - centerChunk.Z) <= halfRenderDistance)
        {
            chunk.GenerationState = ChunkGenerationState.StagingQueued;
            chunkStagingQueue.Enqueue(chunkPos);
        }
    }
    
    private const int MAX_STAGING_PER_FRAME = 2;
    private void processChunkStagingAsync()
    {
        int processed = 0;

        while (processed < MAX_STAGING_PER_FRAME && chunkStagingQueue.TryDequeue(out var chunkPos))
        {
            if (stagingInProgress.ContainsKey(chunkPos))
                continue; // Already processing

            stagingInProgress.TryAdd(chunkPos, true);

            // Run staging in a background task
            ThreadPool.QueueUserWorkItem(x =>
            {
                if (!ECSChunks.TryGetValue(chunkPos, out var chunk))
                {
                    stagingInProgress.TryRemove(chunkPos, out _);
                    return;
                }
                
                // Decorations (must sync with main thread if modifying Raylib objects)
                if (!chunk.IsModified)
                    generateChunkDecorations(chunk);
                
                Lighting.Generate(chunk);

                // Mesh generation (thread safe)
                var meshData = TMesh.GenerateMeshData(chunk, false);
                var tMeshData = TMesh.GenerateMeshData(chunk, true, GameSceneMono.PlayerCharacter.Camera.Position);

                // Enqueue mesh upload for main thread
                meshUploadQueue.Enqueue((chunk, meshData));
                tMeshUploadQueue.Enqueue((chunk, tMeshData));

                // Mark as staged
                chunk.GenerationState = ChunkGenerationState.Decorations;
                
                //var chunkManager = new ChunkManager();
                //
                //var caves = new CaveGeneration(Seed);
                //caves.CarveCavesCrossChunk(chunk.Position, chunkManager, this);
                
                stagingInProgress.TryRemove(chunkPos, out _);
            });

            processed++;
        }
    }
    
    private void generateChunkDecorations(Chunk chunk)
    {
        long chunkSeed = Seed 
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
        
        var rng = new SeededRandom(chunkSeed);
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        var decorations = new TerrainDecorations();
        
        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
        {
            for (int y = ChunkSize - 1; y >= 0; y--)
            {
                var currentBlock =  chunk.GetBlock(x, y, z);

                if (currentBlock.Type == BlockType.Grass)
                {
                    if (rng.NextChance(2f))
                        decorations.GenerateTrees(currentBlock.Position);
                }
            }
        }
    }
    
    private void UnloadDistantChunks(Vector3Int centerChunk)
    {
        List<Vector3Int> chunksToRemove = new List<Vector3Int>();

        foreach (var chunk in ECSChunks)
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
            {
                chunksToRemove.Add(chunk.Key);
            }
        }

        foreach (var coord in chunksToRemove)
        {
            // Opaque model
            if (TMesh.OpaqueModels.TryGetValue(coord, out var oModel) && oModel != null)
            {
                oModel.Dispose();
                TMesh.OpaqueModels.Remove(coord);
            }

            // Transparent model
            if (TMesh.TransparentModels.TryGetValue(coord, out var tModel) &&  tModel != null)
            {
                tModel.Dispose();
                TMesh.TransparentModels.Remove(coord);
            }

            // TODO: This should be preserved as a buffer
            ECSChunks.TryRemove(coord, out var cchunk);
        }
    }
    
    public void Draw()
    {
        /*Raylib.SetShaderValue(TerrainShader, Raylib.GetShaderLocation(TerrainShader, "cameraPos"),
            GameScene.PlayerCharacter.Camera.Position,ShaderUniformDataType.Vec3);
        
        Raylib.SetShaderValue(TerrainShader, Raylib.GetShaderLocation(TerrainShader, "fogNear"), FogEffect.FogNear, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(TerrainShader, Raylib.GetShaderLocation(TerrainShader, "fogFar"), FogEffect.FogFar, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(TerrainShader, Raylib.GetShaderLocation(TerrainShader, "fogColor"), FogEffect.FogColor, ShaderUniformDataType.Vec4);*/
        
        // Draw opaque
        foreach (var oModel in TMesh.OpaqueModels)
        {
            // oModel.Value is a RuntimeModel
            if (oModel.Value != null)
            {
                var effect = (BasicEffect)oModel.Value.Effect;
                effect.Texture = BlockData.TextureAtlas;
                effect.TextureEnabled = true;
                effect.LightingEnabled = false; // If you don't want lighting
                
                oModel.Value.Texture = BlockData.TextureAtlas;
                
                oModel.Value.Draw(Matrix.CreateTranslation(oModel.Key.ToVector3()), // World matrix for chunk position
                    GameSceneMono.PlayerCharacter.Camera.View,      // Your camera's view matrix
                    GameSceneMono.PlayerCharacter.Camera.Projection // Your camera's projection matrix
                );
            }
        }
        
        // Draw transparent
        foreach (var tModel in TMesh.TransparentModels)
        {
            // oModel.Value is a RuntimeModel
            if (tModel.Value != null)
            {
                tModel.Value.Draw(Matrix.CreateTranslation(tModel.Key.ToVector3()), // World matrix for chunk position
                    GameSceneMono.PlayerCharacter.Camera.View,      // Your camera's view matrix
                    GameSceneMono.PlayerCharacter.Camera.Projection // Your camera's projection matrix
                );
            }
        }

        /*if (Debug.ShowDebugExtended)
        {
            // Draw chunk coordinates in 2D after 3D rendering
            foreach (var chunk in ECSChunks)
            {
                var chunkCenter = chunk.Key + new Vector3Int(ChunkSize / 2, ChunkSize / 2, ChunkSize / 2);
                Debug.Draw3DText($"Chunk ({chunk.Key.X}, {chunk.Key.Z})", chunkCenter.ToVector3(), Microsoft.Xna.Framework.Color.White);
                
                Microsoft.Xna.Framework.Color debugColor;
            
                if (chunk.Value.GenerationState == ChunkGenerationState.Modified)
                    debugColor = Microsoft.Xna.Framework.Color.Red;
                else
                    debugColor = Microsoft.Xna.Framework.Color.Blue;

                var padding = 0.01f;
                
                // TODO: Monogame
                //Raylib.DrawCubeWires(chunk.Value.Position.ToVector3() + new Vector3(ChunkSize / 2f + padding, ChunkSize / 2f + padding, ChunkSize / 2f + padding),
                //    ChunkSize - padding, ChunkSize - padding, ChunkSize - padding, debugColor);
            }

            var pos = new Vector3(SelectedBlock.Position.X + 0.5f, SelectedBlock.Position.Y + 1.5f, SelectedBlock.Position.Z + 0.5f);
            Debug.Draw3DText($"Block: {SelectedBlock.Position}", pos, Microsoft.Xna.Framework.Color.White, 0.25f);
        }*/
        
        /*if (SelectedBlock.GeneratedInsideIsland)
        {
            RayEx.DrawCubeWiresThick(SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Green);
        }
        else
        {
            RayEx.DrawCubeWiresThick(SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Red);
        }*/
        
        if (!SelectedBlock.IsAir)
        {
            Vector3 center = SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
            
            // Offset by half a block in the direction of the normal
            Vector3 faceCenter = center + (SelectedNormal.ToVector3() * 0.51f);
            
            // Face selection overlay
            //RayEx.DrawPlane(faceCenter, new Vector2(0.25f, 0.25f), new Color(1f, 1f, 1f, 0.2f), SelectedNormal.ToVector3());
            
            // Draw block selection overlay
            //RayEx.DrawCubeWiresThick(SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Black);
        }
    }
}

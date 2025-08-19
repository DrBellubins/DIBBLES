using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Utils;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Scenes;
using DIBBLES.Terrain;
using Debug = DIBBLES.Utils.Debug;

namespace DIBBLES.Systems;

public class TerrainGeneration
{
    public const int RenderDistance = 16;
    public const int ChunkSize = 16;
    public const float ReachDistance = 5f; // Has to be finite!
    public const bool DrawDebug = false;
    
    public static Dictionary<Vector3Int, Chunk> Chunks = new();
    
    public static Shader terrainShader;
    
    public static int Seed = 1337;
    
    public static Block? SelectedBlock;
    public static Vector3Int SelectedNormal;
    
    public static bool DoneLoading = false;
    
    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen

    // Thread-safe queues for chunk and mesh work
    private ConcurrentQueue<(Chunk chunk, MeshData meshData)> meshUploadQueue = new();
    private HashSet<Vector3Int> pendingNeighbors = new();
    private ConcurrentDictionary<Vector3Int, bool> generatingChunks = new();
    
    private Stopwatch stopwatch = new();
    
    public void Start()
    {
        Block.InitializeBlockPrefabs();
        
        WorldSave.Initialize();
        WorldSave.LoadWorldData("test");
        
        //if (WorldSave.Exists)
        //    Seed = WorldSave.Data.Seed;
        //else
        //    Seed = new Random().Next(Int32.MinValue, int.MaxValue);
        
        WorldSave.Data.Seed = Seed;
        
        terrainShader = Resource.LoadShader("terrain.vs", "terrain.fs");
    }
    
    private bool initialLoad = false;
    
    public void Update(PlayerCharacter playerCharacter)
    {
        // Calculate current chunk coordinates based on camera position
        var currentChunk = new Vector3Int(
            (int)Math.Floor(playerCharacter.Position.X / ChunkSize),
            (int)Math.Floor(playerCharacter.Position.Y / ChunkSize),
            (int)Math.Floor(playerCharacter.Position.Z / ChunkSize)
        );

        // Only update if the camera has moved to a new chunk
        if (!initialLoad)
        //if (currentChunk != lastCameraChunk)
        {
            lastCameraChunk = currentChunk;
            generateTerrainAsync(currentChunk);
            UnloadDistantChunks(currentChunk);
            
            initialLoad = true;
        }
        
        // Initial remesh/lighting
        if (areAllChunksLoaded(currentChunk) && !DoneLoading)
        {
            foreach (var chunk in Chunks.Values)
                GameScene.TMesh.RemeshNeighbors(chunk);

            playerCharacter.ShouldUpdate = true;
            DoneLoading = true;
        }
        
        // Try to upload any queued meshes (must be done on main thread)
        while (meshUploadQueue.TryDequeue(out var entry))
        {
            var chunk = entry.chunk;
            var meshData = entry.meshData;
            
            // Upload mesh on main thread
            if (chunk.Model.MeshCount > 0)
                Raylib.UnloadModel(chunk.Model);

            chunk.Model = GameScene.TMesh.UploadMesh(meshData);
            Chunks[chunk.Position] = chunk;
        };
        
        GameScene.TMesh.RecentlyRemeshedNeighbors.Clear();

        if (Raylib.IsKeyPressed(KeyboardKey.U))
            Console.WriteLine($"Seed: {Seed}");
    }
    
    private void generateTerrainAsync(Vector3Int centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3Int> chunksToGenerate = new();

        for (int cx = centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3Int chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);

            if (!Chunks.ContainsKey(chunkPos) && !generatingChunks.ContainsKey(chunkPos))
                chunksToGenerate.Add(chunkPos);
        }

        foreach (var pos in chunksToGenerate)
        {
            // Spawn a background task for chunk generation
            Task.Run(() =>
            {
                try
                {
                    generatingChunks.TryAdd(pos, true);
                    
                    Chunk chunk;

                    // Check if chunk is in WorldSave.ModifiedChunks
                    if (WorldSave.Data.ModifiedChunks.TryGetValue(pos, out var savedChunk))
                    {
                        chunk = savedChunk;
                        GameScene.Lighting.Generate(chunk);
                    }
                    else
                    {
                        chunk = new Chunk(pos);
                        generateChunkData(chunk);
                        GameScene.Lighting.Generate(chunk);
                    }

                    var meshData = GameScene.TMesh.GenerateMeshData(chunk);
                    //var tMeshData = GameScene.TMesh.GenerateTransparentMeshData(chunk);

                    // Enqueue for main thread mesh upload
                    meshUploadQueue.Enqueue((chunk, meshData));

                    generatingChunks.TryRemove(pos, out _);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });
        }
    }

    private void chunkTickAsync(Vector3Int centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3Int> chunksToTick = new();

        for (int cx = centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3Int chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);

            if (Chunks.ContainsKey(chunkPos))
                chunksToTick.Add(chunkPos);
        }

        foreach (var pos in chunksToTick)
        {
            Task.Run(() =>
            {
                try
                {
                    var chunk = Chunks[pos]; // Might not be thread-safe!
                    
                    // Update lighting, then remesh.
                    GameScene.Lighting.Generate(chunk);
                    
                    var meshData = GameScene.TMesh.GenerateMeshData(chunk);
                    meshUploadQueue.Enqueue((chunk, meshData));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });
        }
    }
    
    private static void generateChunkData(Chunk chunk)
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
                blockReturnData.SurfaceY = -1;
                
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
                        // Biome noise
                        noise.SetFrequency(0.005f);
                        
                        var biomeNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                        
                        if (GMath.InRangeNotEqual(biomeNoise, 0f, 0.25f)) // Desert
                            desertBiome.Generate(ref blockReturnData);
                        else if (GMath.InRangeNotEqual(biomeNoise, 0.25f, 0.5f)) // Plains
                            plainsBiome.Generate(ref blockReturnData);
                        else if (GMath.InRangeNotEqual(biomeNoise, 0.5f, 0.75f)) // Snowlands
                            plainsBiome.Generate(ref blockReturnData);
                        else // Fallback
                            snowlandsBiome.Generate(ref blockReturnData);

                        blockReturnData.CurrentBlock.InsideIsland = true;
                    }
                    else // Not islands
                    {
                        blockReturnData.CurrentBlock = new Block(new Vector3Int(worldX, worldY, worldZ), Block.Prefabs[BlockType.Air]);
                    }
                    
                    chunk.Blocks[x, y, z] = blockReturnData.CurrentBlock;

                    // Loop upward
                    /*if (foundSurface)
                    {
                        int wispOffset = 4;
                        int wispY = surfaceY + wispOffset;
                        int maxWorldY = chunk.Position.Y + ChunkSize - 1;

                        if (wispY >= chunk.Position.Y && wispY <= maxWorldY)
                        {
                            int localWispY = wispY - chunk.Position.Y;

                            // Only place wisp if air
                            if (chunk.Blocks[x, localWispY, z].Info.Type == BlockType.Air)
                            {
                                if (rng.NextChance(0.2f))
                                    chunk.Blocks[x, localWispY, z] = new Block(new Vector3Int(worldX, wispY, worldZ), Block.Prefabs[BlockType.Wisp]);
                            }
                        }
                    }*/
                }
            }
        }
    }
    
    private void UnloadDistantChunks(Vector3Int centerChunk)
    {
        List<Vector3Int> chunksToRemove = new List<Vector3Int>();

        foreach (var chunk in Chunks)
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
            Raylib.UnloadModel(Chunks[coord].Model); // Unload the model to free memory
            Chunks.Remove(coord);
            OnChunkUnloaded(coord);
        }
    }
    
    private void updateChunkDayNight(Chunk chunk)
    {
        // Example: Update block light levels, spawn mobs, etc.
        if (DayNightCycle.IsDay)
        {
            // Set sunlight, despawn night mobs, etc.
        }
        else
        {
            // Set moonlight, spawn night mobs, etc.
        }

        // You could also apply gradual transitions based on Time.WorldTime.
    }
    
    private void OnChunkUnloaded(Vector3Int chunkPos)
    {
        int[] offsets = { -ChunkSize, ChunkSize };
        foreach (var axis in new[] { 0, 1, 2 })
        {
            Vector3Int neighborPos = chunkPos;
            foreach (int offset in offsets)
            {
                if (axis == 0) neighborPos.X = chunkPos.X + offset;
                if (axis == 1) neighborPos.Y = chunkPos.Y + offset;
                if (axis == 2) neighborPos.Z = chunkPos.Z + offset;
    
                if (Chunks.TryGetValue(neighborPos, out var neighborChunk))
                {
                    GameScene.Lighting.Generate(neighborChunk);
                    GameScene.TMesh.RemeshNeighbors(neighborChunk);
                }
            }
            neighborPos = chunkPos;
        }
    }
    
    private bool areAllChunksLoaded(Vector3Int centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        
        for (int cx = centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3Int chunkPos = new Vector3Int(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
            
            if (!Chunks.ContainsKey(chunkPos))
                return false;
        }
        
        return true;
    }
    
    public void Draw()
    {
        foreach (var chunk in Chunks.Values)
        {
            Raylib.DrawModel(chunk.Model, chunk.Position.ToVector3(), 1.0f, Color.White);

            if (DrawDebug)
            {
                Color debugColor;
            
                if (chunk.Info.Modified)
                    debugColor = Color.Red;
                else
                    debugColor = Color.Blue;

                var padding = 0.01f;
                
                Raylib.DrawCubeWires(chunk.Position.ToVector3() + new Vector3(ChunkSize / 2f + padding, ChunkSize / 2f + padding, ChunkSize / 2f + padding),
                    ChunkSize - padding, ChunkSize - padding, ChunkSize - padding, debugColor);
            }
        }
        
        if (DrawDebug)
        {
            // Draw chunk coordinates in 2D after 3D rendering
            foreach (var pos in Chunks.Keys)
            {
                var chunkCenter = pos + new Vector3Int(ChunkSize / 2, ChunkSize / 2, ChunkSize / 2);
                Debug.Draw3DText($"Chunk ({pos.X}, {pos.Z})", chunkCenter.ToVector3(), Color.White);
            }

            if (SelectedBlock != null)
            {
                var pos = new Vector3(SelectedBlock.Position.X + 0.5f, SelectedBlock.Position.Y + 1.5f, SelectedBlock.Position.Z + 0.5f);
                Debug.Draw3DText($"Block: {SelectedBlock.Position}", pos, Color.White, 0.25f);
            }
        }
        
        // Face selection overlay
        if (SelectedBlock != null)
        {
            RayEx.DrawCubeWiresThick(SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Black);
                
            // Center of the block
            Vector3 center = SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);

            // Offset by half a block in the direction of the normal
            Vector3 faceCenter = center + (SelectedNormal.ToVector3() * 0.51f);
                
            RayEx.DrawPlane(faceCenter, new Vector2(0.25f, 0.25f), new Color(1f, 1f, 1f, 0.2f), SelectedNormal.ToVector3());
        }
    }
}
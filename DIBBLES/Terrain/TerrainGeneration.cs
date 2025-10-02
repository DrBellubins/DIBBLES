using System.Collections.Concurrent;
using System.Net;
using DIBBLES.Effects;
using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Scenes;
using DIBBLES.Systems;
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
    public static TerrainMesh Mesh = new();
    public static TerrainLighting Lighting = new();
    public static TerrainGameplay Gameplay = new();
    public static Effect terrainShader;
    public static bool DoneLoading = false;
    
    // Gameplay
    public static Block SelectedBlock;
    public static Vector3Int SelectedNormal;
    
    // Thread-safe mesh generation queues
    private readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> meshUploadQueue = new(); // Opaque
    private readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> tMeshUploadQueue = new(); // Transparent

    private ChunkGenerationStage terrainGenerationStage = ChunkGenerationStage.Uninitialized;
    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen
    private int chunksLoaded = 0;
    
    public void Start()
    {
        BlockData.InitializeBlockPrefabs();
        
        WorldSave.Initialize();
        WorldSave.LoadWorldData("test");
        
        //if (WorldSave.Exists)
        //    Seed = WorldSave.Data.Seed;
        //else
        //    Seed = new Random().Next(Int32.MinValue, int.MaxValue);
        
        // Load modified chunks into chunk buffer
        foreach (var kv in WorldSave.Data.ModifiedChunks)
            ChunkBuffer[kv.Key] = kv.Value;
        
        terrainShader = Engine.Instance.Content.Load<Effect>("Shaders/Terrain");
        
        Commands.RegisterCommand("seed", "Displays seed in chat, and saves to txt file.", seedCmd);
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
        if (chunksLoaded >= expectedChunkCount && !DoneLoading)
        {
            playerCharacter.NeedsToSpawn = true;
            playerCharacter.FreeCamEnabled = false;
            playerCharacter.ShouldUpdate = true;
            DoneLoading = true;
        }
        
        // Try to upload any queued meshes (must be done on main thread)
        // Opaque pass
        while (meshUploadQueue.TryDequeue(out var entry))
        {
            var chunkPos = entry.chunkPos;
            var meshData = entry.meshData;
            
            // Upload mesh on main thread
            Mesh.OpaqueModels[chunkPos] = Mesh.UploadMesh(meshData);
        }
        
        // Transparent pass
        while (tMeshUploadQueue.TryDequeue(out var entry))
        {
            var chunkPos = entry.chunkPos;
            var meshData = entry.meshData;
            
            // Upload mesh on main thread
            Mesh.TransparentModels[chunkPos] = Mesh.UploadMesh(meshData);
            chunksLoaded++;
        }
        
        unloadDistantChunks(centerChunk);
    }

    public static bool InRenderDistance(Vector3Int chunkPos, Vector3Int centerChunk)
    {
        int chunkX = chunkPos.X / ChunkSize;
        int chunkY = chunkPos.Y / ChunkSize;
        int chunkZ = chunkPos.Z / ChunkSize;

        int centerX = centerChunk.X;
        int centerY = centerChunk.Y;
        int centerZ = centerChunk.Z;

        int dx = Math.Abs(chunkX - centerX);
        int dy = Math.Abs(chunkY - centerY);
        int dz = Math.Abs(chunkZ - centerZ);

        return dx <= RenderDistance / 2 && dy <= RenderDistance / 2 && dz <= RenderDistance / 2;
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

            chunksToGenerate.Add(chunkPos);
        }

        // Sort by distance to centerChunk
        chunksToGenerate.Sort((a, b) => 
            (a - centerChunk * ChunkSize).ToVector3().LengthSquared()
            .CompareTo((b - centerChunk * ChunkSize).ToVector3().LengthSquared())
        );
        
        foreach (var pos in chunksToGenerate)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                semaphore.Wait();
                
                try
                {
                    Chunk chunk;
                    
                    // Load from either modified chunks or chunk buffer
                    if (WorldSave.Data.ModifiedChunks.TryGetValue(pos, out var modifiedChunk))
                        chunk = modifiedChunk;
                    else if (ChunkBuffer.TryGetValue(pos, out var bufferChunk))
                        chunk = bufferChunk;
                    else
                        chunk = new Chunk(pos);
                    
                    if (DoneLoading && addAfterInitial)
                    {
                        // Catch up to Meshing on demand post-initial
                        while (chunk.GenerationStage <= ChunkGenerationStage.Meshing)
                            proccesTerrainStage(chunk);
                    }
                    else
                    {
                        // Initial pass advances one stage per wave
                        proccesTerrainStage(chunk);
                        ChunkBuffer.TryAdd(pos, chunk);
                    }
                }
                finally { semaphore.Release(); }
            });
        }
    }

    private void proccesTerrainStage(Chunk chunk)
    {
        // If chunk is modified, skip to lighting and meshing
        if (WorldSave.Data.ModifiedChunks.ContainsKey(chunk.Position))
        {
            // Only progress if at or before Lighting
            if (chunk.GenerationStage < ChunkGenerationStage.Lighting)
                chunk.GenerationStage = ChunkGenerationStage.Lighting;
        
            switch (chunk.GenerationStage)
            {
                case ChunkGenerationStage.Lighting:
                    generateLighting(chunk);
                    chunk.GenerationStage++;
                    break;
                case ChunkGenerationStage.Meshing:
                    generateMesh(chunk);
                    chunk.GenerationStage++;
                    break;
            }
            
            return;
        }
        
        // If chunk is already in buffer, skip to lighting and meshing
        if (ChunkBuffer.ContainsKey(chunk.Position))
        {
            // Only progress if at or before Lighting
            if (chunk.GenerationStage < ChunkGenerationStage.Lighting)
                chunk.GenerationStage = ChunkGenerationStage.Lighting;
        
            switch (chunk.GenerationStage)
            {
                case ChunkGenerationStage.Lighting:
                    generateLighting(chunk);
                    chunk.GenerationStage++;
                    break;
                case ChunkGenerationStage.Meshing:
                    generateMesh(chunk);
                    chunk.GenerationStage++;
                    break;
            }
            
            return;
        }

        // Unmodified: normal pipeline
        switch (chunk.GenerationStage)
        {
            case ChunkGenerationStage.Uninitialized:
                chunk.GenerationStage++;
                break;
            case ChunkGenerationStage.Islands:
                generateIslands(chunk);
                chunk.GenerationStage++;
                break;
            case ChunkGenerationStage.Surface:
                generateSurface(chunk);
                chunk.GenerationStage++;
                break;
            case ChunkGenerationStage.Decorations:
                generateChunkDecorations(chunk);
                chunk.GenerationStage++;
                break;
            case ChunkGenerationStage.Lighting:
                generateLighting(chunk);
                chunk.GenerationStage++;
                break;
            case ChunkGenerationStage.Meshing:
                generateMesh(chunk);
                chunk.GenerationStage++;
                break;
        }
    }
    
    private void generateIslands(Chunk chunk)
    {
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;
                    
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
                        chunk.SetTypeAt(x, y, z, BlockType.Stone);
                    else // Not islands
                        chunk.SetTypeAt(x, y, z, BlockType.Air);
                }
            }
        }
    }

    private void generateSurface(Chunk chunk)
    {
        long chunkSeed = Seed 
                         ^ (chunk.Position.X * 73428767L)
                         ^ (chunk.Position.Y * 9127841L)
                         ^ (chunk.Position.Z * 192837465L);
        
        var rng = new SeededRandom(chunkSeed);
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        var plainsBiome = new PlainsBiome();
        var desertBiome = new DesertBiome();
        var snowlandsBiome = new SnowlandsBiome();
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var blockReturnData = new BlockReturnData();
                blockReturnData.RNG = rng;
                blockReturnData.Noise = noise;
                
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;

                    blockReturnData.LocalPos = new Vector3Int(x, y, z);

                    var currentType = Chunk.GetBlockTypeGlobal(new Vector3Int(worldX, worldY, worldZ));
                    
                    if (currentType.Item1 != BlockType.Stone)
                        continue;
                    
                    // TODO: Biomes other than Plains are really rare
                    /*noise.SetFrequency(0.001f);
                    var biomeNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;

                    if (GMath.InRangeNotEqual(biomeNoise, 0f, 0.25f)) // Desert
                        desertBiome.Generate(chunk, ref blockReturnData);
                    else if (GMath.InRangeNotEqual(biomeNoise, 0.25f, 0.5f)) // Plains
                        plainsBiome.Generate(chunk, ref blockReturnData);
                    else if (GMath.InRangeNotEqual(biomeNoise, 0.5f, 0.75f)) // Snowlands
                        plainsBiome.Generate(chunk, ref blockReturnData);
                    else // Fallback
                        snowlandsBiome.Generate(chunk, ref blockReturnData);*/
                    
                    plainsBiome.Generate(chunk, ref blockReturnData);
                }
            }
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
                var currentBlockType =  chunk.GetTypeAt(x, y, z);
                var pos = new Vector3Int(x, y, z);

                if (currentBlockType == BlockType.Grass)
                {
                    if (rng.NextChance(0.5f))
                        decorations.GenerateTrees(pos, chunk);
                }
            }
        }
    }

    private void generateLighting(Chunk chunk)
    {
        Lighting.Generate(chunk);
        
        // Re-propagate existing neighbors to handle cross-chunk
        var faceInfos = FaceUtils.VoxelFaceInfos();
        foreach (var (_, _, neighborOffset) in faceInfos)
        {
            var neighborPos = chunk.Position + neighborOffset * new Vector3Int(ChunkSize, ChunkSize, ChunkSize);
            
            if (ChunkBuffer.TryGetValue(neighborPos, out var neighborChunk) &&
                neighborChunk.GenerationStage >= ChunkGenerationStage.Lighting) // Only if neighbor already lit
            {
                //Lighting.PropagateLight(neighborChunk); // Re-runs BFS from its current >0 blocks, propagating cross-chunk if needed
            }
        }
    }

    public void generateMesh(Chunk chunk)
    {
        var meshData = Mesh.GenerateMeshData(chunk, false);
        var tMeshData = Mesh.GenerateMeshData(chunk, true, GameScene.PlayerCharacter.Camera.Position.ToVector3());
        
        // Enqueue for main thread mesh upload
        meshUploadQueue.Enqueue((chunk.Position, meshData));
        tMeshUploadQueue.Enqueue((chunk.Position, tMeshData));
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
            {
                chunksToRemove.Add(chunk.Key);
            }
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
        }
    }
    
    public void Draw()
    {
        // Draw opaque
        foreach (var oModel in Mesh.OpaqueModels)
        {
            // oModel.Value is a RuntimeModel
            if (oModel.Value != null)
            {
                var world = Matrix.CreateTranslation(oModel.Key.ToVector3());
                
                var shader = oModel.Value.Shader;
                
                shader.Parameters["World"].SetValue(world);
                shader.Parameters["View"].SetValue(GameScene.PlayerCharacter.Camera.View);
                shader.Parameters["Projection"].SetValue(GameScene.PlayerCharacter.Camera.Projection);
                shader.Parameters["Texture0"].SetValue(BlockData.TextureAtlas);
                shader.Parameters["CameraPos"].SetValue(GameScene.PlayerCharacter.Camera.Position.ToVector3());
                shader.Parameters["FogNear"].SetValue(FogEffect.FogNear);
                shader.Parameters["FogFar"].SetValue(FogEffect.FogFar);
                shader.Parameters["FogColor"].SetValue(FogEffect.FogColor());
                
                foreach (var pass in shader.CurrentTechnique.Passes)
                    pass.Apply();
                
                oModel.Value.Draw(world,                        // World matrix for chunk position
                    GameScene.PlayerCharacter.Camera.View,      // Your camera's view matrix
                    GameScene.PlayerCharacter.Camera.Projection // Your camera's projection matrix
                );
            }
        }
        
        // Draw transparent
        foreach (var tModel in Mesh.TransparentModels)
        {
            // oModel.Value is a RuntimeModel
            if (tModel.Value != null)
            {
                var world = Matrix.CreateTranslation(tModel.Key.ToVector3());
                
                var shader = tModel.Value.Shader;
                
                shader.Parameters["World"].SetValue(world);
                shader.Parameters["View"].SetValue(GameScene.PlayerCharacter.Camera.View);
                shader.Parameters["Projection"].SetValue(GameScene.PlayerCharacter.Camera.Projection);
                shader.Parameters["Texture0"].SetValue(BlockData.TextureAtlas);
                shader.Parameters["CameraPos"].SetValue(GameScene.PlayerCharacter.Camera.Position.ToVector3());
                shader.Parameters["FogNear"].SetValue(FogEffect.FogNear);
                shader.Parameters["FogFar"].SetValue(FogEffect.FogFar);
                shader.Parameters["FogColor"].SetValue(FogEffect.FogColor());
                
                foreach (var pass in shader.CurrentTechnique.Passes)
                    pass.Apply();
                
                tModel.Value.Draw(world,                        // World matrix for chunk position
                    GameScene.PlayerCharacter.Camera.View,      // Your camera's view matrix
                    GameScene.PlayerCharacter.Camera.Projection // Your camera's projection matrix
                );
            }
        }
    }

    private void seedCmd(string[] args)
    {
        Chat.Write($"Current seed: {Seed}", ChatMessageType.Command);

        var filename = Path.Combine(AppContext.BaseDirectory, $"Seed_{Seed}.txt");

        if (!File.Exists(filename))
        {
            File.Create(filename);
            Chat.Write($"Wrote to file: Seed_{Seed}.txt", ChatMessageType.Command);
        }
        else
            Chat.Write($"Seed file 'Seed_{Seed}.txt' already exists.", ChatMessageType.Warning);
    }
}
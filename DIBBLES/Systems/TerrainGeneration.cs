using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Utils;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace DIBBLES.Systems;

// TODO: Chunks no longer cull blocks inside of neighboring chunk islands
// TODO: There are edge cases where faces in neighbor chunks don't regen when breaking/placing
public class TerrainGeneration
{
    public const int RenderDistance = 8;
    public const int ChunkSize = 16;
    public const float ReachDistance = 100f; // Has to be finite!
    public const bool DrawDebug = false;
    
    public static Dictionary<Vector3, Chunk> Chunks = new Dictionary<Vector3, Chunk>();
    
    public static Shader terrainShader;
    public static TerrainMesh TMesh = new TerrainMesh();
    public static TerrainLighting Lighting = new TerrainLighting();
    public static TerrainGameplay Gameplay = new TerrainGameplay();
    
    public static FastNoiseLite Noise = new FastNoiseLite();
    
    public static Block? SelectedBlock;
    
    private Vector3 lastCameraChunk = Vector3.One; // Needs to != zero for first gen

    public int Seed;

    // Thread-safe queues for chunk and mesh work
    private ConcurrentQueue<(Chunk chunk, MeshData meshData)> meshUploadQueue = new();
    private ConcurrentDictionary<Vector3, Chunk> pendingChunks = new();
    private ConcurrentDictionary<Vector3, bool> generatingChunks = new();

    public void Start()
    {
        Block.InitializeBlockPrefabs();
        
        if (WorldSave.Data.Seed != 0)
            Seed = WorldSave.Data.Seed;
        else
            Seed = new Random().Next(int.MaxValue);

        terrainShader = Resource.LoadShader("terrain.vs", "terrain.fs");
        
        Noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        Noise.SetSeed(1337);
        Noise.SetFrequency(0.02f);
        Noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        Noise.SetFractalOctaves(4);
        Noise.SetFractalLacunarity(2.0f);
        Noise.SetFractalGain(0.5f);
    }

    private bool hasGenerated = false; // FOR TESTING PURPOSES
    public void UpdateTerrain(Camera3D camera)
    {
        // Calculate current chunk coordinates based on camera position
        var currentChunk = new Vector3(
            (int)Math.Floor(camera.Position.X / ChunkSize),
            0f,
            (int)Math.Floor(camera.Position.Z / ChunkSize)
        );

        // Only update if the camera has moved to a new chunk
        //if (currentChunk != lastCameraChunk)
        if (!hasGenerated)
        {
            lastCameraChunk = currentChunk;
            GenerateTerrainAsync(currentChunk);
            UnloadDistantChunks(currentChunk);
            
            hasGenerated = true;
        }

        // Try to upload any queued meshes (must be done on main thread)
        while (meshUploadQueue.TryDequeue(out var entry))
        {
            var chunk = entry.chunk;
            var meshData = entry.meshData;

            // Upload mesh on main thread
            if (chunk.Model.MeshCount > 0)
                Raylib.UnloadModel(chunk.Model);

            chunk.Model = TMesh.UploadMesh(meshData);
            Chunks[chunk.Position] = chunk;
        }
    }
    
    private void GenerateTerrainAsync(Vector3 centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3> chunksToGenerate = new List<Vector3>();

        for (int cx = (int)centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = (int)centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = (int)centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3 chunkPos = new Vector3(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);

            if (!Chunks.ContainsKey(chunkPos) && !generatingChunks.ContainsKey(chunkPos))
                chunksToGenerate.Add(chunkPos);
        }

        foreach (var pos in chunksToGenerate)
        {
            generatingChunks.TryAdd(pos, true);

            // Spawn a background task for chunk generation
            Task.Run(() =>
            {
                try
                {
                    var chunk = new Chunk(pos);
                    GenerateChunkData(chunk);
                    Lighting.Generate(chunk);

                    // Generate mesh data in this thread (not Raylib mesh!)
                    var meshData = TMesh.GenerateMeshData(chunk);

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
    
    public static void GenerateChunkData(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var foundSurface = false;
                var islandDepth = 0;
                
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    var worldX = chunk.Position.X + x;
                    var worldY = chunk.Position.Y + y;
                    var worldZ = chunk.Position.Z + z;

                    var noise = Noise.GetNoise(worldX, worldY, worldZ);

                    if (noise > 0.25f)
                    {
                        if (!foundSurface)
                        {
                            // This is the surface
                            chunk.Blocks[x, y, z] = new Block(new Vector3(worldX, worldY, worldZ), Block.Prefabs[BlockType.Grass]);
                            foundSurface = true;
                            islandDepth = 0;
                        }
                        else if (islandDepth < 3) // dirt thickness = 3
                        {
                            chunk.Blocks[x, y, z] = new Block(new Vector3(worldX, worldY, worldZ), Block.Prefabs[BlockType.Dirt]);
                            islandDepth++;
                        }
                        else
                        {
                            chunk.Blocks[x, y, z] = new Block(new Vector3(worldX, worldY, worldZ), Block.Prefabs[BlockType.Stone]);
                            islandDepth++;
                        }
                    }
                    else
                    {
                        chunk.Blocks[x, y, z] = new Block(new Vector3(worldX, worldY, worldZ), Block.Prefabs[BlockType.Air]);
                        islandDepth = 0;
                    }
                }
            }
        }
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
            
            if (SelectedBlock != null)
                Raylib.DrawCubeWires(SelectedBlock.Position + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Black);

            if (DrawDebug)
            {
                Color debugColor;
            
                if (chunk.Info.Modified)
                    debugColor = Color.Red;
                else
                    debugColor = Color.Blue;
            
                Raylib.DrawCubeWires(chunk.Position + new Vector3(ChunkSize / 2f, ChunkSize / 2f, ChunkSize / 2f),
                    ChunkSize, ChunkSize, ChunkSize, debugColor);
            }
        }
        
        if (DrawDebug)
        {
            // Draw chunk coordinates in 2D after 3D rendering
            foreach (var pos in TerrainGeneration.Chunks.Keys)
            {
                var chunkCenter = pos + new Vector3(TerrainGeneration.ChunkSize / 2f, TerrainGeneration.ChunkSize + 2f, TerrainGeneration.ChunkSize / 2f);
                Debug.Draw3DText($"Chunk ({pos.X}, {pos.Z})", chunkCenter, Color.White);
            }
        }
    }
}
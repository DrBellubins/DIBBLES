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
    
    public static Dictionary<Vector3Int, Chunk> Chunks = new();
    
    public static Shader terrainShader;
    public static TerrainMesh TMesh = new TerrainMesh();
    public static TerrainLighting Lighting = new TerrainLighting();
    public static TerrainGameplay Gameplay = new TerrainGameplay();
    
    public static FastNoiseLite Noise = new FastNoiseLite();
    
    public static Block? SelectedBlock;
    
    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen

    public int Seed;

    // Thread-safe queues for chunk and mesh work
    private ConcurrentDictionary<Vector3Int, Chunk> pendingChunks = new();
    private ConcurrentQueue<(Chunk chunk, MeshData meshData)> meshUploadQueue = new();
    private ConcurrentDictionary<Vector3Int, bool> generatingChunks = new();

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
        var currentChunk = new Vector3Int(
            (int)Math.Floor(camera.Position.X / ChunkSize),
            (int)Math.Floor(camera.Position.Y / ChunkSize),
            (int)Math.Floor(camera.Position.Z / ChunkSize)
        );

        // Only update if the camera has moved to a new chunk
        if (currentChunk != lastCameraChunk)
        //if (!hasGenerated)
        {
            lastCameraChunk = currentChunk;
            generateTerrainAsync(currentChunk);
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
            generatingChunks.TryAdd(pos, true);

            // Spawn a background task for chunk generation
            Task.Run(() =>
            {
                try
                {
                    var chunk = new Chunk(pos);
                    GenerateChunkData(chunk);
                    //Lighting.Generate(chunk);

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
                            chunk.Blocks[x, y, z] = new Block(new Vector3Int(worldX, worldY, worldZ), Block.Prefabs[BlockType.Grass]);
                            foundSurface = true;
                            islandDepth = 0;
                        }
                        else if (islandDepth < 3) // dirt thickness = 3
                        {
                            chunk.Blocks[x, y, z] = new Block(new Vector3Int(worldX, worldY, worldZ), Block.Prefabs[BlockType.Dirt]);
                            islandDepth++;
                        }
                        else
                        {
                            chunk.Blocks[x, y, z] = new Block(new Vector3Int(worldX, worldY, worldZ), Block.Prefabs[BlockType.Stone]);
                            islandDepth++;
                        }
                    }
                    else
                    {
                        chunk.Blocks[x, y, z] = new Block(new Vector3Int(worldX, worldY, worldZ), Block.Prefabs[BlockType.Air]);
                        islandDepth = 0;
                    }
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
        }
    }
    
    private bool allNeighborsGenerated(Vector3Int chunkPos)
    {
        int[] offsets = { -ChunkSize, ChunkSize };
        
        foreach (int dx in offsets)
            if (!Chunks.ContainsKey(new Vector3Int(chunkPos.X + dx, chunkPos.Y, chunkPos.Z)))
                return false;
        
        foreach (int dy in offsets)
            if (!Chunks.ContainsKey(new Vector3Int(chunkPos.X, chunkPos.Y + dy, chunkPos.Z)))
                return false;
        
        foreach (int dz in offsets)
            if (!Chunks.ContainsKey(new Vector3Int(chunkPos.X, chunkPos.Y, chunkPos.Z + dz)))
                return false;
        
        return true;
    }
    
    public void Draw()
    {
        foreach (var chunk in Chunks.Values)
        {
            Raylib.DrawModel(chunk.Model, chunk.Position.ToVector3(), 1.0f, Color.White);
            
            if (SelectedBlock != null)
                Raylib.DrawCubeWires(SelectedBlock.Position.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Black);

            if (DrawDebug)
            {
                Color debugColor;
            
                if (chunk.Info.Modified)
                    debugColor = Color.Red;
                else
                    debugColor = Color.Blue;
            
                Raylib.DrawCubeWires(chunk.Position.ToVector3() + new Vector3(ChunkSize / 2f, ChunkSize / 2f, ChunkSize / 2f),
                    ChunkSize, ChunkSize, ChunkSize, debugColor);
            }
        }
        
        if (DrawDebug)
        {
            // Draw chunk coordinates in 2D after 3D rendering
            foreach (var pos in Chunks.Keys)
            {
                var chunkCenter = pos + new Vector3Int(ChunkSize / 2, ChunkSize + 2, ChunkSize / 2);
                Debug.Draw3DText($"Chunk ({pos.X}, {pos.Z})", chunkCenter.ToVector3(), Color.White);
            }
        }
    }
}
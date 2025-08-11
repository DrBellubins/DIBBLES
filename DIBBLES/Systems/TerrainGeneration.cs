using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Utils;

namespace DIBBLES.Systems;

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
    
    private void GenerateTerrain(Vector3 centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3> chunksToGenerate = new List<Vector3>();
        
        for (int cx = (int)centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        for (int cy = (int)centerChunk.Y - halfRenderDistance; cy <= centerChunk.Y + halfRenderDistance; cy++)
        for (int cz = (int)centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
        {
            Vector3 chunkPos = new Vector3(cx * ChunkSize, cy * ChunkSize, cz * ChunkSize);
            
            if (!Chunks.ContainsKey(chunkPos))
                chunksToGenerate.Add(chunkPos);
        }
    
        foreach (var pos in chunksToGenerate)
        {
            var chunk = new Chunk(pos);
            GenerateChunkData(chunk);
            Lighting.Generate(chunk);

            Chunks[pos] = chunk;
            chunk.Model = TMesh.Generate(chunk);

            // Update lighting for neighbors
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (Math.Abs(dx) + Math.Abs(dz) != 1) continue; // only direct neighbors
                
                Vector3 neighborPos = pos + new Vector3(dx * ChunkSize, dy * ChunkSize, dz * ChunkSize);

                if (Chunks.TryGetValue(neighborPos, out var neighborChunk))
                    Lighting.Generate(neighborChunk);
            }
        }
    }
    
    public static void GenerateChunkData(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    var worldPos = chunk.Position + new Vector3(x, y, z);
                    var noise = Noise.GetNoise(chunk.Position.X + x,  chunk.Position.Y + y, chunk.Position.Z + z) + 0.5f * 0.5f;
                    
                    if (noise < 0.5f)
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Air]);
                    else
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Grass]);
                }
            }
        }
    }
    
    /*public static void GenerateChunkData(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                var amplitude = 10f;
                var baseHeight = ChunkSize * 0.5f;
                var height = Noise.GetNoise(chunk.Position.X + x, chunk.Position.Z + z) * amplitude;
                
                int terrainHeight = (int)baseHeight + (int)height;

                for (int y = 0; y < ChunkSize; y++)
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
    }*/
    
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
using System.Collections.Concurrent;
using DIBBLES.Gameplay.Player;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Terrain;

public class TerrainGenerationNew
{
    public const int RenderDistance = 12;
    public const int ChunkSize = 16;
    public const float ReachDistance = 5f; // Has to be finite!
    
    public static int Seed = -1413840509;
    
    public static readonly ConcurrentDictionary<Vector3Int, Chunk> ECSChunks = new();
    
    public static TerrainMesh Mesh = new();
    public static TerrainLighting Lighting = new();
    public static TerrainGameplay Gameplay = new();
    
    public static Effect terrainShader;
    
    private Vector3Int lastCameraChunk = Vector3Int.One; // Needs to != zero for first gen
    
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
        
        terrainShader = Engine.Instance.Content.Load<Effect>("Shaders/Terrain");
    }

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
            
            // Stage new chunks for generation.
        }
    }

    public void Draw()
    {
        
    }
}
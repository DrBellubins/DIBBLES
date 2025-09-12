using System.Numerics;
using System.Text;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using DIBBLES.Terrain;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Systems;

public struct SaveData
{
    public int Seed;
    public string? WorldName;
    public Vector3 PlayerPosition;
    public Vector3 CameraDirection;
    public int HotbarPosition;

    public Dictionary<Vector3Int, Chunk> ModifiedChunks = new ();

    public SaveData()
    {
        Seed = 0;
        WorldName = "";
        PlayerPosition = Vector3.Zero;
        CameraDirection = Vector3.Zero;
        HotbarPosition = 0;
    }
}

public class WorldSave
{
    // Public API
    public static string SavesDirectory = Path.Combine(AppContext.BaseDirectory, "Saves");
    public static SaveData Data = new();
    
    public static bool Exists = false;

    public static void Initialize()
    {
        if (!Directory.Exists(SavesDirectory))
            Directory.CreateDirectory(SavesDirectory);
    }
    
    // TODO: Only save world data that has changed
    public static void SaveWorldData(string worldName)
    {
        var currentSaveDir = Path.Combine(SavesDirectory, $"{worldName}");
        var regionsDir = Path.Combine(currentSaveDir, "Regions");
        var worldDataDir = Path.Combine(currentSaveDir, "WorldData.dat");
        var playerDataDir = Path.Combine(currentSaveDir, "PlayerData.dat");
        
        if (!Directory.Exists(currentSaveDir))
            Directory.CreateDirectory(currentSaveDir);
        
        if (!Directory.Exists(regionsDir))
            Directory.CreateDirectory(regionsDir);

        // World data
        using (var stream = File.Open(worldDataDir, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(GameScene.TerrainGen.Seed);
            }
        }
        
        // Player data
        using (var stream = File.Open(playerDataDir, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(GameScene.PlayerCharacter.Position.X);
                writer.Write(GameScene.PlayerCharacter.Position.Y);
                writer.Write(GameScene.PlayerCharacter.Position.Z);
                
                writer.Write(GameScene.PlayerCharacter.CameraForward.X);
                writer.Write(GameScene.PlayerCharacter.CameraForward.Y);
                writer.Write(GameScene.PlayerCharacter.CameraForward.Z);
                
                writer.Write(Data.HotbarPosition);
            }
        }
        
        // Regions
        
        foreach (var chunk in Data.ModifiedChunks)
        {
            var nonAirBlocks = new List<Block>();
            
            for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
            for (int z = 0; z < ChunkSize; z++)
            {
                var block = chunk.Value.GetBlock(x, y, z);
                
                if (block.Type != BlockType.Air)
                    nonAirBlocks.Add(block);
            }
            
            using (var stream = File.Open(Path.Combine(regionsDir, $"Region_{chunk.Key.ToStringUnderscore()}.dat"), FileMode.Create))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
                {
                    writer.Write(nonAirBlocks.Count);
                    
                    foreach (var block in nonAirBlocks)
                    {
                        writer.Write(block.Position.X);
                        writer.Write(block.Position.Y);
                        writer.Write(block.Position.Z);
                        writer.Write((int)block.Type);
                        writer.Write((int)block.Biome);
                        writer.Write(block.GeneratedInsideIsland);
                    }
                }
            }
        }
    }
    
    public static void LoadWorldData(string worldName)
    {
        var currentSaveDir = Path.Combine(SavesDirectory, $"{worldName}");
        var regionsDir = Path.Combine(currentSaveDir, "Regions");
        var worldDataDir = Path.Combine(currentSaveDir, "WorldData.dat");
        var playerDataDir = Path.Combine(currentSaveDir, "PlayerData.dat");

        if (!Directory.Exists(currentSaveDir))
        {
            Console.WriteLine($"Error: save directory '{currentSaveDir}' doesn't exist");
            return;
        }


        if (!Directory.Exists(regionsDir))
        {
            Console.WriteLine($"Error: region directory '{regionsDir}' doesn't exist");
            return;
        }

        // World data
        if (File.Exists(worldDataDir))
        {
            Exists = true;
            
            using (var stream = File.Open(worldDataDir, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    Data.WorldName = worldName;
                    Data.Seed = reader.ReadInt32();
                }
            }
        }
        
        // Player data
        if (File.Exists(playerDataDir))
        {
            using (var stream = File.Open(playerDataDir, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    Data.PlayerPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Data.CameraDirection = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Data.HotbarPosition = reader.ReadInt32();
                }
            }
        }
        else
        {
            Console.WriteLine($"Error: Player data file '{playerDataDir}' doesn't exist");
            return;
        }
        
        // Regions
        var regionPaths = Directory.GetFiles(regionsDir, "*.dat");
        
        for (int i = 0; i < regionPaths.Length; i++)
        {
            using (var stream = File.Open(regionPaths[i], FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    var currentChunkInfo = new ChunkInfo();
                    currentChunkInfo.Generated = reader.ReadBoolean();
                    currentChunkInfo.Modified = reader.ReadBoolean();

                    var currentChunk = new Chunk(new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
                    currentChunk.Info = currentChunkInfo;

                    for (int x = 0; x < ChunkSize; x++)
                    {
                        for (int y = 0; y < ChunkSize; y++)
                        {
                            for (int z = 0; z < ChunkSize; z++)
                            {
                                var type = (BlockType)reader.ReadInt32();
                                var info = BlockData.Prefabs[type];
                                        
                                var currentBlock = new Block()
                                {
                                    Type = type,
                                    Position = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                                    Info = info
                                };
                                    
                                currentBlock.Biome = (TerrainBiome)reader.ReadInt32();
                                currentBlock.GeneratedInsideIsland = reader.ReadBoolean();
                                    
                                currentChunk.SetBlock(x, y, z, currentBlock);
                            }
                        }
                    }

                    Data.ModifiedChunks.Add(currentChunk.Position, currentChunk);
                }
            }
        }
        
        Console.WriteLine($"Loaded world '{Data.WorldName}'");
        Console.WriteLine($"Data: Seed '{Data.Seed}' PlayerPos '{Data.PlayerPosition}' CamDir '{Data.CameraDirection}' Hotbar '{Data.HotbarPosition}' chunkCount '{regionPaths.Length}'");
    }
}
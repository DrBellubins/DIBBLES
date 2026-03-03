using Microsoft.Xna.Framework;
using System.Text;
using DIBBLES.Gameplay.Inventory;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using DIBBLES.Terrain;
using DIBBLES.Terrain.Biomes;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Systems;

// TODO: Saves were corrupted when shifting player pos to doubles, seems fixed.
// But not certain yet.
public struct SaveData
{
    public int Seed;
    public string? WorldName;
    public GVec3 PlayerPosition;
    public Vector3 CameraDirection;
    public int HotbarPosition;
    public ItemSlot[,] PlayerItemSlots;
    public ItemSlot[] HotbarItemSlots;

    public Dictionary<Vector3Int, Chunk> ModifiedChunks = new ();

    public SaveData()
    {
        Seed = 0;
        WorldName = "";
        PlayerPosition = GVec3.Zero;
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
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("DIBW"));
            writer.Write(Seed);
        }
        
        // Player data
        using (var stream = File.Open(playerDataDir, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("DIBP"));
                
                writer.Write(PlayerManager.Current.Position.X);
                writer.Write(PlayerManager.Current.Position.Y);
                writer.Write(PlayerManager.Current.Position.Z);
                
                writer.Write(PlayerManager.Current.CameraForward.X);
                writer.Write(PlayerManager.Current.CameraForward.Y);
                writer.Write(PlayerManager.Current.CameraForward.Z);
                
                writer.Write(Data.HotbarPosition);

                // Main player inventory slots
                for (int x = 0; x < Data.PlayerItemSlots.GetLength(0); x++)
                {
                    for (int y = 0; y < Data.PlayerItemSlots.GetLength(1); y++)
                    {
                        var itemSlot = Data.PlayerItemSlots[x, y];
                        
                        writer.Write((int)itemSlot.Type);
                        writer.Write(itemSlot.StackAmount);
                    }
                }
                
                // Hotbar slots
                for (var i = 0; i < Data.HotbarItemSlots.Length; i++)
                {
                    var itemSlot = Data.HotbarItemSlots[i];
                    
                    writer.Write((int)itemSlot.Type);
                    writer.Write(itemSlot.StackAmount);
                }
            }
        }
        
        // Regions
        foreach (var chunk in Data.ModifiedChunks)
        {
            var nonAirBlocks = 0;
            
            for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
            for (int z = 0; z < ChunkSize; z++)
            {
                var blockType = chunk.Value.GetTypeAt(x, y, z);

                if (blockType != BlockType.Air)
                    nonAirBlocks++;
            }
            
            using (var stream = File.Open(Path.Combine(regionsDir, $"Region_{chunk.Key.ToStringUnderscore()}.dat"), FileMode.Create))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("DIBR"));
                
                writer.Write(nonAirBlocks);
                
                for (int x = 0; x < ChunkSize; x++)
                for (int y = 0; y < ChunkSize; y++)
                for (int z = 0; z < ChunkSize; z++)
                {
                    var blockType = chunk.Value.GetTypeAt(x, y, z);
                    
                    if (blockType == BlockType.Air) continue;
                    
                    var blockBiome = chunk.Value.GetBiomeAt(x, y, z);
                    
                    writer.Write(x);
                    writer.Write(y);
                    writer.Write(z);
                    writer.Write((int)blockType);
                    writer.Write((int)blockBiome);
                }
            }
        }
        
        Debug.Info($"Saved world `{worldName}`");
    }
    
    public static void LoadWorldData(string worldName)
    {
        var currentSaveDir = Path.Combine(SavesDirectory, $"{worldName}");
        var regionsDir = Path.Combine(currentSaveDir, "Regions");
        var worldDataDir = Path.Combine(currentSaveDir, "WorldData.dat");
        var playerDataDir = Path.Combine(currentSaveDir, "PlayerData.dat");
    
        if (!Directory.Exists(currentSaveDir))
        {
            Debug.Warning($"Save directory '{currentSaveDir}' doesn't exist");
            return;
        }
    
        if (!Directory.Exists(regionsDir))
        {
            Debug.Error($"Region directory '{regionsDir}' doesn't exist");
            return;
        }
    
        // World data
        if (File.Exists(worldDataDir))
        {
            Exists = true;
    
            using (var stream = File.Open(worldDataDir, FileMode.Open))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
            {
                var header = Encoding.ASCII.GetString(reader.ReadBytes(4));

                if (header != "DIBW")
                    Debug.Error("World data format is incorrect");
                
                Data.WorldName = worldName;
                Data.Seed = reader.ReadInt32();
            }
        }
    
        // Player data
        if (File.Exists(playerDataDir))
        {
            using (var stream = File.Open(playerDataDir, FileMode.Open))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
            {
                var header = Encoding.ASCII.GetString(reader.ReadBytes(4));

                if (header != "DIBP")
                    Debug.Error("Player data format is incorrect");
                
                Data.PlayerPosition = new GVec3(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                Data.CameraDirection = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Data.HotbarPosition = reader.ReadInt32();

                // Main player inventory slots
                Data.PlayerItemSlots = new ItemSlot[9, 3];
                
                for (int x = 0; x < 9; x++)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        var itemSlot = new ItemSlot();
                        
                        itemSlot.Type = (BlockType)reader.ReadInt32();
                        itemSlot.StackAmount = reader.ReadInt32();
                        
                        Data.PlayerItemSlots[x, y] = itemSlot;
                    }
                }
                
                // Hotbar slots
                Data.HotbarItemSlots = new ItemSlot[9];
                
                for (var i = 0; i < 9; i++)
                {
                    var itemSlot = new ItemSlot();
                    
                    itemSlot.Type = (BlockType)reader.ReadInt32();
                    itemSlot.StackAmount = reader.ReadInt32();
                    
                    Data.HotbarItemSlots[i] = itemSlot;
                }
            }
        }
        else
        {
            Debug.Error($"Player data file '{playerDataDir}' doesn't exist");
            return;
        }
    
        // Regions (sparse voxel loading)
        var regionPaths = Directory.GetFiles(regionsDir, "*.dat");
    
        for (int i = 0; i < regionPaths.Length; i++)
        {
            using (var stream = File.Open(regionPaths[i], FileMode.Open))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
            {
                // Get chunk position from filename
                var fileName = Path.GetFileNameWithoutExtension(regionPaths[i]);
                var coords = fileName.Replace("Region_", "").Split('_');
                
                var chunkPos = new Vector3Int(
                    int.Parse(coords[0]),
                    int.Parse(coords[1]),
                    int.Parse(coords[2])
                );
    
                var chunk = new Chunk(chunkPos);
    
                // Fill chunk blocks with air
                for (int x = 0; x < ChunkSize; x++)
                for (int y = 0; y < ChunkSize; y++)
                for (int z = 0; z < ChunkSize; z++)
                    chunk.SetTypeAt(x, y, z, BlockType.Air);
                
                var header = Encoding.ASCII.GetString(reader.ReadBytes(4));
                
                if (header != "DIBR")
                    Debug.Error("Region data format is incorrect!");
                
                int nonAirCount = reader.ReadInt32();
    
                for (int b = 0; b < nonAirCount; b++)
                {
                    int x = reader.ReadInt32();
                    int y = reader.ReadInt32();
                    int z = reader.ReadInt32();
                    
                    var type = (BlockType)reader.ReadInt32();
                    var biome = (TerrainBiome)reader.ReadInt32();
    
                    chunk.SetTypeAt(x, y, z, type);
                    chunk.SetBiomeAt(x, y, z, biome);
                }
                
                // Set stage to just before lighting
                chunk.GenerationStage = ChunkGenerationStage.Decorations;
                chunk.IsModified = true;
    
                Data.ModifiedChunks.Add(chunk.Position, chunk);
            }
        }
    
        Debug.Info($"Loaded world '{Data.WorldName}'");
        Debug.Info($"Data: Seed '{Data.Seed}' PlayerPos '{Data.PlayerPosition}' CamDir '{Data.CameraDirection}' Hotbar '{Data.HotbarPosition}' chunkCount '{regionPaths.Length}'");
    }
}
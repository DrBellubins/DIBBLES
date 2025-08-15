using System.Numerics;
using System.Text;
using DIBBLES.Utils;
using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

public struct SaveData
{
    public int Seed;
    public string? WorldName;
    public Vector3 PlayerPosition;
    public int HotbarPosition;

    public Dictionary<Vector3Int, Chunk> ModifiedChunks = new ();

    public SaveData()
    {
        Seed = 0;
        WorldName = "";
        PlayerPosition = Vector3.Zero;
        HotbarPosition = 0;
    }
}

public class WorldSave
{
    // Public API
    public static string SaveDirectory = Path.Combine(AppContext.BaseDirectory, "Saves");
    public static SaveData Data = new SaveData();
    
    public static bool Exists = false;

    public static void Initialize()
    {
        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }

    public static void SaveWorldData(string worldName)
    {
        using (var stream = File.Open(Path.Combine(SaveDirectory, $"{worldName}.dat"), FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                // World data
                writer.Write(Data.Seed);
                writer.Write(worldName);
                    
                writer.Write(Data.PlayerPosition.X);
                writer.Write(Data.PlayerPosition.Y);
                writer.Write(Data.PlayerPosition.Z);
                    
                writer.Write(Data.HotbarPosition);

                // Chunk data
                writer.Write(Data.ModifiedChunks.Count);

                foreach (var chunk in Data.ModifiedChunks)
                {
                    writer.Write(chunk.Value.Info.Generated);
                    writer.Write(chunk.Value.Info.Modified);
                    writer.Write(chunk.Value.Position.X);
                    writer.Write(chunk.Value.Position.Y);
                    writer.Write(chunk.Value.Position.Z);

                    for (int x = 0; x < ChunkSize; x++)
                    {
                        for (int y = 0; y < ChunkSize; y++)
                        {
                            for (int z = 0; z < ChunkSize; z++)
                            {
                                var currentBlock = chunk.Value.Blocks[x, y, z];
                                    
                                writer.Write((int)currentBlock.Info.Type);
                                    
                                writer.Write(currentBlock.Position.X);
                                writer.Write(currentBlock.Position.Y);
                                writer.Write(currentBlock.Position.Z);
                            }
                        }
                    }
                }

                Console.WriteLine($"Saved world '{worldName}'");
            }
        }
    }

    public static void LoadWorldData(string worldName)
    {
        var fileName = Path.Combine(SaveDirectory, $"{worldName}.dat");

        if (File.Exists(fileName))
        {
            Exists = true;
            
            try
            {
                using (var stream = File.Open(fileName, FileMode.Open))
                {
                    using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                    {
                        // World data
                        Data.Seed = reader.ReadInt32();
                        Data.WorldName = reader.ReadString();
                        Data.PlayerPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        Data.HotbarPosition = reader.ReadInt32();

                        // Chunk data
                        Data.ModifiedChunks.Clear(); // THIS PREVENTS CHUNK DUPING!

                        var modifiedChunkCount = reader.ReadInt32();

                        for (int i = 0; i < modifiedChunkCount; i++)
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
                                        var info = Block.Prefabs[type];
                                        var currentBlock = new Block();

                                        currentBlock.Position = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

                                        currentBlock.Info = info;

                                        currentChunk.Blocks[x, y, z] = currentBlock;
                                    }
                                }
                            }

                            Data.ModifiedChunks.Add(currentChunk.Position, currentChunk);
                        }

                        Console.WriteLine($"Loaded world '{Data.WorldName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
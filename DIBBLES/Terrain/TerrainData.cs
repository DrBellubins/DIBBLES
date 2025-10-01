using System.Text.Json;
using System.Text.Json.Serialization;
using NVorbis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Utils;
using Tommy;

namespace DIBBLES.Terrain;

// Only set for block prefabs once at start!
public struct BlockInfo
{
    public int Hardness { get; set; } // 0 to 10 (10 being unbreakable)
    public float Thickness { get; set; } // 0 to 1 (Used for slowing player down)
    public int MaxStack { get; set; }
    public bool IsTransparent { get; set; } // True if light can pass through
    public byte LightEmission { get; set; } // Light level emitted by this block (0-15)
    
    // Key = FaceIdx
    public Dictionary<int, RectangleF>? FaceUVs; // Used for per-face texturing, if null, use same texture for all faces.
    
    public BlockInfo(int hardness, float thickness, int maxStack, bool isTransparent = false, byte lightEmission = 0)
    {
        Hardness = hardness;
        Thickness = thickness;
        MaxStack = maxStack;
        IsTransparent = isTransparent;
        LightEmission = lightEmission;
    }
}

public class BlockData
{
    public static readonly Dictionary<BlockType, BlockInfo> Prefabs = new();
    public static readonly Dictionary<(BlockType, int), Texture2D> Textures = new();
    public static readonly Dictionary<BlockType, BlockSounds> Sounds = new();
    
    public static Texture2D TextureAtlas; // Store the atlas
    public static Dictionary<BlockType, RectangleF> AtlasUVs = new(); // Store UV mappings
    
    public static void InitializeBlockPrefabs()
    {
        // Initialize block prefabs
        loadBlockPrefabsFromToml(Path.Combine(AppContext.BaseDirectory, "Assets", "Blocks.toml"));
        
        // Define block types in the exact order for the atlas
        var atlasBlockTypes = new List<BlockType>();

        foreach (BlockType blockType in Enum.GetValues<BlockType>())
        {
            // Ignore textures that shouldn't be in the atlas here.
            if (blockType != BlockType.Air && blockType != BlockType.Water)
                atlasBlockTypes.Add(blockType);
        }
        
        List<Texture2D> tempTextures = new List<Texture2D>();
        
        int maxWidth = 0;
        int maxHeight = 0;

        // Load textures for atlas in specified order and calculate max dimensions
        foreach (BlockType blockType in atlasBlockTypes)
        {
            var texture = loadBlockTexture(blockType);

            tempTextures.Add(texture);
            Textures.Add(blockType, texture); // Also store in Textures for reference
            
            maxWidth = Math.Max(maxWidth, texture.Width);
            maxHeight = Math.Max(maxHeight, texture.Height);
        }

        // Load sounds
        foreach (BlockType blockType in Enum.GetValues<BlockType>())
        {
            if (blockType != BlockType.Air && blockType != BlockType.Water)
            {
                var blockSounds = new BlockSounds();

                for (int i = 0; i < 4; i++)
                    blockSounds.Sounds[i] = loadBlockSounds(blockType, i);

                Sounds.Add(blockType, blockSounds);
            }
        }

        // Create texture atlas in a 5x1 layout
        // 1. Get your block types (skip air/water)
        var blockTypes = Enum.GetValues(typeof(BlockType))
            .Cast<BlockType>()
            .Where(t => t != BlockType.Air && t != BlockType.Water)
            .ToArray();

        // 2. Call the generator
        var result = AtlasGenerator.GenerateBlockAtlas(
            Engine.Graphics,
            blockTypes,
            Textures,
            16 // or your tile size
        );

        // 3. Assign in BlockData
        TextureAtlas = result.AtlasTexture;
        AtlasUVs = result.BlockUVs;
    }
    
    private static Texture2D loadBlockTexture(BlockType blockType)
    {
        return Resource.Load<Texture2D>($"{blockType.ToString()}.png");
    }
    
    private static SoundEffect loadBlockSounds(BlockType blockType, int index)
    {
        var i = index + 1; // Sounds start at 1
        var blockName = blockType.ToString();
        var blockSoundPath = Path.Combine(blockName, $"{blockName}{i}.ogg");
        
        return Resource.Load<SoundEffect>(blockSoundPath);
    }
    
    public static void loadBlockPrefabsFromToml(string tomlPath)
    {
        if (!File.Exists(tomlPath))
            throw new FileNotFoundException($"TOML file '{tomlPath}' not found.");

        using var reader = new StreamReader(tomlPath);
        var toml = TOML.Parse(reader);

        Prefabs.Clear();

        foreach (BlockType type in Enum.GetValues(typeof(BlockType)))
        {
            if (!toml.HasKey(type.ToString()))
                continue; // Skip missing blocks

            var table = toml[type.ToString()].AsTable;
            
            if (table == null)
                continue; // Not a table

            int hardness = table.HasKey("Hardness") ? (int)table["Hardness"].AsInteger.Value : 0;
            float thickness = table.HasKey("Thickness") ? (float)table["Thickness"].AsFloat.Value : 0f;
            int maxStack = table.HasKey("MaxStack") ? (int)table["MaxStack"].AsInteger.Value : 0;
            bool isTransparent = table.HasKey("IsTransparent") ? table["IsTransparent"].AsBoolean.Value : false;
            byte lightEmission = table.HasKey("LightEmission") ? (byte)table["LightEmission"].AsInteger.Value : (byte)0;

            Prefabs[type] = new BlockInfo(hardness, thickness, maxStack, isTransparent, lightEmission);
        }
    }
}

public class BlockSounds
{
    public SoundEffect[] Sounds = new SoundEffect[4];

    /// <summary>
    /// Get random sound from array
    /// </summary>
    public SoundEffect RND
    {
        get { return Sounds[new Random().Next(0, 3)]; }
    }
}
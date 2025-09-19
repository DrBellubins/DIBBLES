using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Audio;
using SixLabors.ImageSharp;
using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

// Only set for block prefabs once at start!
public struct BlockInfo
{
    public int Hardness; // 0 to 10 (10 being unbreakable)
    public float Thickness; // 0 to 1 (Used for slowling player down)
    public int MaxStack;
    public bool IsTransparent; // True if light can pass through
    public byte LightEmission; // Light level emitted by this block (0-15)
    
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
    public static Dictionary<BlockType, BlockInfo> Prefabs = new();
    public static Dictionary<BlockType, Texture2D> Textures = new();
    public static Dictionary<BlockType, BlockSounds> Sounds = new();
    
    public static Texture2D TextureAtlas; // Store the atlas
    public static Dictionary<BlockType, RectangleF> AtlasUVs = new(); // Store UV mappings
    
    public static void InitializeBlockPrefabs()
    {
        // Initialize block prefabs with transparency and light emission
        Prefabs.Add(BlockType.Dirt, new BlockInfo(2, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Grass, new BlockInfo(2, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Stone, new BlockInfo(4, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Sand, new BlockInfo(1, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Snow, new BlockInfo(1, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Wood, new BlockInfo(3, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.WoodLog, new BlockInfo(3, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Leaves, new BlockInfo(1, 0.0f, 64, true, 0));
        Prefabs.Add(BlockType.Glass, new BlockInfo(2, 0.0f, 64, true, 0));
        Prefabs.Add(BlockType.Feeb, new BlockInfo(2, 0.0f, 64, false, 0));
        
        // Special blocks (No logic
        Prefabs.Add(BlockType.Air, new BlockInfo(0, 0.0f, 0, true, 0));
        Prefabs.Add(BlockType.Water, new BlockInfo(10, 0.5f, 64, true, 0));
        Prefabs.Add(BlockType.Wisp, new BlockInfo(10, 0.5f, 64, true, 15));
        
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

            if (texture == null) // Check if texture failed to load
                texture = Resource.Load<Texture2D>("Error.png");

            tempTextures.Add(texture);
            Textures.Add(blockType, texture); // Also store in Textures for reference
            
            maxWidth = Math.Max(maxWidth, texture.Width);
            maxHeight = Math.Max(maxHeight, texture.Height);
        }

        // Load textures and sounds for remaining block types (Air, Water)
        /*foreach (BlockType blockType in Enum.GetValues<BlockType>())
        {
            if (blockType != BlockType.Air && blockType != BlockType.Water)
            {
                if (!atlasBlockTypes.Contains(blockType))
                {
                    var texture = loadBlockTexture(blockType);
                    Textures.Add(blockType, texture);
                }

                var blockSounds = new BlockSounds();

                for (int i = 0; i < 4; i++)
                    blockSounds.Sounds[i] = loadBlockSounds(blockType, i);

                Sounds.Add(blockType, blockSounds);
            }
        }*/

        // Create texture atlas in a 5x1 layout
        // 1. Get your block types (skip air/water)
        var blockTypes = Enum.GetValues(typeof(BlockType))
            .Cast<BlockType>()
            .Where(t => t != BlockType.Air && t != BlockType.Water)
            .ToArray();

        // 2. Call the generator
        var result = AtlasGenerator.GenerateBlockAtlas(
            MonoEngine.Graphics,
            Path.Combine(AppContext.BaseDirectory, "Assets/Textures/Blocks"),
            blockTypes,
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
        return Resource.Load<SoundEffect>(Path.Combine(blockName, $"{blockName}{i}.ogg"));
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
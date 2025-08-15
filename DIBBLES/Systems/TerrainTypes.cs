using System.Numerics;
using DIBBLES.Utils;
using Raylib_cs;

using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

public enum BlockType
{
    Air,
    Dirt,
    Grass,
    Stone,
    Sand,
    Snow,
    Water,
    Wisp
}

public enum TerrainBiome
{
    Flatland,
    Plains,
    Ocean,
    Beach,
    Mountains,
    Forest,
    Snowlands
}

public class ChunkInfo
{
    public bool Generated; // Runtime quality-of-life check
    public bool Modified;
}

public class Chunk
{
    public Vector3Int Position;
    public Block[,,] Blocks;
    public ChunkInfo Info;
    public Model Model;

    public Chunk(Vector3Int pos)
    {
        Position = pos;
        Blocks = new Block[ChunkSize, ChunkSize, ChunkSize];
        Info =  new ChunkInfo();
    }
}

public class BlockInfo
{
    public BlockType Type;
    public TerrainBiome Biome;
    public int Hardness; // 0 to 10 (10 being unbreakable)
    public float Thickness; // 0 to 1 (Used for slowling player down)
    public int MaxStack;
    public bool IsTransparent; // True if light can pass through
    public byte LightEmission; // Light level emitted by this block (0-15)

    public BlockInfo(BlockType type, int hardness, float thickness, int maxStack, bool isTransparent = false, byte lightEmission = 0)
    {
        Type = type;
        Hardness = hardness;
        Thickness = thickness;
        MaxStack = maxStack;
        IsTransparent = isTransparent;
        LightEmission = lightEmission;
    }
}

public class Block
{
    public Vector3Int Position;
    public BlockInfo Info;
    public byte LightLevel; // Block light level (0-15)
    
    public static Dictionary<BlockType, BlockInfo> Prefabs = new Dictionary<BlockType, BlockInfo>();
    public static Dictionary<BlockType, Texture2D> Textures = new Dictionary<BlockType, Texture2D>();
    public static Dictionary<BlockType, BlockSounds> Sounds = new Dictionary<BlockType, BlockSounds>();
    
    public static Texture2D TextureAtlas; // Store the atlas
    public static Dictionary<BlockType, Rectangle> AtlasUVs = new Dictionary<BlockType, Rectangle>(); // Store UV mappings
    
    public Block()
    {
        Position = Vector3Int.Zero;
        Info = new BlockInfo(BlockType.Dirt, 2, 0.0f, 64);
        LightLevel = 0;
    }

    public Block(Vector3Int position, BlockInfo info)
    {
        Position = position;
        Info = info;
        LightLevel = info.LightEmission; // Set initial block light from emission
    }
    
    public static void InitializeBlockPrefabs()
    {
        // Initialize block prefabs with transparency and light emission
        Prefabs.Add(BlockType.Air, new BlockInfo(BlockType.Air, 0, 0.0f, 0, true, 0));
        Prefabs.Add(BlockType.Dirt, new BlockInfo(BlockType.Dirt, 2, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Grass, new BlockInfo(BlockType.Grass, 2, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Stone, new BlockInfo(BlockType.Stone, 4, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Sand, new BlockInfo(BlockType.Sand, 1, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Snow, new BlockInfo(BlockType.Snow, 1, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Water, new BlockInfo(BlockType.Water, 10, 0.5f, 64, true, 0));
        Prefabs.Add(BlockType.Wisp, new BlockInfo(BlockType.Wisp, 10, 0.5f, 64, true, 15));

        // Define block types in the exact order for the atlas
        var atlasBlockTypes = new List<BlockType>();

        foreach (BlockType blockType in Enum.GetValues<BlockType>())
        {
            // Add textures that shouldn't be in the atlas here.
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

            if (texture.Id == 0) // Check if texture failed to load
                texture = Resource.Load<Texture2D>("error.png");
            
            tempTextures.Add(texture);
            Textures.Add(blockType, texture); // Also store in Textures for reference
            
            maxWidth = Math.Max(maxWidth, texture.Width);
            maxHeight = Math.Max(maxHeight, texture.Height);
        }

        // Load textures and sounds for remaining block types (Air, Water)
        foreach (BlockType blockType in Enum.GetValues<BlockType>())
        {
            if (blockType != BlockType.Air  && blockType != BlockType.Water)
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
        }

        // Create texture atlas in a 5x1 layout
        if (tempTextures.Count > 0)
        {
            int textureCount = tempTextures.Count; // Should be 5 (Dirt, Grass, Stone, Sand, Snow)
            int atlasWidth = maxWidth * textureCount; // 5 textures in a single row
            int atlasHeight = maxHeight;

            RenderTexture2D atlasRenderTexture = Raylib.LoadRenderTexture(atlasWidth, atlasHeight);
            Raylib.BeginTextureMode(atlasRenderTexture);
            Raylib.ClearBackground(new Color(0, 0, 0, 0)); // Transparent background

            for (int index = 0; index < textureCount; index++)
            {
                BlockType blockType = atlasBlockTypes[index];
                
                int x = index * maxWidth; // Place textures side by side
                int y = 0;

                // Define source rectangle to flip the texture vertically
                var sourceRect = new Rectangle(0, 0, tempTextures[index].Width, -tempTextures[index].Height);
                var destRect = new Rectangle(x, y, tempTextures[index].Width, tempTextures[index].Height);
                
                // Draw texture at the correct position
                Raylib.DrawTexturePro(tempTextures[index], sourceRect, destRect, new Vector2(0, 0), 0.0f, Color.White);

                // Calculate UV coordinates for a 5x1 atlas
                float uMin = (float)(index * maxWidth) / atlasWidth;
                float uMax = (float)((index + 1) * maxWidth) / atlasWidth;
                float vMin = 0.0f;
                float vMax = 1.0f;

                // Store UV coordinates
                AtlasUVs[blockType] = new Rectangle(uMin, vMin, uMax - uMin, vMax - vMin);
            }

            Raylib.EndTextureMode();

            // Generate atlas mipmaps
            var atlasImage = Raylib.LoadImageFromTexture(atlasRenderTexture.Texture);
            var atlasTexture = Raylib.LoadTextureFromImage(atlasImage);
            
            // Export atlas for debugging
            //Raylib.ExportImage(atlasImage, "atlas_debug.png");
            
            Raylib.GenTextureMipmaps(ref atlasTexture);
            Raylib.SetTextureFilter(atlasTexture, TextureFilter.Point);
            
            Raylib.UnloadImage(atlasImage);
            Raylib.UnloadRenderTexture(atlasRenderTexture);
            
            TextureAtlas = atlasTexture;

            // Unload temporary textures
            foreach (var texture in tempTextures)
            {
                //if (texture.Id != 0)
                //    Raylib.UnloadTexture(texture);
            }
        }
    }
    
    private static Texture2D loadBlockTexture(BlockType blockType)
    {
        return Resource.Load<Texture2D>($"{blockType.ToString().ToLower()}.png");
    }
    
    private static Sound loadBlockSounds(BlockType blockType, int index)
    {
        var i = index + 1; // Sounds start at 1
        var blockName = blockType.ToString().ToLower();
        return Resource.Load<Sound>(Path.Combine(blockName, $"{blockName}{i}.ogg"));
    }
}

public class BlockSounds
{
    public Sound[] Sounds = new Sound[4];

    /// <summary>
    /// Get random sound from array
    /// </summary>
    public Sound RND
    {
        get { return Sounds[new Random().Next(0, 3)]; }
    }
}
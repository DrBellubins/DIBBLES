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
    Water
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
    public Vector3 Position;
    public Block[,,] Blocks;
    public ChunkInfo Info;
    public Model Model;

    public Chunk(Vector3 pos)
    {
        Position = pos;
        Blocks = new Block[ChunkSize, ChunkHeight, ChunkSize];
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

    public BlockInfo() {}

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
    public Vector3 Position;
    public BlockInfo Info;
    public byte SkyLight;   // Sky light level (0-15)
    public byte BlockLight; // Block light level (0-15)
    
    public static Dictionary<BlockType, BlockInfo> Prefabs = new Dictionary<BlockType, BlockInfo>();
    public static Dictionary<BlockType, Texture2D> Textures = new Dictionary<BlockType, Texture2D>();
    public static Dictionary<BlockType, BlockSounds> Sounds = new Dictionary<BlockType, BlockSounds>();
    
    public static Texture2D TextureAtlas; // Store the atlas
    public static Dictionary<BlockType, Rectangle> AtlasUVs = new Dictionary<BlockType, Rectangle>(); // Store UV mappings
    
    // Helper property to get the effective light level (max of sky and block light)
    public byte LightLevel => (byte)Math.Max(SkyLight, BlockLight);
    
    public Block()
    {
        Position = Vector3.Zero;
        Info = new BlockInfo(BlockType.Dirt, 2, 0.0f, 64);
        SkyLight = 0;
        BlockLight = 0;
    }

    public Block(Vector3 position, BlockInfo info)
    {
        Position = position;
        Info = info;
        SkyLight = 0;
        BlockLight = info.LightEmission; // Set initial block light from emission
    }
    
    public static void InitializeBlockPrefabs()
    {
        // Initialize block prefabs with transparency and light emission
        Prefabs.Add(BlockType.Air, new BlockInfo(BlockType.Air, 0, 0.0f, 0, true, 0));
        Prefabs.Add(BlockType.Dirt, new BlockInfo(BlockType.Dirt, 2, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Grass, new BlockInfo(BlockType.Grass, 2, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Stone, new BlockInfo(BlockType.Stone, 4, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Sand, new BlockInfo(BlockType.Sand, 1, 0.0f, 64, false, 0));
        Prefabs.Add(BlockType.Snow, new BlockInfo(BlockType.Snow, 1, 0.0f, 64, false, 15));
        Prefabs.Add(BlockType.Water, new BlockInfo(BlockType.Water, 10, 0.5f, 64, true, 0));

        // Define block types in the exact order for the atlas
        BlockType[] atlasBlockTypes = { BlockType.Dirt, BlockType.Grass, BlockType.Stone, BlockType.Sand, BlockType.Snow };
        List<Texture2D> tempTextures = new List<Texture2D>();
        
        int maxWidth = 0;
        int maxHeight = 0;

        // Load textures for atlas in specified order and calculate max dimensions
        foreach (BlockType blockType in atlasBlockTypes)
        {
            var texture = loadBlockTexture(blockType);
            
            if (texture.Id == 0) // Check if texture failed to load
                texture = Raylib.LoadTexture("Assets/Textures/Blocks/error.png");
            
            tempTextures.Add(texture);
            Textures.Add(blockType, texture); // Also store in Textures for reference
            
            maxWidth = Math.Max(maxWidth, texture.Width);
            maxHeight = Math.Max(maxHeight, texture.Height);
        }

        // Load textures and sounds for remaining block types (Air, Water)
        foreach (BlockType blockType in Enum.GetValues(typeof(BlockType)))
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
            
            // Export atlas for debugging
            //Image atlasImage = Raylib.LoadImageFromTexture(atlasRenderTexture.Texture);
            //Raylib.ExportImage(atlasImage, "atlas_debug.png");
            //Raylib.UnloadImage(atlasImage);

            TextureAtlas = atlasRenderTexture.Texture;

            // Unload temporary textures
            foreach (var texture in tempTextures)
            {
                if (texture.Id != 0)
                    Raylib.UnloadTexture(texture);
            }
        }
    }
    
    private static Texture2D loadBlockTexture(BlockType blockType)
    {
        switch (blockType)
        {
            case BlockType.Air:
                return default;
            case BlockType.Dirt:
                return Raylib.LoadTexture("Assets/Textures/Blocks/dirt.png");
            case BlockType.Grass:
                return Raylib.LoadTexture("Assets/Textures/Blocks/grass.png");
            case BlockType.Stone:
                return Raylib.LoadTexture("Assets/Textures/Blocks/stone.png");
            case BlockType.Sand:
                return Raylib.LoadTexture("Assets/Textures/Blocks/sand.png");
            case BlockType.Snow:
                return Raylib.LoadTexture("Assets/Textures/Blocks/snow.png");
            case BlockType.Water:
                return Raylib.LoadTexture("Assets/Textures/Blocks/water.png");
            default:
                return Raylib.LoadTexture("Assets/Textures/Blocks/error.png");
        }
    }
    
    private static Sound loadBlockSounds(BlockType blockType, int index)
    {
        var i = index + 1;

        switch (blockType)
        {
            case BlockType.Air:
                return default;
            case BlockType.Dirt:
                return Raylib.LoadSound($"Assets/Sounds/Blocks/Dirt/dirt{i}.ogg");
            case BlockType.Grass:
                return Raylib.LoadSound($"Assets/Sounds/Blocks/Dirt/dirt{i}.ogg");
            case BlockType.Stone:
                return Raylib.LoadSound($"Assets/Sounds/Blocks/Stone/stone{i}.ogg");
            case BlockType.Sand:
                return Raylib.LoadSound($"Assets/Sounds/Blocks/Sand/sand{i}.ogg");
            case BlockType.Snow:
                return Raylib.LoadSound($"Assets/Sounds/Blocks/Snow/snow{i}.ogg");
            default:
                return Raylib.LoadSound($"Assets/Sounds/Blocks/Stone/stone{i}.ogg");
        }
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
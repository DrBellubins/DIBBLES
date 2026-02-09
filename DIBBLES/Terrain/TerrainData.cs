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
    public int Hardness;         // 0 to 10 (10 being unbreakable)
    public float Thickness;      // 0 to 1 (Used for slowing player down)
    public int MaxStack;
    public bool IsTransparent;   // True if light can pass through
    public byte LightEmission;   // Light level emitted by this block (0-15)
    
    // Key = FaceIdx
    public Dictionary<int, RectangleF>? FaceUVs; // Used for per-face texturing, if null, use same texture for all faces.
    public bool AntiTileUVsHorizontally;
    public bool AntiTileUVsVertically;
    
    public bool IsBillboard;
    
    public BlockInfo(int hardness, float thickness, int maxStack, bool isTransparent = false, byte lightEmission = 0, bool isBillboard = false)
    {
        Hardness = hardness;
        Thickness = thickness;
        MaxStack = maxStack;
        IsTransparent = isTransparent;
        LightEmission = lightEmission;
        IsBillboard = isBillboard;
    }
}

public class BlockData
{
    public static readonly Dictionary<BlockType, BlockInfo> Prefabs = new();
    public static readonly Dictionary<(BlockType, int), Texture2D> Textures = new();
    public static readonly Dictionary<(BlockType, int), Texture2D> EmissiveTextures = new();
    public static readonly Dictionary<BlockType, BlockSounds> Sounds = new();
    
    // Store the atlas
    public static Texture2D TextureAtlas = new(Engine.Graphics, 1, 1);
    public static Texture2D EmissiveTextureAtlas = new Texture2D(Engine.Graphics, 1, 1);
    
    // Store UV mappings
    public static Dictionary<(BlockType, int), RectangleF> AtlasUVs = new();
    public static readonly Dictionary<(BlockType, int), Vector2[]> FaceUVsOrdered = new();
    
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
        
        int maxWidth = 0;
        int maxHeight = 0;

        // Load textures for atlas in specified order and calculate max dimensions
        foreach (BlockType blockType in atlasBlockTypes)
        {
            var faceTextureNames = getFaceTextureNamesForBlock(blockType); // returns string[6] or null

            if (faceTextureNames != null)
            {
                for (int faceIdx = 0; faceIdx < 6; faceIdx++)
                {
                    var texture = Resource.Load<Texture2D>(faceTextureNames[faceIdx]);
                    Textures.Add((blockType, faceIdx), texture);
                    maxWidth = Math.Max(maxWidth, texture.Width);
                    maxHeight = Math.Max(maxHeight, texture.Height);
                }
            }
            else
            {
                var texture = loadBlockTexture(blockType);
                
                for (int faceIdx = 0; faceIdx < 6; faceIdx++)
                {
                    Textures.Add((blockType, faceIdx), texture);
                    maxWidth = Math.Max(maxWidth, texture.Width);
                    maxHeight = Math.Max(maxHeight, texture.Height);
                }
            }
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
        var blockTypes = Enum.GetValuesAsUnderlyingType(typeof(BlockType))
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
        
        // DEBUG: Save generated atlas to file
        using (var atlasPngStr = new FileStream(Path.Combine(AppContext.BaseDirectory, "Blocks.png"), FileMode.OpenOrCreate))
            TextureAtlas.SaveAsPng(atlasPngStr, TextureAtlas.Width, TextureAtlas.Height);
        
        using (var s = new FileStream(Path.Combine(AppContext.BaseDirectory, "EmissiveAtlas.png"), FileMode.Create))
            EmissiveTextureAtlas.SaveAsPng(s, EmissiveTextureAtlas.Width, EmissiveTextureAtlas.Height);
        
        FaceUVsOrdered.Clear();
        
        foreach (BlockType blockType in atlasBlockTypes)
        {
            // Ensure a per-face UV rect dictionary exists on the prefab
            var info = Prefabs[blockType];

            var faceRects = new Dictionary<int, RectangleF>();

            for (int faceIdx = 0; faceIdx < 6; faceIdx++)
            {
                RectangleF rect;

                if (!AtlasUVs.TryGetValue((blockType, faceIdx), out rect))
                    rect = new RectangleF(0, 0, 1, 1);

                faceRects[faceIdx] = rect;

                // Canonical BL, TL, TR, BR for this face's atlas sub-rect
                // (GetFaceUVs will now use faceIdx correctly)
                var faceUVs = FaceUtils.GetFaceUVs(blockType, faceIdx);

                // Apply any universal per-face fixups here
                // Top and bottom have their textures facing Z+
                switch (faceIdx)
                {
                    case 0: // Front (-Z)
                    {
                        faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 0, 1);
                        break;
                    }
                    case 1: // Back (+Z)
                    {
                        if (TerrainMesh.UseGreedyMeshing)
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 0, 1);
                        else
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 0, 0);
                        
                        break;
                    }
                    case 2: // Left (-X)
                    {
                        if (TerrainMesh.UseGreedyMeshing)
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 1, 1);
                        else
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 0, 1);
                        
                        break;
                    }
                    case 3: // Right (+X)
                    {
                        if (TerrainMesh.UseGreedyMeshing)
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 1, 2);
                        else
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 0, 0);
                        
                        break;
                    }
                    case 4: // Bottom (-Y)
                    {
                        if (TerrainMesh.UseGreedyMeshing)
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 0, 1);
                        else
                            faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 2, 0);
                        
                        break;
                    }
                    case 5: // Top (+Y)
                    {
                        faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 1, 1);
                        break;
                    }
                    default: // Unused
                    {
                        faceUVs = FaceUtils.ApplyUVTransform(faceUVs, faceIdx, 0, 0);
                        break;
                    }
                }

                // Store final ordered UVs that match GetFaceVertices() for this face
                FaceUVsOrdered[(blockType, faceIdx)] = faceUVs;
            }

            // Write back the per-face rects to the prefab (BlockInfo is a struct)
            info.FaceUVs = faceRects;
            Prefabs[blockType] = info;
        }
    }
    
    private static Texture2D loadBlockTexture(BlockType blockType)
    {
        return Resource.Load<Texture2D>($"{blockType.ToString()}.png");
    }
    
    private static string[]? getFaceTextureNamesForBlock(BlockType blockType)
    {
        // Path to your TOML config
        string tomlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Blocks.toml");
        
        if (!File.Exists(tomlPath))
            throw new FileNotFoundException($"TOML file '{tomlPath}' not found.");

        using var reader = new StreamReader(tomlPath);
        
        var toml = Tommy.TOML.Parse(reader);

        if (!toml.HasKey(blockType.ToString()))
            return null;

        var table = toml[blockType.ToString()].AsTable;
        
        //if (table == null)
        //    return null;

        // Check for a per-face texture array
        if (table.HasKey("FaceTextures"))
        {
            var arr = table["FaceTextures"].AsArray;
            var result = new string[6];
            int i = 0;

            foreach (var item in arr)
            {
                if (i >= 6) break;

                string? output = item.ToString();
                
                if (output != null)
                    result[i++] = output;
            }

            // If less than 6 entries, fill remaining with the first (or fallback)
            for (; i < 6; i++)
                result[i] = result[0];

            return result;
        }

        return null;
    }
    
    private static string[]? getEmissiveFaceTextureNamesForBlock(BlockType blockType)
    {
        string tomlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Blocks.toml");

        if (!File.Exists(tomlPath))
            throw new FileNotFoundException($"TOML file '{tomlPath}' not found.");

        using var reader = new StreamReader(tomlPath);
        var toml = Tommy.TOML.Parse(reader);

        if (!toml.HasKey(blockType.ToString()))
            return null;

        var table = toml[blockType.ToString()].AsTable;

        // Array form: EmissiveFaceTextures = [ ... ]
        if (table.HasKey("EmissiveFaceTextures"))
        {
            var arr = table["EmissiveFaceTextures"].AsArray;
            var result = new string[6];
            int i = 0;

            foreach (var item in arr)
            {
                if (i >= 6) break;

                string? output = item.ToString();

                if (output != null)
                    result[i++] = output;
            }

            for (; i < 6; i++)
                result[i] = result[0];

            return result;
        }

        // Single texture for all faces
        if (table.HasKey("EmissiveTexture"))
        {
            var t = table["EmissiveTexture"].ToString();
            if (!string.IsNullOrWhiteSpace(t))
            {
                return new[] { t, t, t, t, t, t };
            }
        }

        return null;
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

        foreach (BlockType type in Enum.GetValuesAsUnderlyingType(typeof(BlockType)))
        {
            if (!toml.HasKey(type.ToString()))
                continue; // Skip missing blocks

            var table = toml[type.ToString()].AsTable;
            
            //if (table == null)
            //    continue; // Not a table

            int hardness = table.HasKey("Hardness") ? (int)table["Hardness"].AsInteger.Value : 0;
            float thickness = table.HasKey("Thickness") ? (float)table["Thickness"].AsFloat.Value : 0f;
            int maxStack = table.HasKey("MaxStack") ? (int)table["MaxStack"].AsInteger.Value : 0;
            bool isTransparent = table.HasKey("IsTransparent") ? table["IsTransparent"].AsBoolean.Value : false;
            byte lightEmission = table.HasKey("LightEmission") ? (byte)table["LightEmission"].AsInteger.Value : (byte)0;
            bool antiTileUVsHorizontally = table.HasKey("AntiTileUVsHorizontally") ? table["AntiTileUVsHorizontally"].AsBoolean.Value : true;
            bool antiTileUVsVertically = table.HasKey("AntiTileUVsVertically") ? table["AntiTileUVsVertically"].AsBoolean.Value : true;
            bool isBillboard = table.HasKey("IsBillboard") ? table["IsBillboard"].AsBoolean.Value : false;
            
            
            var blockInfo = new BlockInfo(hardness, thickness, maxStack, isTransparent, lightEmission, isBillboard);
            blockInfo.AntiTileUVsHorizontally =  antiTileUVsHorizontally;
            blockInfo.AntiTileUVsVertically = antiTileUVsVertically;
            
            Prefabs[type] = blockInfo;
        }
    }
    
    private static Texture2D? _transparent16x16;
    private static Texture2D getTransparent16x16()
    {
        if (_transparent16x16 == null)
        {
            _transparent16x16 = new Texture2D(Engine.Graphics, 16, 16, false, SurfaceFormat.Color);
            var px = new Color[16 * 16];
        
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(0, 0, 0, 0);
        
            _transparent16x16.SetData(px);
        }
    
        return _transparent16x16;
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
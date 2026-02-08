using DIBBLES.Terrain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Utils;

public static class AtlasGenerator
{
    public struct AtlasResult
    {
        public Texture2D AtlasTexture;
        public Dictionary<(BlockType, int), RectangleF> BlockUVs; // [0,1] UV rectangles for each block
    }

    // Generates an atlas from a dictionary of block textures (already loaded, tile size must be consistent)
    public static AtlasResult GenerateBlockAtlas(GraphicsDevice graphicsDevice, BlockType[] blockTypes, Dictionary<(BlockType, int), Texture2D> textures, int tileSize = 16)
    {
        if (blockTypes == null || blockTypes.Length == 0)
            throw new ArgumentException("blockTypes must not be empty.");
    
        // Unique tiles (dedup identical content) and emissive scale per unique tile
        var uniqueTiles = new List<Color[]>(); // normalized to tileSize
        var uniqueRects = new List<RectangleF>();
        var uniqueScales = new List<float>();  // emissive scale per unique tile (1.0 for non-emissive)
        var hashToIndex = new Dictionary<ulong, int>();
        var keyToIndex = new Dictionary<(BlockType, int), int>();
    
        foreach (var kv in textures)
        {
            var key = kv.Key;
            var tex = kv.Value;
    
            if (tex == null)
                continue;
    
            var tilePixels = getTilePixels(tex, tileSize);
            ulong h = hashPixels(tilePixels);
    
            int idx;
    
            if (!hashToIndex.TryGetValue(h, out idx))
            {
                idx = uniqueTiles.Count;
                hashToIndex[h] = idx;
                uniqueTiles.Add(tilePixels);
    
                // Emissive scale: 1.0 for non-emissive, full emissive strength for now,
                // could be LightEmission / 7.5f (max ~2.0x)
                var info = BlockData.Prefabs[key.Item1];
                //float scale = (info.LightEmission > 0) ? (info.LightEmission * 0.06f) : 1.0f;
                float scale = (info.LightEmission > 0) ? (info.LightEmission) : 1.0f;
                uniqueScales.Add(scale);
            }
    
            keyToIndex[key] = idx;
        }
    
        int uniqueCount = uniqueTiles.Count;
    
        if (uniqueCount == 0)
            throw new InvalidOperationException("No textures found to pack into atlas.");
    
        // Near-square layout, clamp to next power-of-two
        int atlasCols = (int)Math.Ceiling(Math.Sqrt(uniqueCount));
        int atlasRows = (int)Math.Ceiling(uniqueCount / (float)atlasCols);
    
        int rawWidth = atlasCols * tileSize;
        int rawHeight = atlasRows * tileSize;
    
        int atlasWidth = nextPowerOfTwo(rawWidth);
        int atlasHeight = nextPowerOfTwo(rawHeight);
    
        int effCols = Math.Max(1, atlasWidth / tileSize);
        int effRows = Math.Max(1, atlasHeight / tileSize);
    
        if (effCols * effRows < uniqueCount)
            throw new InvalidOperationException("Power-of-two atlas is too small to fit all tiles.");
    
        // Allocate HDR atlas data (HalfVector4 supports >1.0 values)
        var atlasData = new Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4[atlasWidth * atlasHeight];
    
        // Initialize to transparent
        for (int i = 0; i < atlasData.Length; i++)
        {
            atlasData[i] = new Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4(0f, 0f, 0f, 0f);
        }
    
        // Blit each unique tile with emissive scaling into the HDR atlas
        for (int i = 0; i < uniqueCount; i++)
        {
            int col = i % effCols;
            int row = i / effCols;
    
            int startX = col * tileSize;
            int startY = row * tileSize;
    
            var src = uniqueTiles[i];
            float scale = uniqueScales[i];
    
            for (int y = 0; y < tileSize; y++)
            {
                int destY = startY + y;
    
                for (int x = 0; x < tileSize; x++)
                {
                    int destX = startX + x;
    
                    var c = src[y * tileSize + x];
    
                    // Convert to float, apply emissive scale to RGB, preserve alpha
                    float r = (c.R / 255f) * scale;
                    float g = (c.G / 255f) * scale;
                    float b = (c.B / 255f) * scale;
                    float a = (c.A / 255f);
    
                    atlasData[destY * atlasWidth + destX] = new Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4(r, g, b, a);
                }
            }
    
            float u = (float)startX / atlasWidth;
            float v = (float)startY / atlasHeight;
            float uSize = (float)tileSize / atlasWidth;
            float vSize = (float)tileSize / atlasHeight;
    
            uniqueRects.Add(new RectangleF(u, v, uSize, vSize));
        }
    
        // Create HDR Texture2D atlas
        var atlasTex = new Texture2D(graphicsDevice, atlasWidth, atlasHeight, false, SurfaceFormat.HalfVector4);
        atlasTex.SetData(atlasData);
    
        // Map (BlockType, faceIdx) -> rect
        var blockUVs = new Dictionary<(BlockType, int), RectangleF>(textures.Count);
    
        foreach (var kv in keyToIndex)
        {
            blockUVs[kv.Key] = uniqueRects[kv.Value];
        }
    
        return new AtlasResult
        {
            AtlasTexture = atlasTex,
            BlockUVs = blockUVs
        };
    }
    
    private static Color[] getTilePixels(Texture2D tex, int tileSize)
    {
        // Nearest-neighbor resample to tileSize if needed; otherwise return exact pixels.
        if (tex.Width == tileSize && tex.Height == tileSize)
        {
            var pixels = new Color[tileSize * tileSize];
            tex.GetData(pixels);
            return pixels;
        }

        var src = new Color[tex.Width * tex.Height];
        tex.GetData(src);

        var dst = new Color[tileSize * tileSize];

        for (int y = 0; y < tileSize; y++)
        {
            int srcY = y * tex.Height / tileSize;

            for (int x = 0; x < tileSize; x++)
            {
                int srcX = x * tex.Width / tileSize;
                dst[y * tileSize + x] = src[srcY * tex.Width + srcX];
            }
        }

        return dst;
    }
    
    private static ulong hashPixels(Color[] pixels)
    {
        // FNV-1a 64-bit over RGBA bytes
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;

        ulong h = offset;

        for (int i = 0; i < pixels.Length; i++)
        {
            // Expand to bytes; order RGBA
            h ^= pixels[i].R; h *= prime;
            h ^= pixels[i].G; h *= prime;
            h ^= pixels[i].B; h *= prime;
            h ^= pixels[i].A; h *= prime;
        }

        return h;
    }

    private static int nextPowerOfTwo(int v)
    {
        if (v <= 1)
            return 1;

        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        v++;

        return v;
    }
}
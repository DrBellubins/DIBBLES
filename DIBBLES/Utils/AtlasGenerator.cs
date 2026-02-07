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
    
        // Phase 1: Normalize each per-face texture to tileSize and compute a content hash.
        // Build a unique tile list keyed by hash, and remember which face maps to which unique index.
        var uniqueTiles = new List<Color[]>(); // normalized tiles
        var uniqueRects = new List<RectangleF>(); // will fill after layout
        var hashToIndex = new Dictionary<ulong, int>();
        var keyToIndex = new Dictionary<(BlockType, int), int>();
    
        foreach (var kv in textures)
        {
            var key = kv.Key;
            var tex = kv.Value;
    
            if (tex == null)
                continue;
    
            // Normalize to tileSize and get pixels
            var tilePixels = getTilePixels(tex, tileSize);
    
            // Hash pixels to dedupe identical content, even if Texture2D instances differ
            ulong h = hashPixels(tilePixels);
    
            int idx;
    
            if (!hashToIndex.TryGetValue(h, out idx))
            {
                idx = uniqueTiles.Count;
                hashToIndex[h] = idx;
                uniqueTiles.Add(tilePixels);
            }
    
            keyToIndex[key] = idx;
        }
    
        int uniqueCount = uniqueTiles.Count;
    
        if (uniqueCount == 0)
            throw new InvalidOperationException("No textures found to pack into atlas.");
    
        // Phase 2: Choose a near-square layout, then clamp dimensions to next power-of-two.
        int atlasCols = (int)Math.Ceiling(Math.Sqrt(uniqueCount));
        int atlasRows = (int)Math.Ceiling(uniqueCount / (float)atlasCols);
    
        int rawWidth = atlasCols * tileSize;
        int rawHeight = atlasRows * tileSize;
    
        int atlasWidth = nextPowerOfTwo(rawWidth);
        int atlasHeight = nextPowerOfTwo(rawHeight);
    
        // Recompute effective grid after PoT padding (gives us headroom columns/rows)
        int effCols = Math.Max(1, atlasWidth / tileSize);
        int effRows = Math.Max(1, atlasHeight / tileSize);
    
        if (effCols * effRows < uniqueCount)
            throw new InvalidOperationException("Power-of-two atlas is too small to fit all tiles.");
    
        // Phase 3: Blit each unique tile into the atlas and record normalized rects
        var atlasPixels = new Color[atlasWidth * atlasHeight];
    
        for (int i = 0; i < atlasPixels.Length; i++)
            atlasPixels[i] = Color.Transparent;
    
        for (int i = 0; i < uniqueCount; i++)
        {
            int col = i % effCols;
            int row = i / effCols;
    
            int startX = col * tileSize;
            int startY = row * tileSize;
    
            var src = uniqueTiles[i];
    
            for (int y = 0; y < tileSize; y++)
            {
                int destY = startY + y;
    
                for (int x = 0; x < tileSize; x++)
                {
                    int destX = startX + x;
                    atlasPixels[destY * atlasWidth + destX] = src[y * tileSize + x];
                }
            }
    
            float u = (float)startX / atlasWidth;
            float v = (float)startY / atlasHeight;
            float uSize = (float)tileSize / atlasWidth;
            float vSize = (float)tileSize / atlasHeight;
    
            uniqueRects.Add(new RectangleF(u, v, uSize, vSize));
        }
    
        // Create Texture2D
        var atlasTex = new Texture2D(graphicsDevice, atlasWidth, atlasHeight, false, SurfaceFormat.Color);
        atlasTex.SetData(atlasPixels);
    
        // Phase 4: Map every (blockType, faceIdx) to the rect of its unique tile
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
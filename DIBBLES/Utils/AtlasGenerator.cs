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
        if (blockTypes.Length == 0)
            throw new ArgumentException("blockTypes must not be empty.");

        // Find atlas grid size (square-ish, minimal wasted space)
        int atlasCols = (int)Math.Ceiling(Math.Sqrt(blockTypes.Length));
        int atlasRows = (int)Math.Ceiling(blockTypes.Length / (float)atlasCols);

        int atlasWidth = atlasCols * tileSize;
        int atlasHeight = atlasRows * tileSize;

        // Prepare the atlas pixel data (RGBA, row-major)
        Color[] atlasPixels = new Color[atlasWidth * atlasHeight];
        
        for (int i = 0; i < atlasPixels.Length; i++)
            atlasPixels[i] = Color.Transparent;

        var blockUVs = new Dictionary<(BlockType, int), RectangleF>();

        int idx = 0;
        foreach (var type in blockTypes)
        {
            int col = idx % atlasCols;
            int row = idx / atlasCols;

            Texture2D blockTex;
            
            if (!textures.TryGetValue(type, out blockTex) || blockTex == null)
            {
                // Fallback to Error block if missing
                if (!textures.TryGetValue(BlockType.Dirt, out blockTex)) // use Dirt as a fallback, or replace with your Error texture
                    throw new Exception("Missing fallback texture for atlas.");
            }

            // Read block texture data (ensure correct tile size)
            Color[] blockPixels = new Color[tileSize * tileSize];
            
            if (blockTex.Width != tileSize || blockTex.Height != tileSize)
            {
                // Downscale or upscale in memory (bruteforce nearest neighbor)
                // If you want bilinear, you can do that, but for blocks nearest is fine
                Color[] srcPixels = new Color[blockTex.Width * blockTex.Height];
                blockTex.GetData(srcPixels);
                
                for (int dy = 0; dy < tileSize; dy++)
                for (int dx = 0; dx < tileSize; dx++)
                {
                    int srcX = dx * blockTex.Width / tileSize;
                    int srcY = dy * blockTex.Height / tileSize;
                    
                    blockPixels[dy * tileSize + dx] = srcPixels[srcY * blockTex.Width + srcX];
                }
            }
            else
            {
                blockTex.GetData(blockPixels);
            }

            // Blit into atlas
            for (int y = 0; y < tileSize; y++)
            for (int x = 0; x < tileSize; x++)
            {
                int atlasX = col * tileSize + x;
                int atlasY = row * tileSize + y;
                
                atlasPixels[atlasY * atlasWidth + atlasX] = blockPixels[y * tileSize + x];
            }

            // Store UVs as normalized [0,1]
            float u = (float)(col * tileSize) / atlasWidth;
            float v = (float)(row * tileSize) / atlasHeight;
            float uSize = (float)tileSize / atlasWidth;
            float vSize = (float)tileSize / atlasHeight;

            blockUVs[type] = new RectangleF(u, v, uSize, vSize);

            idx++;
        }

        // Copy pixel data to MonoGame Texture2D
        Texture2D atlasTex = new Texture2D(graphicsDevice, atlasWidth, atlasHeight, false, SurfaceFormat.Color);
        atlasTex.SetData(atlasPixels);

        return new AtlasResult
        {
            AtlasTexture = atlasTex,
            BlockUVs = blockUVs
        };
    }
}
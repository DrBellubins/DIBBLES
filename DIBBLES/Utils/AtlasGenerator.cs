using DIBBLES.Terrain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DIBBLES.Utils;

public static class AtlasGenerator
{
    public struct AtlasResult
    {
        public Texture2D AtlasTexture;
        public Dictionary<BlockType, RectangleF> BlockUVs; // [0,1] UV rectangles for each block
    }

    // Generates an atlas from all block textures (expects square PNGs of same size)
    public static AtlasResult GenerateBlockAtlas(GraphicsDevice graphicsDevice, string blockTexturesDir, BlockType[] blockTypes, int tileSize = 16)
    {
        int atlasCols = (int)Math.Ceiling(Math.Sqrt(blockTypes.Length));
        int atlasRows = (int)Math.Ceiling(blockTypes.Length / (float)atlasCols);

        int atlasWidth = atlasCols * tileSize;
        int atlasHeight = atlasRows * tileSize;

        using var atlasImage = new Image<Rgba32>(atlasWidth, atlasHeight);
        var blockUVs = new Dictionary<BlockType, RectangleF>();

        int idx = 0;
        foreach (var type in blockTypes)
        {
            int col = idx % atlasCols;
            int row = idx / atlasCols;
            string path = Path.Combine(blockTexturesDir, $"{type}.png");
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            using var blockImg = Image.Load<Rgba32>(path);
            blockImg.Mutate(x => x.Resize(tileSize, tileSize));
            atlasImage.Mutate(x => x.DrawImage(blockImg, new Point(col * tileSize, row * tileSize), 1f));

            // Store UVs as normalized [0,1]
            float u = (float)(col * tileSize) / atlasWidth;
            float v = (float)(row * tileSize) / atlasHeight;
            float uSize = (float)tileSize / atlasWidth;
            float vSize = (float)tileSize / atlasHeight;
            blockUVs[type] = new RectangleF(u, v, uSize, vSize);

            idx++;
        }

        // Copy ImageSharp pixel data to a MonoGame Texture2D
        Texture2D atlasTex = new Texture2D(graphicsDevice, atlasWidth, atlasHeight, false, SurfaceFormat.Color);
        var pixelData = new Rgba32[atlasWidth * atlasHeight];
        atlasImage.CopyPixelDataTo(pixelData);

        // Convert Rgba32[] to Color[]
        var colorData = new Microsoft.Xna.Framework.Color[pixelData.Length];
        for (int i = 0; i < pixelData.Length; i++)
            colorData[i] = new Microsoft.Xna.Framework.Color(pixelData[i].R, pixelData[i].G, pixelData[i].B, pixelData[i].A);

        atlasTex.SetData(colorData);

        return new AtlasResult
        {
            AtlasTexture = atlasTex,
            BlockUVs = blockUVs
        };
    }
}
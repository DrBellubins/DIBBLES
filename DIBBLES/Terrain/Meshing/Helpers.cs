using System.Runtime.InteropServices;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Meshing;

public class Helpers
{
    // Helper to check if neighbor block is solid, including across chunk boundaries
    public static bool IsVoxelSolid(Chunk currentChunk, int x, int y, int z, NeighborCache cache)
    {
        // Fast in-chunk path
        if (x >= 0 && x < ChunkSize &&
            y >= 0 && y < ChunkSize &&
            z >= 0 && z < ChunkSize)
        {
            var info = currentChunk.GetInfoAt(x, y, z);
            return currentChunk.GetTypeAt(x, y, z) != BlockType.Air && !info.IsTransparent;
        }

        // Only axial neighbors are queried by meshing; if more than one axis is out, treat as non-solid
        int crosses =
            (x < 0 || x >= ChunkSize ? 1 : 0) +
            (y < 0 || y >= ChunkSize ? 1 : 0) +
            (z < 0 || z >= ChunkSize ? 1 : 0);

        if (crosses > 1)
            return false;

        Chunk target = currentChunk;
        int lx = x, ly = y, lz = z;

        // X axis wrap
        if (lx < 0)
        {
            if (cache.NegX == null)
                return false;

            target = cache.NegX;
            lx += ChunkSize;
        }
        else if (lx >= ChunkSize)
        {
            if (cache.PosX == null)
                return false;

            target = cache.PosX;
            lx -= ChunkSize;
        }

        // Y axis wrap
        if (ly < 0)
        {
            if (cache.NegY == null)
                return false;

            target = cache.NegY;
            ly += ChunkSize;
        }
        else if (ly >= ChunkSize)
        {
            if (cache.PosY == null)
                return false;

            target = cache.PosY;
            ly -= ChunkSize;
        }

        // Z axis wrap
        if (lz < 0)
        {
            if (cache.NegZ == null)
                return false;

            target = cache.NegZ;
            lz += ChunkSize;
        }
        else if (lz >= ChunkSize)
        {
            if (cache.PosZ == null)
                return false;

            target = cache.PosZ;
            lz -= ChunkSize;
        }

        var nInfo = target.GetInfoAt(lx, ly, lz);
        
        return target.GetTypeAt(lx, ly, lz) != BlockType.Air && !nInfo.IsTransparent;
    }
    
    // Fast scan of chunk's block types to see if any are transparent
    public static bool ChunkContainsTransparent(Chunk chunk)
    {
        var types = chunk.BlockTypes;

        for (int i = 0; i < types.Length; i++)
        {
            var type = (BlockType)types[i];
            var info = BlockData.Prefabs[type];

            if (info.IsTransparent && type != BlockType.Air)
                return true;
        }

        return false;
    }

    // Deterministic random flipping for this block face
    public static Vector2[] RndFlipUV(SeededRandom rng, BlockInfo blockInfo, Vector3Int pos, int faceIdx, Vector2[] faceUVs)
    {
        var rndOffset = (int)(rng.NextFloat() * ChunkSize);
        var worldBlockPosRNG = new Vector3Int(pos.X + rndOffset, pos.Y + rndOffset, pos.Z + rndOffset);
        
        int flipRandom = ((worldBlockPosRNG.X) ^ (worldBlockPosRNG.Y) ^ (worldBlockPosRNG.Z) ^ faceIdx) & 3;

        // For side faces, only allow axes enabled by TOML
        int flip = 0;
                    
        if (faceIdx >= 0 && faceIdx <= 3) // Sides
        {
            if (blockInfo.AntiTileUVsHorizontally && (flipRandom & 1) != 0)
                flip |= 1; // horizontal
            if (blockInfo.AntiTileUVsVertically && (flipRandom & 2) != 0)
                flip |= 2; // vertical
        }
        else // Top/bottom faces: always allow random flip
            flip = flipRandom;

        // TODO: AntiTileUVsHorizontally & AntiTileUVsVertically false does not disable flipping for tops and bottoms
        // See Feeb block
                    
        // Treat horizontal and vertical being false as disabling uv flipping for entire block
        if (blockInfo.AntiTileUVsHorizontally || blockInfo.AntiTileUVsVertically)
            return FaceUtils.FlipUVsAtlas(faceUVs, faceIdx, flip);
        else
            return [];
    }

    // Deterministic random rotation for this block face
    public static Vector2[] RndRotUV(Vector3Int pos, int faceIdx, Vector2[] faceUVs)
    {
        int rotation = ((pos.X) ^ (pos.Y) ^ (pos.Z) ^ faceIdx) & 3;
        return FaceUtils.RotateUVs(faceUVs, rotation);
    }
}

// Define a custom vertex struct with Position, Normal, Texcoord, Color
[StructLayout(LayoutKind.Sequential)]
public struct VertexPositionNormalTextureColor : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;   // Either absolute atlas UV (non-greedy) or local tile-space (greedy)
    public Color   Color;
    public Vector4 UVRect;     // Atlas sub-rect (x,y,w,h) for greedy tiling; zero for non-greedy

    public readonly static VertexDeclaration VertexDeclaration = new VertexDeclaration
    (
        new VertexElement(0,  VertexElementFormat.Vector3, VertexElementUsage.Position,          0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal,            0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Color,   VertexElementUsage.Color,             0),
        new VertexElement(36, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionNormalTextureColor(Vector3 pos, Vector3 norm, Vector2 tex, Color color, Vector4 uvRect)
    {
        Position = pos;
        Normal = norm;
        TexCoord = tex;
        Color = color;
        UVRect = uvRect;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct VertexBillboardInstance : IVertexType
{
    public Vector3 Center;
    public float Angle;
    public Vector2 Size;   // X = half width, Y = height
    public Color Color;

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration
    (
        new VertexElement(0,  VertexElementFormat.Vector3, VertexElementUsage.Position,          1), // POSITION1
        new VertexElement(12, VertexElementFormat.Single,  VertexElementUsage.TextureCoordinate, 1), // TEXCOORD1
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 2), // TEXCOORD2
        new VertexElement(24, VertexElementFormat.Color,   VertexElementUsage.Color,             1)  // COLOR1
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
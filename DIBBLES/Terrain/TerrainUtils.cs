using Microsoft.Xna.Framework;
using DIBBLES.Utils;
//using Raylib_cs;

namespace DIBBLES.Terrain;

public struct FaceData
{
    public Vector3[] Verts;
    public Vector3 Normal;
    public Vector2[] UVs;
    public Color[] Colors;
    public int VertexOffset;
    public float CenterDistance;   // For sorting
    //public Texture2D Texture;      // If faces may use different textures
}

public static class FaceUtils
{
    public static (int faceIdx, Vector3 normal, Vector3Int neighborOffset)[] VoxelFaceInfos()
    {
        return new[]
        {
            // Front (-Z)
            (0, new Vector3(0, 0, -1), new Vector3Int(0, 0, -1)),
            // Back (+Z)
            (1, new Vector3(0, 0, 1), new Vector3Int(0, 0, 1)),
            // Left (-X)
            (2, new Vector3(-1, 0, 0), new Vector3Int(-1, 0, 0)),
            // Right (+X)
            (3, new Vector3(1, 0, 0), new Vector3Int(1, 0, 0)),
            // Bottom (-Y)
            (4, new Vector3(0, -1, 0), new Vector3Int(0, -1, 0)),
            // Top (+Y)
            (5, new Vector3(0, 1, 0), new Vector3Int(0, 1, 0)),
        };
    }
    
    public static Vector3[] GetFaceVertices(Vector3 pos, int faceIdx)
    {
        // 8 cube corners
        Vector3[] cubeVertices =
        [
            pos + new Vector3(0, 0, 0), // 0
            pos + new Vector3(1, 0, 0), // 1
            pos + new Vector3(1, 1, 0), // 2
            pos + new Vector3(0, 1, 0), // 3
            pos + new Vector3(0, 0, 1), // 4
            pos + new Vector3(1, 0, 1), // 5
            pos + new Vector3(1, 1, 1), // 6
            pos + new Vector3(0, 1, 1), // 7
        ];

        // Face vertices by face
        return faceIdx switch
        {
            0 => new[] { cubeVertices[0], cubeVertices[3], cubeVertices[2], cubeVertices[1] }, // Front
            1 => new[] { cubeVertices[5], cubeVertices[6], cubeVertices[7], cubeVertices[4] }, // Back
            2 => new[] { cubeVertices[4], cubeVertices[7], cubeVertices[3], cubeVertices[0] }, // Left
            3 => new[] { cubeVertices[1], cubeVertices[2], cubeVertices[6], cubeVertices[5] }, // Right
            4 => new[] { cubeVertices[4], cubeVertices[0], cubeVertices[1], cubeVertices[5] }, // Bottom
            5 => new[] { cubeVertices[3], cubeVertices[7], cubeVertices[6], cubeVertices[2] }, // Top
            _ => new Vector3[4]
        };
    }
    
    public static Vector2[] GetFaceUVs(BlockType type, int faceIdx)
    {
        var blockInfo = BlockData.Prefabs[type];
        RectangleF uvRect;

        if (blockInfo.FaceUVs != null && blockInfo.FaceUVs.TryGetValue(faceIdx, out uvRect))
        {
            // Use per-face UV
        }
        else if (BlockData.AtlasUVs.TryGetValue((type, 0), out uvRect))
        {
            // Use default (all faces same)
        }
        else
        {
            uvRect = new RectangleF(0, 0, 1, 1);
        }

        Vector2[] uvCoords =
        {
            new Vector2(uvRect.X, uvRect.Y + uvRect.Height), // Top-left
            new Vector2(uvRect.X + uvRect.Width, uvRect.Y + uvRect.Height), // Top-right
            new Vector2(uvRect.X + uvRect.Width, uvRect.Y), // Bottom-right
            new Vector2(uvRect.X, uvRect.Y) // Bottom-left
        };

        return new[] { uvCoords[0], uvCoords[3], uvCoords[2], uvCoords[1] };
    }
    
    public static Vector2[] RotateUVs(Vector2[] uvs, int rotation)
    {
        // 0 = 0deg, 1 = 90deg, 2 = 180deg, 3 = 270deg
        Vector2[] result = new Vector2[4];
        
        for (int i = 0; i < 4; i++)
            result[i] = uvs[(i + rotation) % 4];
        
        return result;
    }
    
    public static Vector2[] FlipUVsAtlas(Vector2[] uvs, int faceIdx, int flip)
    {
        Vector2[] result = new Vector2[4];
        
        for (int i = 0; i < 4; i++) result[i] = uvs[i];

        // For sides
        if (faceIdx >= 0 && faceIdx <= 3)
        {
            if ((flip & 1) != 0) // Horizontal: mirror left-right
            {
                (result[0], result[3]) = (result[3], result[0]);
                (result[1], result[2]) = (result[2], result[1]);
            }
            
            if ((flip & 2) != 0) // Vertical: invert top-bottom
            {
                (result[0], result[1]) = (result[1], result[0]);
                (result[2], result[3]) = (result[3], result[2]);
            }
        }
        else
        {
            // Top/bottom: normal mapping
            if ((flip & 1) != 0)
            {
                (result[0], result[1]) = (result[1], result[0]);
                (result[3], result[2]) = (result[2], result[3]);
            }
        
            if ((flip & 2) != 0)
            {
                (result[0], result[3]) = (result[3], result[0]);
                (result[1], result[2]) = (result[2], result[1]);
            }
        }
        return result;
    }
    
    public static Color[] GetFaceColors(Chunk chunk, Vector3Int pos, int faceIdx)
    {
        // Lighting calculation for each face (copied from TerrainMesh.cs)
        float l0, l1, l2, l3;
    
        switch (faceIdx)
        {
            case 0: // Front (-Z)
                l0 = GetVertexLight(chunk, pos.X, pos.Y, pos.Z);
                l1 = GetVertexLight(chunk, pos.X, pos.Y + 1, pos.Z);
                l2 = GetVertexLight(chunk, pos.X + 1, pos.Y + 1, pos.Z);
                l3 = GetVertexLight(chunk, pos.X + 1, pos.Y, pos.Z);
                break;
            case 1: // Back (+Z)
                l0 = GetVertexLight(chunk, pos.X + 1, pos.Y, pos.Z + 1);
                l1 = GetVertexLight(chunk, pos.X + 1, pos.Y + 1, pos.Z + 1);
                l2 = GetVertexLight(chunk, pos.X, pos.Y + 1, pos.Z + 1);
                l3 = GetVertexLight(chunk, pos.X, pos.Y, pos.Z + 1);
                break;
            case 2: // Left (-X)
                l0 = GetVertexLight(chunk, pos.X, pos.Y, pos.Z + 1);
                l1 = GetVertexLight(chunk, pos.X, pos.Y + 1, pos.Z + 1);
                l2 = GetVertexLight(chunk, pos.X, pos.Y + 1, pos.Z);
                l3 = GetVertexLight(chunk, pos.X, pos.Y, pos.Z);
                break;
            case 3: // Right (+X)
                l0 = GetVertexLight(chunk, pos.X + 1, pos.Y, pos.Z);
                l1 = GetVertexLight(chunk, pos.X + 1, pos.Y + 1, pos.Z);
                l2 = GetVertexLight(chunk, pos.X + 1, pos.Y + 1, pos.Z + 1);
                l3 = GetVertexLight(chunk, pos.X + 1, pos.Y, pos.Z + 1);
                break;
            case 4: // Bottom (-Y)
                l0 = GetVertexLight(chunk, pos.X, pos.Y, pos.Z + 1);
                l1 = GetVertexLight(chunk, pos.X, pos.Y, pos.Z);
                l2 = GetVertexLight(chunk, pos.X + 1, pos.Y, pos.Z);
                l3 = GetVertexLight(chunk, pos.X + 1, pos.Y, pos.Z + 1);
                break;
            case 5: // Top (+Y)
                l0 = GetVertexLightTopFace(chunk, pos.X, pos.Y, pos.Z);
                l1 = GetVertexLightTopFace(chunk, pos.X, pos.Y, pos.Z + 1);
                l2 = GetVertexLightTopFace(chunk, pos.X + 1, pos.Y, pos.Z + 1);
                l3 = GetVertexLightTopFace(chunk, pos.X + 1, pos.Y, pos.Z);
                break;
            default:
                l0 = l1 = l2 = l3 = 1f;
                break;
        }
        
        // ToColor is from TerrainMesh.cs
        return new[] { ToColor(l0), ToColor(l1), ToColor(l2), ToColor(l3) };
    }

    // Helper: map [0,1] to Color
    public static Color ToColor(float light)
    {
        light = MathF.Max(0.1f, light); // Prevent fully dark
        
        var color = (byte)(255f * light);
        
        return new Color(color, color, color, (byte)255);
    }
    
    // Flat face light from neighbor cell in face direction
    public static float GetFaceLightFlat(Chunk chunk, Vector3Int pos, int faceIdx)
    {
        // Map faceIdx to neighbor offset (same ordering as VoxelFaceInfos)
        Vector3Int neighborOffset = faceIdx switch
        {
            0 => new Vector3Int(0, 0, -1), // Front (-Z)
            1 => new Vector3Int(0, 0,  1), // Back (+Z)
            2 => new Vector3Int(-1,0,  0), // Left (-X)
            3 => new Vector3Int( 1,0,  0), // Right (+X)
            4 => new Vector3Int( 0,-1, 0), // Bottom (-Y)
            5 => new Vector3Int( 0, 1, 0), // Top (+Y)
            _ => Vector3Int.Zero
        };

        int nx = pos.X + neighborOffset.X;
        int ny = pos.Y + neighborOffset.Y;
        int nz = pos.Z + neighborOffset.Z;

        byte lightLevel = 0;

        // If inside current chunk, read directly
        if (nx >= 0 && nx < TerrainGeneration.ChunkSize &&
            ny >= 0 && ny < TerrainGeneration.ChunkSize &&
            nz >= 0 && nz < TerrainGeneration.ChunkSize)
        {
            lightLevel = chunk.GetLightLevelAt(nx, ny, nz);
        }
        else
        {
            // Cross-chunk read: convert to world pos and use global helper
            var worldPos = new Vector3Int(
                chunk.Position.X + nx,
                chunk.Position.Y + ny,
                chunk.Position.Z + nz
            );

            lightLevel = GetLightLevelAtWorldPos(worldPos);
        }

        // Normalize to [0..1]
        return lightLevel / 15f;
    }
    
    // This computes the average light at a vertex, by sampling the 8 blocks touching it
    public static float GetVertexLight(Chunk chunk, int vx, int vy, int vz)
    {
        float total = 0f;
        int count = 0;
        
        for (int dx = 0; dx <= 1; dx++)
        for (int dy = 0; dy <= 1; dy++)
        for (int dz = 0; dz <= 1; dz++)
        {
            int nx = vx - dx;
            int ny = vy - dy;
            int nz = vz - dz;
            
            byte lightLevel;
            
            if (nx >= 0 && nx < TerrainGeneration.ChunkSize &&
                ny >= 0 && ny < TerrainGeneration.ChunkSize &&
                nz >= 0 && nz < TerrainGeneration.ChunkSize)
            {
                lightLevel = chunk.GetLightLevelAt(nx, ny, nz);
            }
            else
            {
                // Sample from neighboring chunk
                var worldPos = new Vector3Int(
                    chunk.Position.X + nx,
                    chunk.Position.Y + ny,
                    chunk.Position.Z + nz
                );
                
                lightLevel = GetLightLevelAtWorldPos(worldPos);
            }

            total += lightLevel;
            count++;
        }
        
        return total / (count * 15f); // Normalize to [0,1]
    }
    
    // TODO: Bottom blocks do not run this check
    public static float GetVertexLightTopFace(Chunk chunk, int vx, int vy, int vz)
    {
        float total = 0f;
        int count = 0;

        for (int dx = 0; dx <= 1; dx++)
        for (int dz = 0; dz <= 1; dz++)
        {
            int nx = vx - dx;
            int ny = vy + 1;
            int nz = vz - dz;

            byte lightLevel;
            
            if (nx >= 0 && nx < TerrainGeneration.ChunkSize &&
                ny >= 0 && ny < TerrainGeneration.ChunkSize &&
                nz >= 0 && nz < TerrainGeneration.ChunkSize)
            {
                lightLevel = chunk.GetLightLevelAt(nx, ny, nz);
            }
            else
            {
                // Sample from neighboring chunk
                var worldPos = new Vector3Int(
                    chunk.Position.X + nx,
                    chunk.Position.Y + ny,
                    chunk.Position.Z + nz
                );
                
                lightLevel = GetLightLevelAtWorldPos(worldPos);
            }

            total += lightLevel;
            count++;
        }

        return total / (count * 15f); // Normalize to [0,1]
    }
    
    public static byte GetLightLevelAtWorldPos(Vector3Int worldPos)
    {
        int chunkX = (int)Math.Floor((float)worldPos.X / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        int chunkY = (int)Math.Floor((float)worldPos.Y / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        int chunkZ = (int)Math.Floor((float)worldPos.Z / TerrainGeneration.ChunkSize) * TerrainGeneration.ChunkSize;
        
        var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (TerrainGeneration.ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
        {
            int localX = worldPos.X - chunkX;
            int localY = worldPos.Y - chunkY;
            int localZ = worldPos.Z - chunkZ;
            
            if (localX >= 0 && localX < TerrainGeneration.ChunkSize &&
                localY >= 0 && localY < TerrainGeneration.ChunkSize &&
                localZ >= 0 && localZ < TerrainGeneration.ChunkSize)
            {
                return chunk.GetLightLevelAt(localX, localY, localZ);
            }
        }
        
        return 0; // Treat as out-of-bounds
    }
}
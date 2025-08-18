using System.Numerics;
using System.Runtime.InteropServices;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using Raylib_cs;

using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

public class TerrainMesh
{
    public const bool Fullbright = true;
    public const bool SmoothLighting = false;
    
    public HashSet<Vector3Int> RecentlyRemeshedNeighbors = new();
    
    // MeshData generation (thread-safe, no Raylib calls)
    public MeshData GenerateMeshData(Chunk chunk)
    {
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Vector2> texcoords = [];
        List<Color> colors = [];

        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            if (chunk.Blocks[x, y, z]?.Info.Type == BlockType.Air) continue;

            var pos = new Vector3(x, y, z);
            var blockType = chunk.Blocks[x, y, z].Info.Type;
            int vertexOffset = vertices.Count;

            // Define cube vertices (8 corners)
            Vector3[] cubeVertices =
            [
                pos + new Vector3(0, 0, 0), pos + new Vector3(1, 0, 0),
                pos + new Vector3(1, 1, 0), pos + new Vector3(0, 1, 0),
                pos + new Vector3(0, 0, 1), pos + new Vector3(1, 0, 1),
                pos + new Vector3(1, 1, 1), pos + new Vector3(0, 1, 1)
            ];

            // Get UVs from atlas
            Vector2[] uvCoords;
            if (Block.AtlasUVs.TryGetValue(blockType, out var uvRect))
            {
                uvCoords = new Vector2[]
                {
                    new Vector2(uvRect.X, uvRect.Y + uvRect.Height), // Top-left
                    new Vector2(uvRect.X + uvRect.Width, uvRect.Y + uvRect.Height), // Top-right
                    new Vector2(uvRect.X + uvRect.Width, uvRect.Y), // Bottom-right
                    new Vector2(uvRect.X, uvRect.Y) // Bottom-left
                };
            }
            else
            {
                uvCoords = new Vector2[]
                {
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(1, 0), new Vector2(0, 0)
                };
            }

            Vector2[] rotatedUvCoords = new Vector2[]
            {
                uvCoords[1], uvCoords[2], uvCoords[3], uvCoords[0]
            };

            // Front face (-Z)
            if (!isVoxelSolid(chunk, x, y, z - 1))
            {
                // Vertices: 0,3,2,1
                float l0 = getVertexLight(chunk, x,   y,   z  );
                float l1 = getVertexLight(chunk, x,   y+1, z  );
                float l2 = getVertexLight(chunk, x+1, y+1, z  );
                float l3 = getVertexLight(chunk, x+1, y,   z  );
                
                if (Fullbright)
                {
                    l0 = 1f;
                    l1 = 1f;
                    l2 = 1f;
                    l3 = 1f;
                }
                
                colors.AddRange([
                    ToColor(l0), ToColor(l1), ToColor(l2), ToColor(l3)
                ]);
                
                vertices.AddRange([cubeVertices[0], cubeVertices[3], cubeVertices[2], cubeVertices[1]]);
                normals.AddRange([new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1)]);
                texcoords.AddRange(rotatedUvCoords);
                indices.AddRange([vertexOffset, vertexOffset + 2, vertexOffset + 3, vertexOffset, vertexOffset + 1, vertexOffset + 2]);
                
                vertexOffset += 4;
            }
            
            // Back face (+Z)
            if (!isVoxelSolid(chunk, x, y, z + 1))
            {
                // Vertices: 5,6,7,4
                float l0 = getVertexLight(chunk, x+1, y,   z+1 );
                float l1 = getVertexLight(chunk, x+1, y+1, z+1 );
                float l2 = getVertexLight(chunk, x,   y+1, z+1 );
                float l3 = getVertexLight(chunk, x,   y,   z+1 );
                
                if (Fullbright)
                {
                    l0 = 1f;
                    l1 = 1f;
                    l2 = 1f;
                    l3 = 1f;
                }
                
                colors.AddRange([
                    ToColor(l0), ToColor(l1), ToColor(l2), ToColor(l3)
                ]);
                
                vertices.AddRange([cubeVertices[5], cubeVertices[6], cubeVertices[7], cubeVertices[4]]);
                normals.AddRange([new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1)]);
                texcoords.AddRange(rotatedUvCoords);
                indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                
                vertexOffset += 4;
            }
            
            // Left face (-X)
            if (!isVoxelSolid(chunk, x - 1, y, z))
            {
                // Vertices: 4,7,3,0
                float l0 = getVertexLight(chunk, x,   y,   z+1 );
                float l1 = getVertexLight(chunk, x,   y+1, z+1 );
                float l2 = getVertexLight(chunk, x,   y+1, z   );
                float l3 = getVertexLight(chunk, x,   y,   z   );
                
                if (Fullbright)
                {
                    l0 = 1f;
                    l1 = 1f;
                    l2 = 1f;
                    l3 = 1f;
                }
                
                colors.AddRange([
                    ToColor(l0), ToColor(l1), ToColor(l2), ToColor(l3)
                ]);
                
                vertices.AddRange([cubeVertices[4], cubeVertices[7], cubeVertices[3], cubeVertices[0]]);
                normals.AddRange([new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0)]);
                texcoords.AddRange(rotatedUvCoords);
                indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                
                vertexOffset += 4;
            }
            
            // Right face (+X)
            if (!isVoxelSolid(chunk, x + 1, y, z))
            {
                // Vertices: 1,2,6,5
                float l0 = getVertexLight(chunk, x+1, y,   z   );
                float l1 = getVertexLight(chunk, x+1, y+1, z   );
                float l2 = getVertexLight(chunk, x+1, y+1, z+1 );
                float l3 = getVertexLight(chunk, x+1, y,   z+1 );
                
                if (Fullbright)
                {
                    l0 = 1f;
                    l1 = 1f;
                    l2 = 1f;
                    l3 = 1f;
                }
                
                colors.AddRange([
                    ToColor(l0), ToColor(l1), ToColor(l2), ToColor(l3)
                ]);
                
                vertices.AddRange([cubeVertices[1], cubeVertices[2], cubeVertices[6], cubeVertices[5]]);
                normals.AddRange([new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)]);
                texcoords.AddRange(rotatedUvCoords);
                indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                
                vertexOffset += 4;
            }
            
            // Bottom face (-Y)
            if (!isVoxelSolid(chunk, x, y - 1, z))
            {
                // Vertices: 4,0,1,5
                float l0 = getVertexLight(chunk, x,   y,   z+1 );
                float l1 = getVertexLight(chunk, x,   y,   z   );
                float l2 = getVertexLight(chunk, x+1, y,   z   );
                float l3 = getVertexLight(chunk, x+1, y,   z+1 );
                
                if (Fullbright)
                {
                    l0 = 1f;
                    l1 = 1f;
                    l2 = 1f;
                    l3 = 1f;
                }
                
                colors.AddRange([
                    ToColor(l0), ToColor(l1), ToColor(l2), ToColor(l3)
                ]);
                
                vertices.AddRange([cubeVertices[4], cubeVertices[0], cubeVertices[1], cubeVertices[5]]);
                normals.AddRange([new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0)]);
                texcoords.AddRange(rotatedUvCoords);
                indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                
                vertexOffset += 4;
            }
            
            // Top face (+Y)
            if (!isVoxelSolid(chunk, x, y + 1, z))
            {
                // Vertices: 3,7,6,2
                float l0 = getVertexLightTopFace(chunk, x,   y, z   );  
                float l1 = getVertexLightTopFace(chunk, x,   y, z + 1 );
                float l2 = getVertexLightTopFace(chunk, x+1, y, z + 1 );
                float l3 = getVertexLightTopFace(chunk, x+1, y, z   );  
                
                if (Fullbright)
                {
                    l0 = 1f;
                    l1 = 1f;
                    l2 = 1f;
                    l3 = 1f;
                }
                
                colors.AddRange([
                    ToColor(l0), ToColor(l1), ToColor(l2), ToColor(l3)
                ]);
                
                vertices.AddRange([cubeVertices[3], cubeVertices[7], cubeVertices[6], cubeVertices[2]]);
                normals.AddRange([new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0)]);
                texcoords.AddRange(rotatedUvCoords);
                indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
            }
        }

        // Convert lists to arrays
        int vcount = vertices.Count;
        int icount = indices.Count / 3;
        var meshData = new MeshData(vcount, icount);

        for (int i = 0; i < vertices.Count; i++)
        {
            meshData.Vertices[i * 3 + 0] = vertices[i].X;
            meshData.Vertices[i * 3 + 1] = vertices[i].Y;
            meshData.Vertices[i * 3 + 2] = vertices[i].Z;
        }
        for (int i = 0; i < normals.Count; i++)
        {
            meshData.Normals[i * 3 + 0] = normals[i].X;
            meshData.Normals[i * 3 + 1] = normals[i].Y;
            meshData.Normals[i * 3 + 2] = normals[i].Z;
        }
        for (int i = 0; i < texcoords.Count; i++)
        {
            meshData.TexCoords[i * 2 + 0] = texcoords[i].X;
            meshData.TexCoords[i * 2 + 1] = texcoords[i].Y;
        }
        for (int i = 0; i < colors.Count; i++)
        {
            meshData.Colors[i * 4 + 0] = colors[i].R;
            meshData.Colors[i * 4 + 1] = colors[i].G;
            meshData.Colors[i * 4 + 2] = colors[i].B;
            meshData.Colors[i * 4 + 3] = colors[i].A;
        }
        for (int i = 0; i < indices.Count; i++)
        {
            meshData.Indices[i] = (ushort)indices[i];
        }

        return meshData;
    }
    
    // Main-thread only: allocates Raylib Mesh, uploads data, returns Model
    public Model UploadMesh(MeshData data)
    {
        unsafe
        {
            Mesh mesh = new Mesh
            {
                VertexCount = data.VertexCount,
                TriangleCount = data.TriangleCount
            };

            // Allocate and copy arrays
            mesh.Vertices = (float*)Raylib.MemAlloc((uint)data.Vertices.Length * sizeof(float));
            Marshal.Copy(data.Vertices, 0, (IntPtr)mesh.Vertices, data.Vertices.Length);

            mesh.Normals = (float*)Raylib.MemAlloc((uint)data.Normals.Length * sizeof(float));
            Marshal.Copy(data.Normals, 0, (IntPtr)mesh.Normals, data.Normals.Length);

            mesh.TexCoords = (float*)Raylib.MemAlloc((uint)data.TexCoords.Length * sizeof(float));
            Marshal.Copy(data.TexCoords, 0, (IntPtr)mesh.TexCoords, data.TexCoords.Length);

            mesh.Colors = (byte*)Raylib.MemAlloc((uint)data.Colors.Length * sizeof(byte));
            Marshal.Copy(data.Colors, 0, (IntPtr)mesh.Colors, data.Colors.Length);

            mesh.Indices = (ushort*)Raylib.MemAlloc((uint)data.Indices.Length * sizeof(ushort));
            
            byte[] indicesBytes = new byte[data.Indices.Length * sizeof(ushort)];
            Buffer.BlockCopy(data.Indices, 0, indicesBytes, 0, indicesBytes.Length);
            Marshal.Copy(indicesBytes, 0, (IntPtr)mesh.Indices, indicesBytes.Length);

            Raylib.UploadMesh(&mesh, false);

            Model model = Raylib.LoadModelFromMesh(mesh);

            // Assign texture atlas
            if (Block.TextureAtlas.Id != 0)
            {
                model.Materials[0].Shader = Raylib.LoadMaterialDefault().Shader;
                model.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = Block.TextureAtlas;
            }

            return model;
        }
    }
    
    // Helper: map [0,1] to Color
    private static Color ToColor(float light)
    {
        light = MathF.Max(0.1f, light); // Prevent fully dark
        
        var color = (byte)(255f * light);
        
        return new Color(color, color, color, (byte)255);
    }

    // This computes the average light at a vertex, by sampling the 8 blocks touching it
    private float getVertexLight(Chunk chunk, int vx, int vy, int vz)
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
            
            total += neighborLightLevel(chunk, nx, ny, nz);
            
            count++;
        }
        
        return total / (count * 15f); // Normalize to [0,1]
    }

    // TODO: Bottom blocks do not run this check
    private float getVertexLightTopFace(Chunk chunk, int vx, int vy, int vz)
    {
        // For each vertex, sample only the 4 blocks directly above it
        // The 4 blocks are at (vx, vy+1, vz), (vx-1, vy+1, vz), (vx, vy+1, vz-1), (vx-1, vy+1, vz-1)
        float total = 0f;
        int count = 0;
        for (int dx = 0; dx <= 1; dx++)
        for (int dz = 0; dz <= 1; dz++)
        {
            int nx = vx - dx;
            int ny = vy + 1;
            int nz = vz - dz;
            total += neighborLightLevel(chunk, nx, ny, nz);
            count++;
        }
        return total / (count * 15f); // Normalize to [0,1]
    }
    
    private bool isVoxelSolid(Chunk chunk, int x, int y, int z)
    {
        BlockInfo info = null;

        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize || z < 0 || z >= ChunkSize)
        {
            // Compute which chunk to check in all axes
            Vector3Int chunkCoord = new Vector3Int(
                chunk.Position.X / ChunkSize,
                chunk.Position.Y / ChunkSize,
                chunk.Position.Z / ChunkSize
            );

            Vector3Int neighborCoord = chunkCoord;
            int nx = x, ny = y, nz = z;

            if (x < 0) { nx = ChunkSize - 1; neighborCoord.X -= 1; }
            else if (x >= ChunkSize) { nx = 0; neighborCoord.X += 1; }

            if (y < 0) { ny = ChunkSize - 1; neighborCoord.Y -= 1; }
            else if (y >= ChunkSize) { ny = 0; neighborCoord.Y += 1; }

            if (z < 0) { nz = ChunkSize - 1; neighborCoord.Z -= 1; }
            else if (z >= ChunkSize) { nz = 0; neighborCoord.Z += 1; }

            Vector3Int neighborChunkPos = new Vector3Int(
                neighborCoord.X * ChunkSize,
                neighborCoord.Y * ChunkSize,
                neighborCoord.Z * ChunkSize
            );

            // Look up the neighboring chunk
            if (Chunks.TryGetValue(neighborChunkPos, out var neighborChunk))
            {
                info = neighborChunk.Blocks[nx, ny, nz]?.Info;
            }
        }
        else
        {
            info = chunk.Blocks[x, y, z]?.Info;
        }

        // Air or transparent blocks are NOT solid
        if (info == null)
            return false;
        
        return info.Type != BlockType.Air && !info.IsTransparent;
    }

    public void RemeshNeighbors(Chunk chunk)
    {
        int[] offsets = { -ChunkSize, ChunkSize };
        
        foreach (var axis in new[] { 0, 1, 2 })
        {
            foreach (int offset in offsets)
            {
                Vector3Int neighborPos = chunk.Position;
                
                if (axis == 0) neighborPos.X += offset;
                if (axis == 1) neighborPos.Y += offset;
                if (axis == 2) neighborPos.Z += offset;

                if (Chunks.TryGetValue(neighborPos, out var neighborChunk))
                    RemeshNeighborPos(neighborChunk.Position);
            }
        }
    }
    
    public void RemeshNeighborPos(Vector3Int neighborPos)
    {
        if (RecentlyRemeshedNeighbors.Contains(neighborPos))
            return; // Already remeshed this frame
        
        if (Chunks.TryGetValue(neighborPos, out var neighborChunk))
        {
            Raylib.UnloadModel(neighborChunk.Model);
            
            var meshData = GameScene.TMesh.GenerateMeshData(neighborChunk);
            neighborChunk.Model = GameScene.TMesh.UploadMesh(meshData);
            
            RecentlyRemeshedNeighbors.Add(neighborPos);
        }
    }
    
    public byte averageNeighborLightLevel(Chunk chunk, int x, int y, int z)
    {
        int[] dx = { -1, 1, 0, 0, 0, 0 };
        int[] dy = { 0, 0, -1, 1, 0, 0 };
        int[] dz = { 0, 0, 0, 0, -1, 1 };

        int totalLight = 0;
        int count = 0;

        for (int i = 0; i < 6; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];
            int nz = z + dz[i];
            byte light = neighborLightLevel(chunk, nx, ny, nz);
            totalLight += light;
            count++;
        }

        // Clamp and cast to byte
        int averaged = count > 0 ? (totalLight / count) : 0;
        return (byte)Math.Clamp(averaged, 0, 15);
    }
    
    private byte neighborLightLevel(Chunk chunk, int nx, int ny, int nz)
    {
        if (nx < 0 || nx >= ChunkSize || ny < 0 || ny >= ChunkSize || nz < 0 || nz >= ChunkSize)
        {
            // Calculate the current chunk's coordinates from its Position
            Vector3Int chunkCoord = new Vector3Int(
                chunk.Position.X / ChunkSize,
                chunk.Position.Y / ChunkSize,
                chunk.Position.Z / ChunkSize
            );

            // Adjust chunk coordinates based on out-of-bounds voxel
            Vector3Int neighborCoord = chunkCoord;
            int tx = nx, ty = ny, tz = nz;

            if (nx < 0) { tx = ChunkSize - 1; neighborCoord.X -= 1; }
            else if (nx >= ChunkSize) { tx = 0; neighborCoord.X += 1; }

            if (ny < 0) { ty = ChunkSize - 1; neighborCoord.Y -= 1; }
            else if (ny >= ChunkSize) { ty = 0; neighborCoord.Y += 1; }

            if (nz < 0) { tz = ChunkSize - 1; neighborCoord.Z -= 1; }
            else if (nz >= ChunkSize) { tz = 0; neighborCoord.Z += 1; }

            // Look up the neighboring chunk
            if (Chunks.TryGetValue(new Vector3Int(
                    neighborCoord.X * ChunkSize,
                    neighborCoord.Y * ChunkSize,
                    neighborCoord.Z * ChunkSize
                ), out var neighborChunk))
            {
                // Defensive: check indices and block existence
                if (tx >= 0 && tx < ChunkSize && ty >= 0 && ty < ChunkSize && tz >= 0 && tz < ChunkSize)
                {
                    var neighborBlock = neighborChunk.Blocks[tx, ty, tz];
                    
                    if (neighborBlock != null)
                        return neighborBlock.LightLevel;
                }
            }

            return 0;
        }
        else
        {
            var block = chunk.Blocks[nx, ny, nz];
            
            if (block != null)
                return block.LightLevel;
            return 0;
        }
    }
}
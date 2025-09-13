using System.Numerics;
using System.Runtime.InteropServices;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using Raylib_cs;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainMesh
{
    public const bool Fullbright = false;
    public const bool SmoothLighting = false;
    
    public HashSet<Vector3Int> RecentlyRemeshedNeighbors = new();

    public Dictionary<Vector3Int, Model> OpaqueModels = new();
    public Dictionary<Vector3Int, Model> TransparentModels = new();
    
    // MeshData generation (thread-safe, no Raylib calls)
    public MeshData GenerateMeshData(Chunk chunk, bool isTransparencyPass, Vector3? cameraPosition = null)
    {
        List<(float dist, FaceData face)> transparentFaces = new();
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Vector2> texcoords = [];
        List<Color> colors = [];

        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var pos = new Vector3(x, y, z);
            var block = chunk.GetBlock(x, y, z);
            var blockType = block.Type;
            var isTransparent = block.Info.IsTransparent;

            // Opaque mesh pass: skip transparent and air blocks
            if (!isTransparencyPass && (isTransparent || blockType == BlockType.Air)) continue;

            // Transparent mesh pass: skip opaque and air blocks
            if (isTransparencyPass && (!isTransparent || blockType == BlockType.Air)) continue;
            
            int vertexOffset = vertices.Count;

            foreach (var (faceIdx, normal, neighborOffset) in FaceUtils.VoxelFaceInfos())
            {
                int nx = x + neighborOffset.X;
                int ny = y + neighborOffset.Y;
                int nz = z + neighborOffset.Z;
        
                if (!isVoxelSolid(chunk, isTransparencyPass, nx, ny, nz))
                {
                    var faceVerts = FaceUtils.GetFaceVertices(pos, faceIdx);
                    var faceUVs = FaceUtils.GetFaceUVs(blockType, faceIdx);
                    var faceColors = FaceUtils.GetFaceColors(chunk, Vector3Int.FromVector3(pos), faceIdx);
        
                    if (isTransparencyPass && cameraPosition.HasValue)
                    {
                        // For transparent faces, store for sorting
                        var center = (faceVerts[0] + faceVerts[1] + faceVerts[2] + faceVerts[3]) / 4f;
                        var dist = Vector3.Distance(cameraPosition.Value, center);
                        
                        transparentFaces.Add((dist, new FaceData
                        {
                            Verts = faceVerts,
                            Normal = normal,
                            UVs = faceUVs,
                            Colors = faceColors,
                            VertexOffset = vertexOffset,
                            CenterDistance =  dist
                        }));
                    }
                    else
                    {
                        // Opaque faces: add immediately
                        vertices.AddRange(faceVerts);
                        normals.AddRange(Enumerable.Repeat(normal, 4));
                        texcoords.AddRange(faceUVs);
                        colors.AddRange(faceColors);
        
                        indices.AddRange(new int[]
                        {
                            vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
                            vertexOffset + 0, vertexOffset + 2, vertexOffset + 3
                        });
        
                        vertexOffset += 4;
                    }
                }
            }
        }
        
        // If transparent: sort faces back-to-front and build arrays
        if (isTransparencyPass && cameraPosition.HasValue)
        {
            transparentFaces.Sort((a, b) => b.dist.CompareTo(a.dist));
            
            int vertexOffset = 0;
            
            foreach (var (_, face) in transparentFaces)
            {
                vertices.AddRange(face.Verts);
                normals.AddRange(Enumerable.Repeat(face.Normal, 4));
                texcoords.AddRange(face.UVs);
                colors.AddRange(face.Colors);
                
                indices.AddRange(new int[]
                {
                    vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
                    vertexOffset + 0, vertexOffset + 2, vertexOffset + 3
                });
                
                vertexOffset += 4;
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
            if (BlockData.TextureAtlas.Id != 0)
            {
                model.Materials[0].Shader = TerrainShader;
                model.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = BlockData.TextureAtlas;
            }

            return model;
        }
    }
    
    private bool isVoxelSolid(Chunk chunk, bool isTransparentPass, int x, int y, int z)
    {
        if (isTransparentPass)
            return false;
        
        BlockInfo info = new BlockInfo();
        
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
            if (ECSChunks.TryGetValue(neighborChunkPos, out var neighborChunk))
            {
                info = neighborChunk.GetBlock(nx, ny, nz).Info;
            }
        }
        else
        {
            info = chunk.GetBlock(x, y, z).Info;
        }

        // Air blocks are NOT solid
        //if (info == null)
        //    return false;
        
        if (!isTransparentPass) // Opaque pass: treat transparent blocks as non-solid
            return chunk.GetBlock(x, y, z).Type != BlockType.Air && !info.IsTransparent;
        else // Transparent pass: treat only transparent blocks as solid
            return chunk.GetBlock(x, y, z).Type != BlockType.Air && info.IsTransparent;
    }

    public void RemeshNeighbors(Chunk chunk, bool isTransparentPass)
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

                if (ECSChunks.TryGetValue(neighborPos, out var neighborChunk))
                    RemeshNeighborPos(neighborChunk.Position, isTransparentPass);
            }
        }
    }
    
    public void RemeshNeighborPos(Vector3Int neighborPos, bool isTransparentPass)
    {
        if (RecentlyRemeshedNeighbors.Contains(neighborPos))
            return; // Already remeshed this frame

        if (ECSChunks.TryGetValue(neighborPos, out var neighborChunk))
        {
            // Opaque or transparent model dictionary
            var modelDict = isTransparentPass ? TransparentModels : OpaqueModels;

            // Unload existing model if present
            if (modelDict.TryGetValue(neighborPos, out var oldModel) && oldModel.MeshCount > 0)
                Raylib.UnloadModel(oldModel);

            // Generate new mesh
            var meshData = GenerateMeshData(neighborChunk, isTransparentPass);
            var newModel = UploadMesh(meshData);

            // Store new model
            modelDict[neighborPos] = newModel;

            RecentlyRemeshedNeighbors.Add(neighborPos);
        }
    }
    
    private byte getNeighborLightLevel(Chunk chunk, int nx, int ny, int nz)
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
            if (ECSChunks.TryGetValue(new Vector3Int(
                    neighborCoord.X * ChunkSize,
                    neighborCoord.Y * ChunkSize,
                    neighborCoord.Z * ChunkSize
                ), out var neighborChunk))
            {
                // Defensive: check indices and block existence
                if (tx >= 0 && tx < ChunkSize && ty >= 0 && ty < ChunkSize && tz >= 0 && tz < ChunkSize)
                {
                    try
                    {
                        // TODO: Can sometimes cause a crash???
                        var neighborBlock = neighborChunk.GetBlock(tx, ty, tz);
                        
                        return neighborBlock.LightLevel;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            return 0;
        }
        else
        {
            var block = chunk.GetBlock(nx, ny, nz);
            
            return block.LightLevel;
        }
    }
}